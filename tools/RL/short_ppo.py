"""Equal episode budget on the P5.3.1 engine-verified trees."""
import json,sys
from pathlib import Path
import numpy as np
import torch
from torch import nn
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"RL"))
from iterate import Model,PH,tensors,actor_params,batches
from ppo import optimize

def trial(entries,seed,mode):
    torch.manual_seed(seed)
    rng=np.random.default_rng(seed)
    actor=Model();tensors(Path("work/policy-v1/policy-v1.bin"),PH,actor_params(actor))
    critic=nn.Sequential(nn.Linear(293,32),nn.Tanh(),nn.Linear(32,1))
    train=[s["tree"] for s in entries if s["seed"]<116]
    test=[s["tree"] for s in entries if s["seed"]>=116]
    def lp(n):
        return actor(torch.tensor(n["x"],dtype=torch.float32)[None],torch.tensor(n["a"],dtype=torch.float32)[None])[0].log_softmax(0)
    def score(n):
        if n["reward"] is not None:return n["reward"]
        return sum(float(p)*score(c) for p,c in zip(lp(n).exp(),n["children"]))
    @torch.no_grad()
    def measure():
        return dict(win=float(np.mean([score(n) for n in test])),
            high=float(np.mean([sum(float(p) for p,c in zip(lp(n).exp(),n["children"]) if c["best"]==n["best"]) for n in test])))
    curve=[dict(episodes=0,**measure())]
    actor_steps=critic_steps=0
    for iteration in range(6):
        rows=[];labels=[];groups=[];behavior=[]
        for game in range(200):
            node=train[rng.integers(len(train))]
            path=[]
            while node["reward"] is None:
                with torch.no_grad():p=lp(node).double().softmax(0).numpy()
                pick=rng.choice(len(p),p=p)
                path.append((node,p,pick))
                node=node["children"][pick]
            for state,p,pick in path:
                rows.append((np.array(state["x"],np.float32),np.array(state["a"],np.float32),np.eye(len(p),dtype=np.float32)[pick],1/len(path)))
                labels.append(node["reward"]);groups.append(game);behavior.append(p)
        if mode=="ppo":
            report,_,_=optimize(actor,critic,rows,labels,groups,behavior,batches,seed+iteration,1,.0002,actor_batch=32,critic_batch=32)
            actor_steps+=report["actor_updates"];critic_steps+=report["critic_updates"]
            stats=report["final"]
        else:
            x=torch.tensor(np.stack([r[0] for r in rows]))
            z=torch.tensor(labels,dtype=torch.float32)
            opt=torch.optim.AdamW(list(actor.parameters())+list(critic.parameters()),lr=.0002)
            use_weight=sum(r[3] for r in rows if len(r[1])>1)
            all_weight=sum(r[3] for r in rows)
            opt.zero_grad()
            for start in range(0,len(rows),128):
                ix=list(range(start,min(start+128,len(rows))))
                bx,ba,by,mask,w=next(batches(rows,128,ix))
                p=actor(bx,ba).masked_fill(~mask,-1e9).log_softmax(1)
                v=critic(x[ix]).flatten()
                use=mask.sum(1)>1
                adv=z[ix]-v.detach().sigmoid()
                loss=(-((p*by).sum(1)*adv-.01*(p.exp()*p).sum(1))[use]*w[use]).sum()/use_weight
                loss+=(nn.functional.binary_cross_entropy_with_logits(v,z[ix],reduction="none")*w).sum()/all_weight
                loss.backward()
            nn.utils.clip_grad_norm_(list(actor.parameters())+list(critic.parameters()),1,error_if_nonfinite=True)
            opt.step();actor_steps+=1;critic_steps+=1
            kl=[];entropy=[]
            with torch.no_grad():
                for start in range(0,len(rows),128):
                    ix=list(range(start,min(start+128,len(rows))))
                    bx,ba,_,mask,_=next(batches(rows,128,ix))
                    q=actor(bx,ba).masked_fill(~mask,-1e9).log_softmax(1)
                    for j,i in enumerate(ix):
                        old=torch.tensor(behavior[i])
                        if len(old)>1:
                            current=q[j,:len(old)]
                            kl.append(float((old*(old.log()-current)).sum()))
                            entropy.append(float(-(current.exp()*current).sum()))
            stats=dict(kl=float(np.mean(kl)),entropy=float(np.mean(entropy)))
        curve.append(dict(episodes=(iteration+1)*200,actor_steps=actor_steps,critic_steps=critic_steps,**measure(),**stats))
    return curve

if __name__=="__main__":
    torch.set_num_threads(1)
    rows=json.loads(Path("work/p531/scenarios.json").read_text(encoding="utf8"))["scenarios"]
    out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
    result=[]
    for family in sorted({r["family"] for r in rows}):
        for seed in (713,919,1181):
            for mode in ("legacy","ppo"):
                curve=trial([r for r in rows if r["family"]==family],seed,mode)
                result.append(dict(family=family,seed=seed,mode=mode,curve=curve))
                (out/"short.json").write_text(json.dumps(result,indent=2))
                print(family,seed,mode,curve[-1]["win"],flush=True)

