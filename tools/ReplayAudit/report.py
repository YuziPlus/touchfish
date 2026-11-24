import csv,json,re,sys
from pathlib import Path
p=Path(sys.argv[1])
rows=json.loads((p/"decisions.json").read_text(encoding="utf8"))
catalog={}
for f in Path("data/cardsets").glob("*.json"):
    data=json.loads(f.read_text(encoding="utf-8-sig"))
    for c in data["Cards"]:catalog[c["Id"]]=c["Name"]
def name(key):
    if not key:return "—"
    if key.startswith("decision:"):return key
    kind,slot,card,target,position,choice=key.split("|",5)
    label={"play":"使用","attack":"攻击","power":"英雄技能","end":"结束回合"}.get(kind,kind)
    if card:label+=" "+catalog.get(card,card)
    if target!="-":
        side="己方" if target[0]=="S" else "敌方"
        label+=" → "+side+("英雄" if target[1:]=="-1" else "第"+str(int(target[1:].split(":")[0])+1)+"位 "+catalog.get(target.split(":")[-1],target.split(":")[-1]))
    if position!="-1":label+="（站位 "+str(int(position)+1)+"）"
    if choice:label+=" / "+choice
    return label
def outcome(r,b):
    q=r[b]
    if not q:return "—","—","—","—"
    if r["Kind"]=="mulligan":
        hand={x["Id"]:catalog.get(x["CardId"],x["CardId"]) for x in r["hand"]}
        fmt=lambda ids:"换 "+("、".join(hand.get(i,str(i)) for i in ids) if ids else "无")
        ids=sorted(map(int,r["human"].split(","))) if r["human"] else []
        ranked=sorted(q["Candidates"],key=lambda c:(-c["MeanValue"],len(c["ReplaceIds"])))
        hit=next(((i,c["MeanValue"]) for i,c in enumerate(ranked,1) if sorted(c["ReplaceIds"])==ids),None)
        return fmt(q["ReplaceIds"]),f"{hit[0]} / {hit[1]:.3f}" if hit else "—",str(q["Worlds"]),q["FallbackReason"] or "—"
    return name(q["best"]),f'{q["humanRank"]} / {q["value"]:.3f}' if q["visits"] else "—",str(q["visits"]),q["FallbackReason"] or ("唯一合法" if q["StopReason"]=="single-action" else "—")
with (p/"逐步决策.csv").open("w",encoding="utf-8-sig",newline="") as f, (p/"逐回合对照.md").open("w",encoding="utf8") as md:
    header=["步骤","行动方","人类操作","16 次首选","人类排名 / 价值","人类访问次数","16 次回退","160 次首选","人类排名 / 价值","人类访问次数","160 次回退"]
    writer=csv.writer(f);writer.writerow(["引擎半回合",*header])
    current=None
    for r in rows:
        turn=0 if r["Kind"]=="mulligan" else r["Turn"]
        if turn!=current:
            current=turn
            title="起手换牌" if turn==0 else f"第 {(turn+1)//2} 回合 · {'萨满' if turn%2 else '法师'}"
            md.write("\n## "+title+"\n\n| "+" | ".join(header)+" |\n|"+"---|"*len(header)+"\n")
        rec=" / ".join(r["Log"][:1]) or name(r["human"])
        if turn==0:
            hand={x["Id"]:catalog.get(x["CardId"],x["CardId"]) for x in r["hand"]}
            rec="换 "+("、".join(hand[int(i)] for i in r["human"].split(",")) if r["human"] else "无")
        row=[str(r["Index"]),("萨满","法师")[r["Seat"]],rec,*outcome(r,"small"),*outcome(r,"large")]
        writer.writerow([turn,*row]);md.write("| "+" | ".join(v.replace("|","\\|") for v in row)+" |\n")
