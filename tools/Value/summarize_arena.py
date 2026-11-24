import csv, json
from pathlib import Path

root=Path("work/value-arena")
reports={}
for folder in sorted(root.iterdir()):
    if not folder.is_dir(): continue
    modes={}
    for path in folder.glob("*.json"):
        mode=path.name.split("-")[0]
        if mode in ("Heuristic","Neural","Shadow"):
            row=json.loads(path.read_text(encoding="utf-8"))
            modes.setdefault(mode,{})[row["i"]]=row
    if not modes:continue
    def metrics(rows):
        n=len(rows)
        sums={k:sum(r[k] for r in rows) for k in ("decisions","fallbacks","failures","terminal","truncated","calls","milliseconds","inference")}
        return dict(games=n,wins=sum(r["Winner"]==0 for r in rows),first_wins=sum(r["Winner"]==0 and r["first"]==0 for r in rows),
                    first_games=sum(r["first"]==0 for r in rows),second_wins=sum(r["Winner"]==0 and r["first"]==1 for r in rows),**sums)
    report={k:metrics(list(v.values())) for k,v in modes.items()}
    h,n=modes.get("Heuristic",{}),modes.get("Neural",{})
    common=sorted(h.keys()&n.keys())
    report["paired"]=dict(games=len(common),improved=sum(h[i]["Winner"]!=0 and n[i]["Winner"]==0 for i in common),
                          regressed=sum(h[i]["Winner"]==0 and n[i]["Winner"]!=0 for i in common))
    with (folder/"paired.csv").open("w",newline="") as f:
        w=csv.writer(f);w.writerow(["index","seed","first_seat","heuristic_winner","neural_winner","heuristic_ms","neural_ms","neural_inference_ms","neural_calls"])
        for i in common:
            a,b=h[i],n[i]
            assert a["seed"]==b["seed"] and a["first"]==b["first"]
            w.writerow([i,a["seed"],a["first"],a["Winner"],b["Winner"],a["milliseconds"],b["milliseconds"],b["inference"],b["calls"]])
    if "Shadow" in modes:
        report["shadow_identical"]=all(h[i]["actions"]==v["actions"] and h[i]["Winner"]==v["Winner"] for i,v in modes["Shadow"].items())
        assert report["shadow_identical"]
    reports[folder.name]=report
(root/"summary.json").write_text(json.dumps(reports,indent=2))
for name,m in reports.items():
    print(name,m["paired"],{k:(v["wins"],v["games"],v["first_wins"],v["second_wins"],v["fallbacks"],v["failures"]) for k,v in m.items() if isinstance(v,dict) and "wins" in v})
