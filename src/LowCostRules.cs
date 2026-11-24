using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
public static class LowCostRules {
 public static readonly string[] CardIds={"CS2_146","EX1_001","EX1_049","EX1_055","EX1_059","EX1_080","EX1_082","NEW1_016","NEW1_018","NEW1_019","NEW1_020","NEW1_025"};
 public static readonly ConditionalTrigger[] Triggers={
  new ConditionalTrigger{CardId="EX1_001",Timing=MatchTriggerTiming.CharacterHealed,Condition=c=>c.Amount>0,Apply=c=>c.Game.Buff(new MatchTarget(c.Source.Owner,c.Source.Id),2,0)},
  new ConditionalTrigger{CardId="EX1_055",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>c.Source.TempAttack+=2},
  new ConditionalTrigger{CardId="EX1_080",Timing=MatchTriggerTiming.CardPlayed,Condition=c=>c.Card!=null&&MatchEngine.Has(c.Card,"SECRET"),Apply=c=>c.Game.Buff(new MatchTarget(c.Source.Owner,c.Source.Id),1,1)},
  new ConditionalTrigger{CardId="NEW1_019",Timing=MatchTriggerTiming.MinionSummoned,Condition=c=>c.Summoned!=null&&c.Seat==c.Source.Owner&&c.Summoned.Id!=c.Source.Id,Apply=c=>{var target=c.Game.RandomTarget(c.Game.Characters(1-c.Source.Owner));if(target!=null)c.Game.Damage(target,1);}},
  new ConditionalTrigger{CardId="NEW1_020",Timing=MatchTriggerTiming.SpellResolved,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>c.Game.DamageBatch(()=>{foreach(var target in c.Game.Characters(0,false).Concat(c.Game.Characters(1,false)))c.Game.Damage(target,1);})},
 };
 public static void Apply(EffectContext c){var g=c.Game;switch(c.Card.BaseId){
  case "EX1_049":g.ReturnToHand(c.Target);break;
  case "EX1_059":var unit=g.Unit(c.Target);int attack=g.AttackValue(unit),health=unit.Health;g.SetHealth(c.Target,attack);unit.BuffAttack=health-unit.BaseAttack-unit.TempAttack-EnrageRules.AttackBonus(unit,g.Card(unit.CardId).BaseId)-g.AuraAttack(unit);if(attack==0)unit.DamageTaken=unit.MaxHealth;break;
  case "EX1_082":for(int n=0;n<3&&!g.Finished;n++){var target=g.RandomTarget(g.Characters(0).Concat(g.Characters(1)).Where(t=>t.Hero||t.UnitId!=c.Summoned.Id&&g.Unit(t).Health>0));if(target!=null)g.DamageSequenceStep(target,1);}break;
  case "NEW1_016":g.DrawMatchingCard(c.Seat,"PIRATE");break;
  case "NEW1_018":c.Summoned.BuffAttack+=g.WeaponAttackValue(c.Seat);break;
  case "NEW1_025":var enemy=g.Players[1-c.Seat];if(enemy.WeaponDurability>0&&--enemy.WeaponDurability==0)g.BreakWeapon(1-c.Seat);break;
 }}
}
public sealed partial class MatchEngine {
 sealed class SummonNotification {
  internal int Seat;
  internal BattleUnit Unit;
  internal BattleUnit[] Listeners;
 }
 Queue<SummonNotification> summonTriggers;
 bool drainingSummons;
 int deferSummonDepth;
 void QueueSummonTrigger(BattleUnit unit,BattleUnit[] listeners,bool deferred){
  if(summonTriggers==null)summonTriggers=new Queue<SummonNotification>();
  summonTriggers.Enqueue(new SummonNotification{Seat=unit.Owner,Unit=unit,Listeners=listeners});
  if(!deferred&&deferSummonDepth==0)FlushSummonTriggers();
 }
 void FlushSummonTriggers(){
  if(drainingSummons||summonTriggers==null)return;
  drainingSummons=true;
  try{
   int steps=0;
   while(summonTriggers.Count>0){
    if(++steps>10000)throw new InvalidOperationException("结算超过安全上限，可能存在循环触发。");
    var item=summonTriggers.Dequeue();
    ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.MinionSummoned,item.Seat,summoned:item.Unit,listeners:item.Listeners);
   }
  }finally{drainingSummons=false;}
 }
 public void DrawMatchingCard(int seat,string race){NoteHistory(HistoryKind.Unmodeled,seat);var p=Players[seat];var indices=p.Deck.Select((deckCard,i)=>new{Card=Card(deckCard),Index=i}).Where(x=>x.Card.Race==race).ToArray();if(indices.Length==0)return;int index=indices[random.Next(indices.Length)].Index;string id=p.Deck[index];p.Deck.RemoveAt(index);p.Deck.Insert(0,id);Draw(seat,1);}
}
}
