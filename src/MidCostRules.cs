using System;
using System.Linq;
namespace Touchfish {
public static class MidCostRules {
 public static readonly string[] CardIds={"CS2_181","EX1_005","EX1_006","EX1_007","EX1_014","EX1_025","EX1_043","EX1_057","EX1_083","EX1_085","EX1_089","EX1_102","EX1_584","EX1_590","EX1_595","NEW1_022","NEW1_026","tt_004"};
 public static readonly ConditionalTrigger[] Triggers={
  new ConditionalTrigger{CardId="EX1_006",Timing=MatchTriggerTiming.TurnStart,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>c.Game.SwapAlarmBot(c.Source)},
  new ConditionalTrigger{CardId="EX1_007",Timing=MatchTriggerTiming.MinionDamaged,Condition=c=>c.Damaged.Id==c.Source.Id&&c.Amount>0,Apply=c=>c.Game.Draw(c.Source.Owner,1)},
  new ConditionalTrigger{CardId="EX1_102",Timing=MatchTriggerTiming.TurnStart,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>{var target=c.Game.RandomTarget(c.Game.Characters(1-c.Source.Owner));if(target!=null)c.Game.Damage(target,2);}},
  new ConditionalTrigger{CardId="EX1_595",Timing=MatchTriggerTiming.MinionDied,Condition=c=>c.Died!=null&&c.Died.Owner==c.Source.Owner,Apply=c=>c.Game.Draw(c.Source.Owner,1)},
  new ConditionalTrigger{CardId="tt_004",Timing=MatchTriggerTiming.MinionDied,Condition=c=>c.Died!=null,Apply=c=>c.Source.BuffAttack++},
  new ConditionalTrigger{CardId="NEW1_026",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Seat==c.Source.Owner,Apply=c=>c.Game.Summon(c.Source.Owner,"VAN_NEW1_026t")}
 };
 public static void Apply(EffectContext c){var g=c.Game;switch(c.Card.BaseId){
  case "CS2_181":g.Damage(new MatchTarget(c.Seat,c.Summoned.Id),4);break;
  case "EX1_005":if(c.Target!=null){var victim=g.Unit(c.Target);victim.DamageTaken=victim.MaxHealth;}break;
  case "EX1_014":g.GiveMidCostCards(1-c.Seat,"GAME_BANANA",2);break;
  case "EX1_025":g.Summon(c.Seat,"GAME_MECHANICAL_DRAGON",g.Players[c.Seat].Board.IndexOf(c.Summoned)+1);break;
  case "EX1_043":c.Summoned.BuffHealth+=g.Players[c.Seat].Hand.Count;break;
  case "EX1_057":g.ReturnToHand(c.Target);break;
  case "EX1_083":var targets=g.Players.SelectMany(p=>p.Board).Where(u=>u.Id!=c.Summoned.Id&&u.Health>0).Select(u=>new MatchTarget(u.Owner,u.Id));var target=g.RandomTarget(targets);if(target!=null)g.Transform(target,g.RandomIndex(2)==0?"VAN_EX1_tk28":"VAN_EX1_tk29");break;
  case "EX1_085":if(g.Players[1-c.Seat].Board.Count>=4){var chosen=g.RandomTarget(g.Characters(1-c.Seat,false));g.TakePermanentControl(c.Seat,chosen);}break;
  case "EX1_089":var enemy=g.Players[1-c.Seat];if(enemy.MaxMana<g.Mode.ManaLimit){enemy.MaxMana++;enemy.Mana=Math.Min(enemy.MaxMana-enemy.LockedMana,enemy.Mana+1);}break;
  case "EX1_584":var board=g.Players[c.Seat].Board;int position=board.IndexOf(c.Summoned);foreach(var unit in board.Where((u,i)=>Math.Abs(i-position)==1).ToArray())unit.SpellDamage++;break;
  case "EX1_590":int count=0;foreach(var unit in g.Players.SelectMany(p=>p.Board)){if(!unit.Shield)continue;unit.Shield=false;count++;}g.Buff(new MatchTarget(c.Seat,c.Summoned.Id),3*count,3*count);break;
 }}
}
public sealed partial class MatchEngine {
 public int MidCostDiscount(int seat,CardRecord card){return card.BaseId=="NEW1_022"?WeaponAttackValue(seat):0;}
 public void GiveMidCostCards(int seat,string cardId,int count){var p=Players[seat];int added=0;for(int n=0;n<count;n++){int instance=nextId++;if(p.Hand.Count<Mode.HandLimit){p.Hand.Add(new HandCard{Id=instance,CardId=cardId,Generated=true});added++;}else RecordGraveyard(seat,seat,cardId,instance,GraveyardReason.Burned);}Log.Add(PlayerLabel(seat)+" · 获得生成牌 "+added+" 张");}
 public void SwapAlarmBot(BattleUnit bot){var p=Players[bot.Owner];var candidates=p.Hand.Where(h=>Card(h.CardId).Type=="MINION").ToArray();if(candidates.Length==0)return;var chosen=candidates[random.Next(candidates.Length)];int position=p.Board.IndexOf(bot);p.Hand.Remove(chosen);p.Board.Remove(bot);p.Hand.Add(new HandCard{Id=nextId++,CardId=bot.CardId,Generated=true});RefreshAuras();SummonHandCard(bot.Owner,chosen,position);Log.Add(PlayerLabel(bot.Owner)+" · 报警机器人交换一张随从牌");}
}
}
