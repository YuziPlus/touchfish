"""Frozen-batch Monte Carlo PPO with independently optimized critic."""
import copy
import math
from collections import defaultdict

import numpy as np
import torch
from torch import nn


def grouped_order(groups, rng):
    """Interleave shuffled games; consume each decision exactly once per epoch."""
    queues = defaultdict(list)
    for i, group in enumerate(groups):
        queues[group].append(i)
    for queue in queues.values():
        rng.shuffle(queue)
    active = list(queues)
    while active:
        rng.shuffle(active)
        for group in active:
            yield queues[group].pop()
        active = [group for group in active if queues[group]]


def optimize(actor, critic, rows, labels, groups, behavior, batches, seed, temperature, lr,
             actor_batch=256, critic_batch=256, epochs=1, critic_epochs=1,
             clip=.2, max_kl=.01, entropy=.01, weighting="game"):
    if min(actor_batch, critic_batch, epochs, critic_epochs)<1 or not (
        0<clip<1 and math.isfinite(max_kl) and max_kl>0 and
        math.isfinite(entropy) and entropy>=0 and math.isfinite(lr) and lr>0
    ):
        raise ValueError("Invalid PPO configuration")
    if weighting=="decision":
        rows=[(*r[:3],1.) for r in rows]
    rng=np.random.default_rng(seed)
    train_actor=torch.optim.AdamW(actor.parameters(),lr=lr)
    train_critic=torch.optim.AdamW(critic.parameters(),lr=lr)
    x=torch.from_numpy(np.stack([r[0] for r in rows]))
    z=torch.tensor(labels,dtype=torch.float32)
    with torch.no_grad():
        values=critic(x).flatten()
        advantage=z-values.sigmoid()
    eligible=[i for i,r in enumerate(rows) if len(r[1])>1]
    if not eligible:raise ValueError("No eligible actor decisions")
    actor_rows=[rows[i] for i in eligible]
    actor_groups=[groups[i] for i in eligible]
    adv=advantage[eligible]
    old=[torch.tensor(behavior[i],dtype=torch.float64) for i in eligible]
    selected=[int(rows[i][2].argmax()) for i in eligible]
    old_log=torch.tensor([math.log(float(p[j])) for p,j in zip(old,selected)],dtype=torch.float64)
    def logp(x,a,mask):
        return (actor(x,a)/temperature).double().masked_fill(~mask,-1e9).log_softmax(1)
    # Fixed whole-batch KL guard. Pad once per evaluation chunk, preserving every legal candidate.
    audit=[]
    parity=0.
    for start in range(0,len(old),256):
        ix=list(range(start,min(start+256,len(old))))
        bx,ba,by,mask,w=next(batches(actor_rows,256,ix))
        p=torch.zeros(mask.shape,dtype=torch.float64)
        for j,i in enumerate(ix):p[j,:len(old[i])]=old[i]
        with torch.no_grad():parity=max(parity,float((logp(bx,ba,mask).exp()-p).abs().max()))
        audit.append((bx,ba,by,mask,w,p))
    if parity>2e-6:raise ValueError(f"Frozen behavior probability mismatch: {parity}")
    def statistics():
        kl=[];ratios=[];ent=[];tv=[];weights=[]
        with torch.no_grad():
            for bx,ba,by,mask,w,p in audit:
                q=logp(bx,ba,mask)
                kl.append((p*(p.clamp_min(1e-300).log()-q)).sum(1))
                ratios.append(((q*by).sum(1)-p.gather(1,by.argmax(1)[:,None]).flatten().log()).exp())
                ent.append(-(q.exp()*q).sum(1))
                tv.append((p-q.exp()).abs().sum(1)/2)
                weights.append(w)
        k,r,h,t,w=map(torch.cat,(kl,ratios,ent,tv,weights))
        if not all(torch.isfinite(v).all() for v in (k,r,h,t)):raise ValueError("Nonfinite PPO statistics")
        mean=lambda v:float((v*w).sum()/w.sum())
        return dict(kl=mean(k),kl_p95=float(k.quantile(.95)),kl_max=float(k.max()),approx_kl=mean(r-1-r.log()),
                    ratio_quantiles=r.quantile(torch.tensor([0,.05,.5,.95,1],dtype=r.dtype)).tolist(),
                    clip_fraction=mean((r-1).abs()>clip),entropy=mean(h),tv=mean(t))
    first=statistics()
    logs=[]; accepted=0; stopped=None
    for epoch in range(epochs):
        order=list(grouped_order(actor_groups,rng))
        for start in range(0,len(order),actor_batch):
            ix=order[start:start+actor_batch]
            bx,ba,by,mask,w=next(batches(actor_rows,actor_batch,ix))
            p=logp(bx,ba,mask)
            ratio=((p*by).sum(1)-old_log[ix]).exp()
            objective=torch.minimum(ratio*adv[ix],ratio.clamp(1-clip,1+clip)*adv[ix])
            loss=(-(objective-entropy*(p.exp()*p).sum(1))*w).sum()/w.sum()
            train_actor.zero_grad();loss.backward()
            norm=nn.utils.clip_grad_norm_(actor.parameters(),1,error_if_nonfinite=True)
            previous=copy.deepcopy(actor.state_dict())
            previous_opt=copy.deepcopy(train_actor.state_dict())
            train_actor.step()
            delta=math.sqrt(sum(float((v-previous[k]).square().sum()) for k,v in actor.state_dict().items()))
            stats=statistics()
            # A high percentile catches concentrated drift; entropy guard catches broad collapse.
            ok=stats["kl"]<=max_kl and stats["kl_p95"]<=4*max_kl and stats["entropy"]>=.75*first["entropy"]
            logs.append(dict(epoch=epoch+1,accepted=ok,loss=float(loss.detach()),grad_norm=float(norm),
                             update_norm=delta,games=len({actor_groups[i] for i in ix}),**stats))
            if not ok:
                actor.load_state_dict(previous);train_actor.load_state_dict(previous_opt)
                stopped="KL or entropy guard"
                break
            accepted+=1
        if stopped:break
    critic_logs=[]
    for epoch in range(critic_epochs):
        order=list(grouped_order(groups,rng))
        for start in range(0,len(order),critic_batch):
            ix=order[start:start+critic_batch]
            prediction=critic(x[ix]).flatten()
            w=torch.tensor([rows[i][3] for i in ix])
            loss=(nn.functional.binary_cross_entropy_with_logits(prediction,z[ix],reduction="none")*w).sum()/w.sum()
            train_critic.zero_grad();loss.backward()
            norm=nn.utils.clip_grad_norm_(critic.parameters(),1,error_if_nonfinite=True)
            train_critic.step()
            critic_logs.append(dict(loss=float(loss.detach()),grad_norm=float(norm)))
    with torch.no_grad():
        new=critic(x).flatten()
        if not torch.isfinite(new).all():raise ValueError("Nonfinite critic")
        brier=lambda v:float((v.sigmoid()-z).square().mean())
        ll=lambda v:float(nn.functional.binary_cross_entropy_with_logits(v,z))
    return dict(samples=len(rows),eligible=len(eligible),actor_attempts=len(logs),actor_updates=accepted,critic_updates=len(critic_logs),
                stopped=stopped,parity=parity,initial=first,final=statistics(),actor=logs,critic=critic_logs,
                advantage_mean=float(adv.mean()),advantage_sd=float(adv.std(unbiased=False)),
                advantage_quantiles=adv.quantile(torch.tensor([0,.1,.5,.9,1])).tolist(),advantage_normalization="none",
                critic_brier=[brier(values),brier(new)],critic_logloss=[ll(values),ll(new)]), train_actor,train_critic
