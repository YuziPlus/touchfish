import argparse, hashlib, json, math, struct, sys
from collections import Counter
from pathlib import Path

import numpy as np
import torch
from torch import nn
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "SelfPlay"))
from read_dataset import iter_games

def encode(o):
    def scaled(obj, spec):
        return [obj[key] / scale for key, scale in spec]
    x = [o["Turn"]/40, o["ActiveSeat"] == o["Seat"], o["FirstSeat"] == o["Seat"],
         o["SpellPower"]/5, o["CardsPlayed"]/10, o["DeathsThisTurn"]/10,
         o["PreparationDiscount"]/5, len(o["Decision"]["Options"])/5 if o["Decision"] else 0, o["MulliganComplete"]]
    for p in (o["Self"], o["Enemy"]):
        x += scaled(p, zip("Health Armor Mana MaxMana DeckCount HandCount HeroAttack WeaponAttack WeaponDurability Fatigue SecretCount LockedMana Overload Frozen Immune PowerUsed".split(),
                           (30,30,10,10,30,10,10,10,5,10,5,10,10,1,1,1)))
        for u in p["Board"][:7]:
            x += [1] + scaled(u, zip("Attack Health MaxHealth Cost AttacksRemaining Frozen Shield Taunt Stealth Poison Windfury SpellDamage".split(),
                                     (12,12,12,10,2,1,1,1,1,1,1,5)))
        x += [0]*13*max(0,7-len(p["Board"]))
    for c in o["Hand"][:10]:
        x += [1,c["Cost"]/10,c["BaseCost"]/10,c["Type"]=="MINION",c["Type"]=="SPELL",c["Charge"],c["Secret"]]
    x += [0]*7*max(0,10-len(o["Hand"]))
    return np.clip(np.asarray(x,dtype=np.float32),-4,4)

def heuristic(o):
    def strength(p):
        return (.35*min(30,p["Health"]+p["Armor"])+.85*p["HandCount"]+.025*p["DeckCount"]+.24*p["MaxMana"]+
                .05*p["Mana"]+.4*p["WeaponAttack"]*min(2,p["WeaponDurability"])+.55*p["SecretCount"]+
                sum(.6+u["Attack"]*(.25 if u["Frozen"] else .65)+.45*u["Health"]+.9*u["Shield"]+
                    .65*u["Taunt"]+.7*u["Poison"]+.4*u["Windfury"] for u in p["Board"]))
    a,b=o["Self"],o["Enemy"]
    def threatened(p,q):
        return p["Health"]+p["Armor"]<=sum(u["Attack"] for u in q["Board"] if not u["Frozen"]) and not any(u["Taunt"] for u in p["Board"])
    return .5+.48*math.tanh((strength(a)-strength(b)-2*threatened(a,b)+2*threatened(b,a))/13)

def data(path):
    index=path/"dataset-index.json"
    roots=[(path/r["manifest"]).parent for r in json.loads(index.read_text())["manifests"]] if index.exists() else [path]
    rows, games, hashes, seen, fixtures = [], [], {}, set(), []
    for root in roots:
        manifest=root/"manifest.json"
        hashes[str(manifest)]=hashlib.sha256(manifest.read_bytes()).hexdigest()
        for g, samples in iter_games(root):
            gid=root.name+"/"+g["GameId"]
            if gid in seen or g["Sha256"] in seen: raise ValueError("Duplicate game")
            seen.update((gid,g["Sha256"]))
            bucket=int.from_bytes(hashlib.sha256(str(g["Seed"]).encode()).digest()[:8],"big")%100
            split="train" if bucket<80 else "validation" if bucket<90 else "test"
            games.append(dict(game_id=gid, seed=g["Seed"], split=split, samples=len(samples), matchup=g["Decks"]))
            for r in samples:
                o=r["observation"]
                rows.append((encode(o),(r["z"]+1)/2,heuristic(o),1/len(samples),split,r["player_class"],
                             "/".join(g["Decks"]), "early" if r["turn"]<=8 else "mid" if r["turn"]<=18 else "late"))
                if len(fixtures)<32 and r["decision_index"]%3==0: fixtures.append(o)
    x,y,h,w,s,c,m,t=zip(*rows)
    return np.stack(x),np.array(y,np.float32),np.array(h),np.array(w,np.float32),np.array(s),np.array(c),np.array(m),np.array(t),games,hashes,fixtures

