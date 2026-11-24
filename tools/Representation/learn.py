import argparse,json,sys
from pathlib import Path
import numpy as np
import torch
from torch import nn
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"RL"))
from iterate import Model,PH,tensors,actor_params,export
import struct

def run(scenarios,family,seed,initial,version,control,out):
    torch.manual_seed(seed)
    rng=np.random.default_rng(seed)
    actor=Model()
    if initial:tensors(initial,PH,actor_params(actor))
    field="x2" if version else "x"
    width=735 if version else 293
    if version:
        layer=nn.Linear(width,32)
        with torch.no_grad():
            layer.weight.zero_()
            layer.weight[:,:293]=actor.state.weight
            layer.bias.copy_(actor.state.bias)
        actor.state=layer
    critic=nn.Sequential(nn.Linear(width,32),nn.Tanh(),nn.Linear(32,1))
    opt=torch.optim.AdamW(list(actor.parameters())+list(critic.parameters()),lr=.002)
    baseline=torch.cat([p.detach().flatten() for p in actor.parameters()])
    def encode(n):
        if n["reward"] is not None:return
        v=n[field]
        if control:v=v[:293]+[0]*(width-293)
        n["xt"]=torch.tensor(v,dtype=torch.float32)
        n["at"]=torch.tensor(n["a"],dtype=torch.float32)
        for child in n["children"]:encode(child)
    entries=[s for s in scenarios if s["family"]==family]
    train=[s["tree"] for s in entries if s["seed"]<116]
    test=[s["tree"] for s in entries if s["seed"]>=116]
    for node in train+test:encode(node)
    def logp(n):return actor(n["xt"][None],n["at"][None])[0].log_softmax(0)
    def exact(n):
        if n["reward"] is not None:return n["reward"]
        return sum(float(p)*exact(c) for p,c in zip(logp(n).exp(),n["children"]))
    def eval_nodes(nodes):
        with torch.no_grad():
            win=np.mean([exact(n) for n in nodes])
            good=np.mean([sum(float(p) for p,c in zip(logp(n).exp(),n["children"]) if c["best"]==n["best"]) for n in nodes])
        return dict(win=float(win),optimal_first=float(good))
    before={"train":eval_nodes(train),"heldout":eval_nodes(test)}
    with torch.no_grad():old=[logp(n).clone() for n in test]
    curve=[]
    for step in range(48):
        terms=[];values=[];advances=[];entropies=[];wins=[]
        for _ in range(24):
            n=train[rng.integers(len(train))]
            path=[]
            while n["reward"] is None:
                lp=logp(n)
                pick=int(torch.multinomial(lp.detach().exp(),1))
                value=critic(n["xt"]).squeeze()
                path.append((lp[pick],value,-(lp*lp.exp()).sum(),len(lp)>1))
                n=n["children"][pick]
            z=n["reward"];wins.append(z)
            for lp,value,ent,multiple in path:
                reward=value.new_tensor(z)
                adv=reward-value.detach().sigmoid()
                if multiple:terms.append(-lp*adv-.01*ent);advances.append(float(adv));entropies.append(float(ent.detach()))
                values.append(nn.functional.binary_cross_entropy_with_logits(value,reward))
        al=torch.stack(terms).mean();cl=torch.stack(values).mean()
        opt.zero_grad();(al+cl).backward();opt.step()
        if step%8==7:
            curve.append(dict(episodes=(step+1)*24,actor=float(al.detach()),critic=float(cl.detach()),sample_win=float(np.mean(wins)),
                advantage=np.quantile(advances,[0,.25,.5,.75,1]).tolist(),entropy=float(np.mean(entropies)),heldout=eval_nodes(test)))
    with torch.no_grad():
        kl=np.mean([float((lp.exp()*(lp-logp(n))).sum()) for lp,n in zip(old,test)])
        if version and not control and initial and seed==713:
            export(out/"tactical-v2.bin",struct.pack("<5i",0x31504c54,1,2,63,32),actor_params(actor))
            n=test[0]
            scores=actor(n["xt"][None],n["at"][None])[0].tolist()
            (out/"model-parity.json").write_text(json.dumps(dict(x=n[field],a=n["a"],logits=scores)))
    return dict(family=family,seed=seed,init="pretrained" if initial else "random",schema=2 if version else 1,control=control,
        before=before,after={"train":eval_nodes(train),"heldout":eval_nodes(test)},kl=float(kl),
        update_norm=float((torch.cat([p.detach().flatten() for p in actor.parameters()])-baseline).norm()),curve=curve)

def main():
    p=argparse.ArgumentParser();p.add_argument("directory",type=Path);p.add_argument("--check-features",action="store_true");args=p.parse_args()
    torch.set_num_threads(1)
    data=json.loads((args.directory/"scenarios.json").read_text(encoding="utf8"))
    tags="BATTLECRY DEATHRATTLE COMBO TAUNT DIVINE_SHIELD CHARGE STEALTH WINDFURY FREEZE POISONOUS SECRET OVERLOAD ENRAGE SILENCE".split()
    for fixture in data["audit"]["parity"]:
        x=list(fixture["baseline"])
        def append(card):
            c=fixture["cards"][card["CardId"]] if card else {}
            x.extend(c.get(k,0)/s for k,s in (("Attack",12),("Health",12),("SpellDamage",5),("Durability",5)))
            x.extend(t in (c.get("Mechanics") or []) for t in tags)
        for i in range(10):
            hand=fixture["Hand"]
            append(hand[i] if i<len(hand) else None)
            x.append(hand[i]["UndiscountedCost"]/10 if i<len(hand) else 0)
        for side in ("Self","Enemy"):
            board=fixture[side]["Board"]
            for i in range(7):append(board[i] if i<len(board) else None)
        np.testing.assert_allclose(x,fixture["enhanced"],rtol=0,atol=1e-6)
    if args.check_features:
        print("PASS Python/C# schema 2 feature parity")
        return
    scenarios=data["scenarios"]
    result=[]
    for family in sorted({s["family"] for s in scenarios}):
        for init in (None,Path("work/policy-v1/policy-v1.bin")):
            for seed in (713,919,1181):
                for version,control in ((0,False),(1,True),(1,False)):
                    r=run(scenarios,family,seed,init,version,control,args.directory);result.append(r)
                    print(family,r["init"],seed,r["schema"],control,r["before"]["heldout"],r["after"]["heldout"],flush=True)
                    (args.directory/"short-learning.json").write_text(json.dumps(result,indent=2))
if __name__=="__main__":main()
