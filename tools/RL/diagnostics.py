import json,sys
from pathlib import Path
from collections import Counter,defaultdict
import numpy as np
import torch
from torch import nn
from iterate import tensors,VH

root=Path(sys.argv[1])
torch.set_num_threads(2)
def critic(path):
    net=nn.Sequential(nn.Linear(293,128),nn.ReLU(),nn.Linear(128,128),nn.ReLU(),nn.Linear(128,1))
    tensors(path,VH,list(net.parameters()))
    return net
def metrics(x,z,net):
    with torch.no_grad():p=net(torch.tensor(x,dtype=torch.float32)).flatten().sigmoid().numpy().clip(1e-7,1-1e-7)
    z=np.asarray(z)
    bins=np.minimum(9,(p*10).astype(int))
    ece=sum(abs((p-z)[bins==i].sum()) for i in range(10))/len(z)
    return dict(n=len(z),brier=float(((p-z)**2).mean()),logloss=float(-(z*np.log(p)+(1-z)*np.log1p(-p)).mean()),
                ece=float(ece),extreme_errors=int(((p>.95)&(z==0)|(p<.05)&(z==1)).sum()))
summary=[]
previous=Path("work/value-v1/value-v1.bin")
for folder in sorted(root.glob("round-*")):
    if not (folder/"evaluation.json").exists():continue
    groups=defaultdict(lambda:([],[])); counts=Counter()
    for file in (folder/"ood/before").glob("[0-9]*.json"):
        g=json.loads(file.read_text(encoding="utf8"))
        for row in g["rows"]:
            for key in ("overall","class:"+g["classes"][row["seat"]],"matchup:"+"/".join(g["decks"])):
                x,z=groups[key];x.append(row["x"]);z.append(.5 if g["winner"]<0 else float(g["winner"]==row["seat"]))
    for name in ("self","history"):
        for file in (folder/name).glob("[0-9]*.json"):
            g=json.loads(file.read_text(encoding="utf8"))
            counts.update(r["kind"] for r in g["rows"] if r["eligible"])
    old,new=critic(previous),critic(folder/"critic.bin")
    previous=folder/"critic.bin"
    summary.append(dict(round=folder.name,action_counts=dict(counts),
                        critic={k:dict(before=metrics(x,z,old),after=metrics(x,z,new)) for k,(x,z) in groups.items()}))
(root/"diagnostics.json").write_text(json.dumps(summary,indent=2))
