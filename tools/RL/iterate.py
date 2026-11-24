import argparse, hashlib, json, math, struct, subprocess, sys, time
from collections import Counter
from pathlib import Path
import numpy as np
import torch
from torch import nn
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"Policy"))
import importlib.util
spec=importlib.util.spec_from_file_location("policy_training",Path(sys.path[0])/"train.py")
policy_training=importlib.util.module_from_spec(spec)
spec.loader.exec_module(policy_training)
Model,batches=policy_training.Model,policy_training.batches

def put(path,data):
    tmp=path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data,indent=2),encoding="utf8")
    tmp.replace(path)

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def tensors(path,header,params):
    raw=path.read_bytes()
    if raw[:len(header)]!=header: raise ValueError("Model schema mismatch")
    data=np.frombuffer(raw[len(header):],dtype="<f4")
    i=0
    with torch.no_grad():
        for p in params:
            p.copy_(torch.tensor(data[i:i+p.numel()].copy()).reshape(p.shape));i+=p.numel()
    if i!=len(data):raise ValueError("Model size mismatch")

def export(path,header,params):
    path.write_bytes(header+b"".join(p.detach().numpy().astype("<f4").tobytes() for p in params))

PH=struct.pack("<5i",0x31504c54,1,1,63,32)
VH=struct.pack("<4i",0x31564c54,1,293,128)
def actor_params(m):return [m.state.weight,m.action.weight,m.state.bias,m.output.weight]

def update(data,actor_path,critic_path,out,seed,temperature,lr,ppo=None):
    torch.manual_seed(seed)
    actor=Model()
    critic=nn.Sequential(nn.Linear(293,128),nn.ReLU(),nn.Linear(128,128),nn.ReLU(),nn.Linear(128,1))
    tensors(actor_path,PH,actor_params(actor));tensors(critic_path,VH,list(critic.parameters()))
    opt=torch.optim.AdamW(list(actor.parameters())+list(critic.parameters()),lr=lr)
    rows=[];labels=[];behavior=[];groups=[];kinds=Counter()
    model_hash=digest(actor_path)
    for game_id,game in enumerate(data):
        if ppo is not None and (game.get("schema"),game.get("features"),game.get("actions"))!=(1,1,1):
            raise ValueError(f"Incompatible rollout schema in game {game_id}")
        selected=[r for r in game["rows"] if r["acting_model_hash"]==model_hash and r["behavior"] in ("policy","forced")]
        for r in selected:
            p=r["probs"]; k=r["pick"]
            if not (r["seat"] in (0,1) and game["winner"] in (-1,0,1) and p and
                    0<=k<len(r["features"]) and len(p)==len(r["features"]) and abs(sum(p)-1)<1e-10 and
                    all(v>=0 and math.isfinite(v) for v in p) and p[k]>0 and abs(math.log(p[k])-r["logprob"])<1e-10 and
                    r["temperature"]==temperature and r["eligible"]==(len(p)>1) and
                    (r["behavior"]=="policy")==r["eligible"]):
                raise ValueError(f"Invalid behavior receipt in game {game_id}")
            z=.5 if game["winner"]<0 else float(game["winner"]==r["seat"])
            kinds[r["kind"]]+=1
            rows.append((np.array(r["x"],np.float32),np.array(r["features"],np.float32),np.eye(len(p),dtype=np.float32)[k],1/len(selected)))
            labels.append(z);behavior.append(p)
            groups.append(game_id)
    if not rows or not any(len(p)>1 for p in behavior):raise ValueError("No eligible samples")
    if ppo is not None:
        from ppo import optimize
        started=time.perf_counter()
        report,actor_opt,critic_opt=optimize(actor,critic,rows,labels,groups,behavior,batches,seed,temperature,lr,**ppo)
        export(out/"actor.bin",PH,actor_params(actor));export(out/"critic.bin",VH,list(critic.parameters()))
        torch.save(dict(actor=actor.state_dict(),critic=critic.state_dict(),actor_optimizer=actor_opt.state_dict(),
                        critic_optimizer=critic_opt.state_dict(),source=model_hash,seed=seed,config=ppo),out/"checkpoint.pt")
        put(out/"training.json",dict(report,actor_before=model_hash,actor_after=digest(out/"actor.bin"),
            critic_before=digest(critic_path),critic_after=digest(out/"critic.bin"),config=ppo,seed=seed,
            temperature=temperature,lr=lr,seconds=time.perf_counter()-started,action_counts=kinds))
        return
    def policy(x,a,mask):return (actor(x,a)/temperature).masked_fill(~mask,-1e9).log_softmax(1)
    fixtures=list(range(min(64,len(rows))))
    with torch.no_grad():
        old=[policy(x,a,m).exp().numpy() for x,a,_,m,_ in batches(rows,64,fixtures)][0]
        before=[v for x,a,_,m,_ in batches(rows,128,range(len(rows))) for v in policy(x,a,m).exp().numpy()]
        error=max(np.max(np.abs(v[:len(p)]-p)) for v,p in zip(before,behavior))
        if error>2e-6:raise ValueError(f"Behavior probability mismatch {error}")
    started=time.perf_counter()
    logs=[]
    order=np.random.default_rng(seed).permutation(len(rows))
    actor_weight=sum(row[3] for row,p in zip(rows,behavior) if len(p)>1)
    critic_weight=sum(row[3] for row in rows)
    opt.zero_grad()
    for start in range(0,len(order),128):
        ix=order[start:start+128]
        x,a,chosen,mask,w=next(batches(rows,128,ix))
        z=torch.tensor([labels[i] for i in ix],dtype=torch.float32)
        logits=critic(x).flatten()
        adv=z-logits.detach().sigmoid()
        lp=policy(x,a,mask)
        ent=-(lp.exp()*lp).sum(1)
        use=mask.sum(1)>1
        denom=w[use].sum().clamp_min(1e-8)
        actor_loss=(-((lp*chosen).sum(1)*adv+.01*ent)[use]*w[use]).sum()/denom
        critic_loss=(nn.functional.binary_cross_entropy_with_logits(logits,z,reduction="none")*w).sum()/w.sum()
        (actor_loss*w[use].sum()/actor_weight+critic_loss*w.sum()/critic_weight).backward()
        logs.append([float(v.detach()) for v in (actor_loss,critic_loss,adv.mean(),adv.std(unbiased=False),ent.mean())])
    norm=nn.utils.clip_grad_norm_(list(actor.parameters())+list(critic.parameters()),1,error_if_nonfinite=True)
    opt.step()
    with torch.no_grad():
        x,a,_,mask,_=next(batches(rows,64,fixtures))
        after=policy(x,a,mask).exp().numpy()
    export(out/"actor.bin",PH,actor_params(actor));export(out/"critic.bin",VH,list(critic.parameters()))
    torch.save(dict(actor=actor.state_dict(),critic=critic.state_dict(),optimizer=opt.state_dict()),out/"checkpoint.pt")
    put(out/"training.json",dict(samples=len(rows),eligible=sum(len(p)>1 for p in behavior),action_counts=kinds,
        probability_parity=float(error),mean_metrics=np.mean(logs,axis=0).tolist()+[float(norm)],metrics=["actor_loss","critic_loss","advantage_mean","advantage_sd","entropy","grad_norm"],
        seconds=time.perf_counter()-started,actor_before=model_hash,actor_after=digest(out/"actor.bin"),
        critic_before=digest(critic_path),critic_after=digest(out/"critic.bin"),mean_tv=float(np.abs(old-after).sum(1).mean()/2),
        top_changed=int((old.argmax(1)!=after.argmax(1)).sum()),examples=[dict(before=p.tolist(),after=q.tolist()) for p,q in zip(old,after)]))

