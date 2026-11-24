import argparse,csv,json,math
from collections import Counter
from pathlib import Path

def summarize(folder):
    modes={}
    for file in folder.glob("*.json"):
        mode=file.stem.rsplit("-",1)[0]
        row=json.loads(file.read_text(encoding="utf8"))
        if "i" in row:modes.setdefault(mode,{})[row["i"]]=row
    report={}
    for mode,by_id in modes.items():
        rows=list(by_id.values())
        trace=[t for r in rows for t in r["trace"]]
        depths=Counter();expansion=Counter()
        roots=[];tops=[];effective=[]
        calls=hits=fail=0;policy_ms=0
        for t in trace:
            for field,dest in (("depth",depths),("expansion",expansion)):
                for d,n in enumerate(t[field] or []):dest[d]+=n
            p=t["policy"]
            if p:
                calls+=p["Calls"];hits+=p["CacheHits"];fail+=p["Failures"];policy_ms+=p["Milliseconds"]
            visits=[a["Visits"] for a in (t["root"] or []) if a["Visits"]]
            if visits:
                roots.append(len(visits))
                tops.append(max(visits)/sum(visits))
                effective.append(math.exp(-sum(v/sum(visits)*math.log(v/sum(visits)) for v in visits)))
        total=lambda key:sum(r[key] for r in rows)
        mean=lambda x:sum(x)/len(x) if x else 0
        report[mode]=dict(games=len(rows),wins=sum(r["Winner"]==0 for r in rows),
          first=sum(r["first"]==0 and r["Winner"]==0 for r in rows),second=sum(r["first"]==1 and r["Winner"]==0 for r in rows),
          fallbacks=total("fallbacks"),sampling_failures=total("failures"),searches=len(trace),search_ms=total("milliseconds"),
          mean_ms=total("milliseconds")/len(trace),iterations=sum(t["Iterations"] for t in trace),terminal=total("terminal"),truncated=total("truncated"),
          policy_calls=calls,policy_cache_hits=hits,policy_failures=fail,policy_ms=policy_ms,depth=dict(depths),expansion=dict(expansion),
          mean_depth=sum(d*n for d,n in depths.items())/max(1,sum(depths.values())),mean_root_visited=mean(roots),mean_root_top_share=mean(tops),
          mean_root_effective=mean(effective))
    baseline=modes.get("Heuristic",{})
    comparisons={}
    with (folder/"paired.csv").open("w",newline="") as f:
        writer=csv.writer(f);writer.writerow(["mode","index","seed","first_seat","uct_winner","guided_winner"])
        for mode,rows in modes.items():
            if mode=="Heuristic":continue
            deltas=[]
            for i in sorted(baseline.keys()&rows.keys()):
                a,b=baseline[i],rows[i]
                assert (a["seed"],a["first"])==(b["seed"],b["first"])
                writer.writerow([mode,i,a["seed"],a["first"],a["Winner"],b["Winner"]])
                deltas.append(int(b["Winner"]==0)-int(a["Winner"]==0))
            p,m=deltas.count(1),deltas.count(-1)
            n=p+m
            exact=min(1,2*sum(math.comb(n,k) for k in range(min(p,m)+1))/2**n)
            comparisons[mode]=dict(pairs=len(deltas),improved=p,regressed=m,mcnemar_exact_p=exact)
    report["paired"]=comparisons
    return report

if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("root",type=Path);a=p.parse_args()
    report={f.name:summarize(f) for f in sorted(a.root.iterdir()) if f.is_dir()}
    (a.root/"summary.json").write_text(json.dumps(report,indent=2),encoding="utf8")
    for k,v in report.items(): print(k,v["paired"],{m:(r["games"],r["wins"],round(r["mean_root_top_share"],3),round(r["mean_depth"],3)) for m,r in v.items() if m!="paired"})

