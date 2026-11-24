import argparse, hashlib, json, math, struct, sys
from pathlib import Path
import numpy as np
import torch
from torch import nn

TOOLS=Path(__file__).resolve().parents[1]
sys.path[:0]=[str(TOOLS/"Value"),str(TOOLS/"SelfPlay")]
from train import encode
from read_dataset import iter_games

KINDS=("play","attack","power","end","decision","mulligan")

def action_vector(a):
    x=np.zeros(63,np.float32)
    x[KINDS.index(a["kind"])]=1
    for key,offset,size in (("source_slot",6,10),("target_slot",16,7),("position",23,8),("option_index",31,5)):
        i=a[key]
        if i is not None and 0<=i<size: x[offset+i]=1
    for key,offset,first in (("target_side",36,"self"),("target_zone",38,"hero")):
        if a[key] is not None:x[offset+int(a[key]!=first)]=1
    for key,offset in (("source_card",40),("choice",48)):
        if a[key] is not None:
            h=2166136261
            raw=a[key].encode("utf-16-le")
            for i in range(0,len(raw),2):
                h=((h^(raw[i]+256*raw[i+1]))*16777619)&0xffffffff
            x[offset+h%8]=1
    if a["replace_slots"] is not None:
        for i in a["replace_slots"]:
            if i<6:x[56+i]=1
        x[62]=len(a["replace_slots"])/10
    return x

class Model(nn.Module):
    def __init__(self):
        super().__init__()
        self.state=nn.Linear(293,32)
        self.action=nn.Linear(63,32,bias=False)
        self.output=nn.Linear(32,1,bias=False)

    def forward(self,x,a):
        return self.output((self.state(x)[:,None,:]+self.action(a)).relu()).squeeze(-1)

def load(path,ood=False):
    index=path/"dataset-index.json"
    roots=[(path/r["manifest"]).parent for r in json.loads(index.read_text())["manifests"]] if index.exists() else [path]
    groups={name:[] for name in ("train","validation","test","ood")}
    hashes={}
    fixtures=[]
    for root in roots:
        manifest=root/"manifest.json"
        hashes[str(manifest)]=hashlib.sha256(manifest.read_bytes()).hexdigest()
        for game,rows in iter_games(root):
            bucket=int.from_bytes(hashlib.sha256(str(game["Seed"]).encode()).digest()[:8],"big")%100
            split="ood" if ood else "train" if bucket<80 else "validation" if bucket<90 else "test"
            selected=[]
            for row in rows:
                acts=row["legal_actions"]
                visits=np.array([a["visits"] or 0 for a in acts],np.float32)
                if row["search"]["fallback_reason"] or not visits.sum() or len(acts)<2:continue
                vector=encode(row["observation"])
                features=np.stack([action_vector(a) for a in acts])
                selected.append((vector,features,visits/visits.sum()))
                if len(fixtures)<24: fixtures.append((row["observation"],acts,vector,features))
            if selected:
                groups[split].extend((*r,1/len(selected)) for r in selected)
    return groups,hashes,fixtures

def batches(rows,batch,order):
    for start in range(0,len(order),batch):
        items=[rows[i] for i in order[start:start+batch]]
        n=max(len(r[1]) for r in items)
        x=np.stack([r[0] for r in items])
        a=np.zeros((len(items),n,items[0][1].shape[1]),np.float32)
        target=np.zeros((len(items),n),np.float32)
        mask=np.zeros((len(items),n),bool)
        w=np.array([r[3] for r in items],np.float32)
        for i,(_,feat,y,_) in enumerate(items):
            a[i,:len(y)]=feat;target[i,:len(y)]=y;mask[i,:len(y)]=True
        yield tuple(torch.from_numpy(v) for v in (x,a,target,mask,w))

def measure(model,rows,batch):
    sums=np.zeros(7)
    with torch.no_grad():
        for x,a,y,mask,w in batches(rows,batch,range(len(rows))):
            p=model(x,a).masked_fill(~mask,-1e9).log_softmax(1)
            count=mask.sum(1).float()
            vals=[-(p*y).sum(1),count.log(),-(p.exp()*p).sum(1),-(y*y.clamp_min(1e-12).log()).sum(1),
                  (p.argmax(1)==y.argmax(1)).float(),w*0+1,count]
            sums+=np.array([(v*w).sum().item() for v in vals])
    return dict(zip(("ce","uniform_ce","entropy","target_entropy","top1","weight","mean_candidates"),[*(sums[:5]/sums[5]),sums[5],sums[6]/sums[5]]))

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("data",type=Path);ap.add_argument("out",type=Path);ap.add_argument("--ood",type=Path)
    ap.add_argument("--epochs",type=int,default=14);ap.add_argument("--batch",type=int,default=256);ap.add_argument("--seed",type=int,default=5241)
    ap.add_argument("--evaluate",action="store_true")
    args=ap.parse_args()
    torch.set_num_threads(3);torch.manual_seed(args.seed)
    rng=np.random.default_rng(args.seed)
    groups,hashes,fixtures=load(args.data)
    if args.ood:
        extra,oh,_=load(args.ood,ood=True);groups["ood"]=extra["ood"];hashes.update(oh)
    model=Model()
    optimizer=torch.optim.AdamW(model.parameters(),lr=.002)
    args.out.mkdir(parents=True,exist_ok=True)
    history=[];best=math.inf
    train=groups["train"]
    for i in range(0 if args.evaluate else args.epochs):
        model.train()
        for x,a,y,mask,w in batches(train,args.batch,rng.permutation(len(train))):
            logp=model(x,a).masked_fill(~mask,-1e9).log_softmax(1)
            loss=(-(logp*y).sum(1)*w).sum()/w.sum()
            optimizer.zero_grad();loss.backward();optimizer.step()
        model.eval()
        score=measure(model,groups["validation"],args.batch)
        history.append(score)
        print(i+1,score["ce"],flush=True)
        if score["ce"]<best:
            best=score["ce"];torch.save(model.state_dict(),args.out/"best.pt")
    model.load_state_dict(torch.load(args.out/"best.pt",weights_only=True));model.eval()
    if args.evaluate:
        report_path=args.out/"metrics.json"
        report=json.loads(report_path.read_text(encoding="utf8"))
        report["hashes"].update(hashes)
        report["rows"].update({k:len(v) for k,v in groups.items()})
        report["metrics"].update({k:measure(model,v,args.batch) for k,v in groups.items() if v})
        report_path.write_text(json.dumps(report,indent=2),encoding="utf8")
        return
    with (args.out/"policy-v1.bin").open("wb") as f:
        f.write(struct.pack("<5i",0x31504c54,1,1,63,32))
        for tensor in (model.state.weight,model.action.weight,model.state.bias,model.output.weight):
            f.write(tensor.detach().numpy().astype("<f4").tobytes())
    with torch.no_grad():
        parity=[dict(observation=o,actions=acts,features=v.tolist(),action_features=a.tolist(),
                     logits=model(torch.from_numpy(v[None]),torch.from_numpy(a[None]))[0].tolist()) for o,acts,v,a in fixtures]
    (args.out/"parity.json").write_text(json.dumps(parity),encoding="utf8")
    report=dict(seed=args.seed,feature_schema=1,action_schema=1,hashes=hashes,history=history,
                rows={k:len(v) for k,v in groups.items()},
                metrics={k:measure(model,v,args.batch) for k,v in groups.items() if v})
    (args.out/"metrics.json").write_text(json.dumps(report,indent=2),encoding="utf8")

if __name__=="__main__":main()