def main():
    p=argparse.ArgumentParser();p.add_argument("root",type=Path);p.add_argument("out",type=Path)
    p.add_argument("--games",type=int,default=200);p.add_argument("--rounds",type=int,default=3)
    p.add_argument("--start",type=Path);p.add_argument("--critic",type=Path)
    p.add_argument("--temperature",type=float,default=1.1);p.add_argument("--lr",type=float,default=.0002)
    p.add_argument("--random-init",action="store_true")
    p.add_argument("--rollback",type=Path)
    p.add_argument("--historical",type=Path,action="append",default=[])
    p.add_argument("--self-fraction",type=float,default=.5)
    p.add_argument("--fixed-training",type=int,default=0)
    p.add_argument("--trainer",choices=("legacy","ppo"),default="legacy")
    p.add_argument("--actor-batch",type=int,default=256);p.add_argument("--critic-batch",type=int,default=256)
    p.add_argument("--epochs",type=int,default=1);p.add_argument("--critic-epochs",type=int,default=1)
    p.add_argument("--max-kl",type=float,default=.01);p.add_argument("--clip",type=float,default=.2)
    p.add_argument("--entropy",type=float,default=.01)
    p.add_argument("--weighting",choices=("game","decision"),default="game")
    p.add_argument("--reuse-first",type=Path)
    a=p.parse_args();torch.set_num_threads(2)
    root,out=a.root,a.out
    ppo={k:getattr(a,k) for k in ("actor_batch","critic_batch","epochs","critic_epochs","max_kl","clip","entropy","weighting")} if a.trainer=="ppo" else None
    identity=dict(games=a.games,trainer=a.trainer,ppo=ppo,lr=a.lr,temperature=a.temperature,
        start=str(a.start),critic=str(a.critic),reuse_first=str(a.reuse_first),random_init=a.random_init,
        self_fraction=a.self_fraction,fixed_training=a.fixed_training,historical=[str(p) for p in a.historical])
    if not a.rollback:
        stamp=out/"trainer.json"
        if stamp.exists() and json.loads(stamp.read_text())!=identity:raise ValueError("Trainer configuration changed")
        put(stamp,identity)
    champion=(root/"work/policy-v1/policy-v1.bin").resolve()
    critic=(root/"work/value-v1/value-v1.bin").resolve()
    if a.rollback:
        target=a.rollback.resolve()
        tensors(target,PH,actor_params(Model()))
        put(out/"champion.json",dict(path=str(target),sha256=digest(target)))
        return
    if a.critic:critic=a.critic.resolve()
    source=a.start.resolve() if a.start else champion
    if a.random_init:
        if a.start:raise ValueError("--random-init and --start are mutually exclusive")
        torch.manual_seed(7203)
        source=out/"random-actor.bin"
        export(source,PH,actor_params(Model()))
    ood=json.loads((root/"work/value-ood/configs/zoo_variant-freeze.json").read_text())["CustomDecks"]
    saved=json.loads((root/"work/self-play-cross-1000/configs/zoo-vs-inner-fire.json").read_text())["CustomDecks"]
    decks={**ood,**saved}
    pool=["zoo","freeze","miracle","inner-fire","zoo_variant","mage_board"]
    if not 0<=a.self_fraction<=1 or not 0<=a.fixed_training<a.games:
        raise ValueError("Invalid opponent mix")
    def collect(folder,games,seed,modes,models,pool_ids,enemy_deck=None):
        folder.mkdir(parents=True,exist_ok=True)
        cfg=dict(Games=games,Seed=seed,Temperature=a.temperature,Iterations=16,Modes=modes,Models=[str(m) if m else None for m in models],Pool=pool_ids,Decks=decks)
        if enemy_deck:cfg["EnemyDeck"]=enemy_deck
        config=folder/"config.json"
        identity=dict(config=cfg,weights=[digest(m) if m else None for m in models])
        stamp=folder/"identity.json"
        if stamp.exists() and json.loads(stamp.read_text())!=identity:raise ValueError(f"Run identity changed: {folder}")
        manifest=folder/"manifest.json"
        if manifest.exists():
            for name,expected in json.loads(manifest.read_text())["files"].items():
                if digest(folder/name)!=expected:raise ValueError(f"Invalid rollout receipt: {folder/name}")
        put(stamp,identity);put(config,cfg)
        subprocess.run([str(out/"Rollout.exe"),str(root),str(config),str(folder)],check=True)
        files=sorted(folder.glob("[0-9]*.json"))
        if len(files)!=games:raise ValueError("Incomplete batch")
        data=[json.loads(f.read_text(encoding="utf8")) for f in files]
        put(folder/"manifest.json",dict(files={f.name:digest(f) for f in files},wins=dict(Counter(g["winner"] for g in data)),
            decks=dict(Counter("/".join(g["decks"]) for g in data)),classes=dict(Counter(c for g in data for c in g["classes"])),
            first=dict(Counter(g["first"] for g in data)),mean_turns=sum(g["turns"] for g in data)/games,
            mean_mana=sum(r["mana"] for g in data for r in g["rows"])/sum(len(g["rows"]) for g in data)))
        return data
    def replay(folder,games,models):
        manifest=json.loads((folder/"manifest.json").read_text())
        expected=[digest(m) for m in models]
        data=[]
        for name,checksum in sorted(manifest["files"].items()):
            file=folder/name
            if digest(file)!=checksum:raise ValueError("Replay checksum mismatch")
            game=json.loads(file.read_text(encoding="utf8"))
            if game["models"]!=expected or game["modes"]!=["policy","policy"]:
                raise ValueError("Replay behavior model mismatch")
            data.append(game)
        if len(data)!=games:raise ValueError("Replay batch size mismatch")
        return data
    def compare(folder,base,candidate,mode,opp,opponent,pool_ids,seed,n):
        old=collect(folder/"before",n,seed,[mode,opp],[base,opponent],pool_ids)
        new=collect(folder/"after",n,seed,[mode,opp],[candidate,opponent],pool_ids)
        assert all((g["seed"],g["first"],g["decks"])==(h["seed"],h["first"],h["decks"]) for g,h in zip(old,new))
        def score(rows):
            return dict(wins=sum(g["winner"]==0 for g in rows),first_wins=sum(g["winner"]==0 and g["first"]==0 for g in rows),
                second_wins=sum(g["winner"]==0 and g["first"]==1 for g in rows),fallback=sum(g["fallback"][0] for g in rows),
                search_ms=sum(g["ms"][0] for g in rows),inference_ms=sum(g["inference"][0] for g in rows),
                decisions=sum(r["seat"]==0 for g in rows for r in g["rows"]))
        put(folder/"paired.json",[dict(seed=g["seed"],first=g["first"],decks=g["decks"],before=g["winner"],after=h["winner"]) for g,h in zip(old,new)])
        plus=sum(g["winner"]!=0 and h["winner"]==0 for g,h in zip(old,new))
        minus=sum(g["winner"]==0 and h["winner"]!=0 for g,h in zip(old,new))
        pvalue=sum(math.comb(plus+minus,i) for i in range(plus,plus+minus+1))/2**(plus+minus)
        return dict(before=score(old),after=score(new),improved=plus,regressed=minus,p=pvalue,games=n)
    def critic_holdout(path, before, after):
        x=[];z=[]
        for file in sorted(path.glob("[0-9]*.json")):
            game=json.loads(file.read_text(encoding="utf8"))
            for row in game["rows"]:
                x.append(row["x"])
                z.append(.5 if game["winner"]<0 else float(game["winner"]==row["seat"]))
        x=torch.tensor(x,dtype=torch.float32)
        z=torch.tensor(z,dtype=torch.float32)
        model=nn.Sequential(nn.Linear(293,128),nn.ReLU(),nn.Linear(128,128),nn.ReLU(),nn.Linear(128,1))
        result={}
        with torch.no_grad():
            for name,path in (("before",before),("after",after)):
                tensors(path,VH,list(model.parameters()))
                p=model(x).flatten()
                result[name]=dict(rows=len(z),brier=float((p.sigmoid()-z).square().mean()),
                    logloss=float(nn.functional.binary_cross_entropy_with_logits(p,z)))
        return result
    for i in range(a.rounds):
        folder=out/f"round-{i+1:02}";folder.mkdir(exist_ok=True)
        n=20 if a.games==20 else a.games
        train=[]
        first=int((n-a.fixed_training)*a.self_fraction)
        historic=n-first-a.fixed_training
        frozen=a.historical[i%len(a.historical)].resolve() if a.historical else champion
        if i==0 and a.reuse_first:
            if a.fixed_training or not first or not historic:raise ValueError("Replay requires both original policy batches")
            train+=replay(a.reuse_first/"self",first,[source,source])
            train+=replay(a.reuse_first/"history",historic,[source,frozen])
        else:
            if first:train+=collect(folder/"self",first,553000+10*i,["policy","policy"],[source,source],pool)
            if historic:train+=collect(folder/"history",historic,553001+10*i,["policy","policy"],[source,frozen],pool)
            if a.fixed_training:train+=collect(folder/"train-fixed",a.fixed_training,553002+10*i,["policy","freeze"],[source,None],pool,"freeze")
        update(train,source,critic,folder,1200+i,a.temperature,a.lr,ppo)
        candidate=folder/"actor.bin"
        seed=651000+i*100
        evals={
            "frozen":compare(folder/"frozen",source,candidate,"policy","policy",champion,pool,seed,20),
            "fixed":compare(folder/"fixed",source,candidate,"policy","freeze",None,["freeze"],seed+1,8),
            "ood":compare(folder/"ood",source,candidate,"policy","policy",champion,["rogue_board"],seed+2,8),
            "puct":compare(folder/"puct",source,candidate,"puct","uct",None,pool,seed+3,8)}
        heldout=critic_holdout(folder/"ood"/"before",critic,folder/"critic.bin") if ppo is not None else None
        # Pilot comparisons can screen regressions. Promotion requires an independent confirmatory set.
        eligible=evals["frozen"]["p"]<.05 and all(v["after"]["wins"]>=v["before"]["wins"] for v in evals.values())
        promoted=False
        if eligible:
            final=compare(folder/"confirm",champion,candidate,"policy","policy",champion,pool,851000+i,40)
            evals["confirm"]=final
            promoted=final["p"]<.025 and final["after"]["wins"]>final["before"]["wins"]
        if promoted:champion=candidate
        put(out/"champion.json",dict(path=str(champion),sha256=digest(champion)))
        put(folder/"evaluation.json",dict(version=i+1,sha256=digest(candidate),promoted=promoted,evaluations=evals,critic_holdout=heldout,
            policy_schema=1,feature_schema=1,learning_rate=a.lr,temperature=a.temperature,training_games=n,
            training_manifests=[str(base/name/"manifest.json") for base in [a.reuse_first if i==0 and a.reuse_first else folder]
                for name in ("self","history","train-fixed") if (base/name/"manifest.json").exists()]))
        print(f"Round {i+1}: {[(k,v['before']['wins'],v['after']['wins']) for k,v in evals.items()]} promoted={promoted}",flush=True)
        source=candidate;critic=folder/"critic.bin"
if __name__=="__main__":main()


