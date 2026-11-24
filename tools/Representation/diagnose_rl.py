import json,sys
from pathlib import Path
from collections import defaultdict
import numpy as np
import torch
from torch import nn
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"RL"))
from iterate import Model,PH,VH,tensors,actor_params,digest,batches

def main():
    torch.set_num_threads(2)
    root=Path("work/rl-p53")
    source=Path("work/policy-v1/policy-v1.bin")
    value=Path("work/value-v1/value-v1.bin")
    report=[]
    for folder in sorted(root.glob("round-*")):
        actor=Model();tensors(source,PH,actor_params(actor))
        critic=nn.Sequential(nn.Linear(293,128),nn.ReLU(),nn.Linear(128,128),nn.ReLU(),nn.Linear(128,1))
        tensors(value,VH,list(critic.parameters()))
        before=torch.cat([p.detach().flatten() for p in actor.parameters()])
        rows=[];labels=[];classes=[];matchups=[]
        for path in sorted(list((folder/"self").glob("[0-9]*.json"))+list((folder/"history").glob("[0-9]*.json"))):
            g=json.loads(path.read_text(encoding="utf8"))
            source_hash=digest(source)
            active=[r for r in g["rows"] if r["acting_model_hash"]==source_hash and r["behavior"] in ("policy","forced")]
            for r in active:
                n=len(r["features"])
                rows.append((np.asarray(r["x"],np.float32),np.asarray(r["features"],np.float32),np.eye(n,dtype=np.float32)[r["pick"]],1/len(active)))
                labels.append(.5 if g["winner"]<0 else float(g["winner"]==r["seat"]))
                classes.append(g["classes"][r["seat"]]);matchups.append(g["decks"][r["seat"]]+"/"+g["decks"][1-r["seat"]])
        grads=defaultdict(lambda:torch.zeros_like(before))
        advs=[];crit=[];old=[];indices=[]
        for start in range(0,len(rows),128):
            ix=list(range(start,min(start+128,len(rows))))
            x,a,y,mask,w=next(batches(rows,128,ix))
            p=(actor(x,a)/1.1).masked_fill(~mask,-1e9).log_softmax(1)
            z=torch.tensor([labels[i] for i in ix])
            pred=critic(x).detach().flatten().sigmoid()
            adv=z-pred;use=mask.sum(1)>1
            advs.extend(adv[use].tolist());crit.extend((pred-z).square().tolist())
            old.append((x,a,mask,p.detach(),use))
            term=(-(p*y).sum(1)*adv+.01*(p.exp()*p).sum(1))*w
            keys=[("class",classes),("matchup",matchups)]
            for kind,values in keys:
                for name in set(values[i] for i in ix):
                    sel=use & torch.tensor([values[i]==name for i in ix])
                    if sel.any():
                        v=torch.autograd.grad(term[sel].sum(),list(actor.parameters()),retain_graph=True)
                        grads[kind+":"+name]+=torch.cat([g.flatten() for g in v])
        def shifts():
            kl=[];ent=[];tv=[]
            with torch.no_grad():
                for x,a,mask,p,use in old:
                    q=(actor(x,a)/1.1).masked_fill(~mask,-1e9).log_softmax(1)
                    kl.extend((p.exp()*(p-q)).sum(1)[use].tolist())
                    ent.extend((-(q.exp()*q).sum(1)+ (p.exp()*p).sum(1))[use].tolist())
                    tv.extend(((p.exp()-q.exp()).abs().sum(1)/2)[use].tolist())
            return dict(kl=float(np.mean(kl)),entropy_delta=float(np.mean(ent)),tv=float(np.mean(tv)))
        total=sum(v for k,v in grads.items() if k.startswith("class:"))
        def cancellation(prefix):
            group={k:v for k,v in grads.items() if k.startswith(prefix)}
            denominator=sum(float(v.norm()) for v in group.values())
            return dict(ratio=float(total.norm())/denominator,
               alignment={k:float(v@total/(v.norm()*total.norm()).clamp_min(1e-12)) for k,v in group.items()})
        tensors(folder/"actor.bin",PH,actor_params(actor))
        after=torch.cat([p.detach().flatten() for p in actor.parameters()])
        actor_weight=sum(r[3] for r in rows if len(r[1])>1)
        config={}
        for scale in (.5,1,2,4):
            with torch.no_grad():
                pos=0
                for p in actor.parameters():
                    p.copy_((before+scale*(after-before))[pos:pos+p.numel()].reshape(p.shape));pos+=p.numel()
            config[str(scale)]=shifts()
        report.append(dict(round=folder.name,samples=len(rows),eligible=len(advs),forced=len(rows)-len(advs),
            advantage_mean=float(np.mean(advs)),advantage_variance=float(np.var(advs)),
            advantage_quantiles=np.quantile(advs,[0,.1,.25,.5,.75,.9,1]).tolist(),
            critic_brier=float(np.mean(crit)),actor_grad=float(total.norm())/actor_weight,
            update_norm=float((after-before).norm()),ppo_clipping=None,
            class_cancellation=cancellation("class:"),matchup_cancellation=cancellation("matchup:"),step_scale=config))
        source=folder/"actor.bin";value=folder/"critic.bin"
        Path("work/p531/rl-audit.json").write_text(json.dumps(report,indent=2))
        print(folder.name,config,flush=True)
if __name__=="__main__":main()