def metrics(y,p,w):
    p=np.clip(p,1e-7,1-1e-7)
    loss=-(y*np.log(p)+(1-y)*np.log1p(-p))
    bins=[]
    for i in range(10):
        keep=(p>=i/10)&(p<(i+1)/10)
        if keep.any(): bins.append(dict(bin=i,n=int(keep.sum()),mean=float(np.average(p[keep],weights=w[keep])),actual=float(np.average(y[keep],weights=w[keep]))))
    order=np.argsort(p,kind="stable")
    scores=p[order]
    starts=np.r_[0,np.flatnonzero(np.diff(scores))+1]
    positive=np.add.reduceat((w*y)[order],starts).astype(np.float64)
    negative=np.add.reduceat((w*(1-y))[order],starts).astype(np.float64)
    pairs=positive.sum()*negative.sum()
    auc=float(positive@(np.cumsum(negative)-.5*negative)/pairs) if pairs else None
    return dict(n=len(y),brier=float(np.average((p-y)**2,weights=w)),logloss=float(np.average(loss,weights=w)),auc=auc,calibration=bins)

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("data",type=Path);ap.add_argument("out",type=Path)
    ap.add_argument("--ood",type=Path);ap.add_argument("--seed",type=int,default=9051)
    ap.add_argument("--batch",type=int,default=512);ap.add_argument("--epochs",type=int,default=30)
    ap.add_argument("--lr",type=float,default=.001);ap.add_argument("--device",default="auto")
    ap.add_argument("--resume",action="store_true");ap.add_argument("--evaluate",action="store_true")
    a=ap.parse_args()
    a.out.mkdir(parents=True,exist_ok=True)
    torch.set_num_threads(2); torch.manual_seed(a.seed)
    device=torch.device(("cuda" if torch.cuda.is_available() else "cpu") if a.device=="auto" else a.device)
    x,y,h,w,s,c,m,t,games,hashes,fixtures=data(a.data)
    train=np.flatnonzero(s=="train"); valid=np.flatnonzero(s=="validation")
    X,Y,W=[torch.from_numpy(v).to(device) for v in (x,y,w)]
    model=nn.Sequential(nn.Linear(293,128),nn.ReLU(),nn.Linear(128,128),nn.ReLU(),nn.Linear(128,1)).to(device)
    opt=torch.optim.AdamW(model.parameters(),lr=a.lr)
    lossfn=nn.BCEWithLogitsLoss(reduction="none")
    rng=np.random.default_rng(a.seed)
    def loss(ix):
        ix=torch.as_tensor(ix,device=device)
        return (lossfn(model(X[ix]).flatten(),Y[ix])*W[ix]).sum()/W[ix].sum()
    identity=dict(schema=1,features=293,hidden=[128,128],seed=a.seed,batch=a.batch,lr=a.lr,hashes=hashes,device=str(device),torch=torch.__version__)
    best=float("inf"); start=0; history=[]
    checkpoint=a.out/"checkpoint.pt"
    if a.resume or a.evaluate:
        state=torch.load(checkpoint,map_location=device,weights_only=False)
        if state["identity"]!=identity: raise ValueError("Checkpoint configuration mismatch")
        model.load_state_dict(state["model"]);opt.load_state_dict(state["optimizer"])
        start=state["epoch"];best=state["best"];history=state["history"];rng.bit_generator.state=state["rng"]
    if not a.evaluate:
        for epoch in range(start,a.epochs):
            model.train(); order=rng.permutation(train)
            for ix in np.array_split(order,math.ceil(len(order)/a.batch)):
                opt.zero_grad(); l=loss(ix);l.backward();nn.utils.clip_grad_norm_(model.parameters(),5);opt.step()
            model.eval()
            with torch.no_grad(): tl,vl=float(loss(train)),float(loss(valid))
            history.append([epoch+1,tl,vl]);print(history[-1],flush=True)
            if vl<best:
                best=vl;torch.save(model.state_dict(),a.out/"best.pt")
            temp=checkpoint.with_suffix(".tmp")
            torch.save(dict(identity=identity,model=model.state_dict(),optimizer=opt.state_dict(),epoch=epoch+1,best=best,history=history,rng=rng.bit_generator.state),temp)
            temp.replace(checkpoint)
    model.load_state_dict(torch.load(a.out/"best.pt",map_location=device,weights_only=True));model.eval()
    baseline=float(np.average(y[train],weights=w[train]))
    # Fit only on training games. The heuristic is a bounded score rather than a calibrated probability.
    ht=torch.tensor(np.log(h/(1-h)),dtype=torch.float64)
    calibration=torch.zeros(2,dtype=torch.float64,requires_grad=True)
    optimizer=torch.optim.LBFGS([calibration],max_iter=30,line_search_fn="strong_wolfe")
    def closure():
        optimizer.zero_grad()
        v=(nn.functional.binary_cross_entropy_with_logits(ht[train]*calibration[0]+calibration[1],torch.tensor(y[train],dtype=torch.float64),reduction="none")*torch.tensor(w[train])).sum()/w[train].sum()
        v.backward();return v
    optimizer.step(closure)
    slope,intercept=calibration.detach().tolist()
    def evaluate(x,y,h,w,c,m,t):
        with torch.no_grad(): p=model(torch.from_numpy(x).to(device)).sigmoid().flatten().cpu().numpy()
        calibrated=1/(1+np.exp(-(slope*np.log(h/(1-h))+intercept)))
        preds={"constant":np.full(len(y),baseline),"heuristic_raw":h,"heuristic_calibrated":calibrated,"neural":p}
        group=lambda keep:{k:metrics(y[keep],v[keep],w[keep]) for k,v in preds.items()}
        return dict(overall=group(slice(None)),groups={key:{label:group(values==label) for label in sorted(set(values))} for key,values in (("class",c),("matchup",m),("phase",t))})
    keep=s=="test"
    report=dict(identity=identity,history=history,split_games=dict(Counter(g["split"] for g in games)),split_samples=dict(Counter(s)),test=evaluate(x[keep],y[keep],h[keep],w[keep],c[keep],m[keep],t[keep]))
    if a.ood:
        ox,oy,oh,ow,_,oc,om,ot,og,ohash,_=data(a.ood)
        report["ood"]=dict(games=og,hashes=ohash,metrics=evaluate(ox,oy,oh,ow,oc,om,ot))
    with (a.out/"value-v1.bin").open("wb") as f:
        f.write(struct.pack("<4i",0x31564c54,1,293,128))
        for layer in (model[0],model[2],model[4]):
            for tensor in (layer.weight,layer.bias): f.write(tensor.detach().cpu().numpy().astype("<f4").tobytes())
    with torch.no_grad():
        encoded=np.stack([encode(o) for o in fixtures])
        p=model(torch.from_numpy(encoded).to(device)).sigmoid().flatten().cpu().tolist()
    (a.out/"parity.json").write_text(json.dumps([dict(observation=o,features=x.tolist(),prediction=v,heuristic=heuristic(o)) for o,x,v in zip(fixtures,encoded,p)]),encoding="utf-8")
    (a.out/"splits.json").write_text(json.dumps(games,indent=2),encoding="utf-8")
    (a.out/"metrics.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print({k:(v["brier"],v["logloss"]) for k,v in report["test"]["overall"].items()})

if __name__=="__main__":
    main()
