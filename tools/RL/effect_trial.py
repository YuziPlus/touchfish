"""Equal episode budget for the engine-verified damage versus healing task."""
import json,sys,struct
from pathlib import Path
import numpy as np
import torch
from torch import nn
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"RL"))
from iterate import Model,PH,tensors,actor_params,batches,export
from ppo import optimize

def trial(entries,seed,mode,out):
    torch.manual_seed(seed)
    rng=np.random.default_rng(seed)
    actor=Model();tensors(Path("work/policy-v1/policy-v1.bin"),PH,actor_params(actor))
    projection=nn.Linear(893,32)
    with torch.no_grad():
        projection.weight.zero_();projection.weight[:,:293]=actor.state.weight;projection.bias.copy_(actor.state.bias)
    actor.state=projection
    action_projection=nn.Linear(88,32,bias=False)
    with torch.no_grad():
        action_projection.weight.zero_();action_projection.weight[:,:63]=actor.action.weight
    actor.action=action_projection
    def candidates(n):return n["a3"] if mode=="effects" else [v+[0]*25 for v in n["a"]]
    critic=nn.Sequential(nn.Linear(893,32),nn.Tanh(),nn.Linear(32,1))
    def features(n):return n["x3"] if mode=="effects" else n["x"]+[0]*600
    train=[s["tree"] for s in entries if s["seed"]<116]
    test=[s["tree"] for s in entries if s["seed"]>=116]
    def lp(n):
        return actor(torch.tensor(features(n),dtype=torch.float32)[None],torch.tensor(candidates(n),dtype=torch.float32)[None])[0].log_softmax(0)
    def score(n):
        if n["reward"] is not None:return n["reward"]
        return sum(float(p)*score(c) for p,c in zip(lp(n).exp(),n["children"]))
    @torch.no_grad()
    def measure():
        return dict(win=float(np.mean([score(n) for n in test])),
            train_win=float(np.mean([score(n) for n in train])),
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
                rows.append((np.array(features(state),np.float32),np.array(candidates(state),np.float32),np.eye(len(p),dtype=np.float32)[pick],1/len(path)))
                labels.append(node["reward"]);groups.append(game);behavior.append(p)
        report,_,_=optimize(actor,critic,rows,labels,groups,behavior,batches,seed+iteration,1,.0002,actor_batch=32,critic_batch=32)
        actor_steps+=report["actor_updates"];critic_steps+=report["critic_updates"]
        stats=report["final"]
        curve.append(dict(episodes=(iteration+1)*200,actor_steps=actor_steps,critic_steps=critic_steps,**measure(),**stats))
    if mode=="effects" and seed==713:
        export(out/"tactical-v2.bin",struct.pack("<5i",0x31504c54,1,3,88,32),actor_params(actor))
        n=test[0]
        with torch.no_grad():
            logits=actor(torch.tensor(features(n),dtype=torch.float32)[None],
                         torch.tensor(candidates(n),dtype=torch.float32)[None])[0].tolist()
        (out/"model-parity.json").write_text(json.dumps(dict(x=features(n),a=candidates(n),logits=logits)))
    return curve

if __name__=="__main__":
    torch.set_num_threads(1)
    rows=json.loads(Path("work/p533/scenarios.json").read_text(encoding="utf8"))["scenarios"]
    out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
    result=[]
    for family in ("effects","effect_draw"):
        for seed in (713,919,1181):
            for mode in ("old","effects"):
                import time
                start=time.perf_counter()
                curve=trial([r for r in rows if r["family"]==family],seed,mode,out)
                result.append(dict(family=family,seed=seed,mode=mode,curve=curve,seconds=time.perf_counter()-start))
                (out/"short.json").write_text(json.dumps(result,indent=2))
                print(family,seed,mode,curve[-1]["win"],flush=True)



