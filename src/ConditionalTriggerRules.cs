using System;
using System.Linq;

namespace Touchfish {
public enum MatchTriggerTiming { CardPlayed, SpellCast, TurnStart }
public sealed class MatchTriggerContext {
 public MatchEngine Game;public int Seat;public CardRecord Card;public BattleUnit Source;
}
// Conditions and effects are separate; new conditional cards register their timing here.
public sealed class ConditionalTrigger {
 public string CardId;public MatchTriggerTiming Timing;
 public Func<MatchTriggerContext,bool> Condition;public Action<MatchTriggerContext> Apply;
}
public static class ConditionalTriggerRules {
 static readonly ConditionalTrigger[] rules={
  new ConditionalTrigger{CardId="EX1_044",Timing=MatchTriggerTiming.CardPlayed,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>c.Game.Buff(new MatchTarget(c.Source.Owner,c.Source.Id),1,1)},
  new ConditionalTrigger{CardId="EX1_100",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Card!=null&&c.Card.Type=="SPELL",Apply=c=>c.Game.CopySpellToHand(1-c.Seat,c.Card)},
  new ConditionalTrigger{CardId="EX1_557",Timing=MatchTriggerTiming.TurnStart,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>{if(c.Game.TriggerCoinFlip())c.Game.FishingDraw(c.Source);}},
  new ConditionalTrigger{CardId="NEW1_021",Timing=MatchTriggerTiming.TurnStart,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>c.Game.DestroyAllMinions()}
 };
 public static string[] CardIds{get{return rules.Select(r=>r.CardId).Distinct().ToArray();}}
 public static void Resolve(MatchEngine game,MatchTriggerTiming timing,int seat,CardRecord card=null){
  var queue=game.Players.SelectMany(p=>p.Board).OrderBy(u=>u.Id).ToArray();
  foreach(var source in queue){if(game.Finished)break;if(source.Silenced||source.Health<=0||!game.Players[source.Owner].Board.Contains(source))continue;
   foreach(var rule in rules.Where(r=>r.Timing==timing&&r.CardId==game.Card(source.CardId).BaseId)){
    var context=new MatchTriggerContext{Game=game,Seat=seat,Card=card,Source=source};if(!rule.Condition(context))continue;
    game.Log.Add("玩家 "+(source.Owner+1)+" · 触发："+game.Card(source.CardId).Name);rule.Apply(context);game.Cleanup();if(game.Finished)break;
   }
  }
 }
}
public static class TurnStartRules {
 public static void Resolve(MatchEngine game,int seat){ConditionalTriggerRules.Resolve(game,MatchTriggerTiming.TurnStart,seat);}
}
public static class TimedCostRules {
 public static void GrantFreeSpells(MatchEngine game,int seat,int throughTurn){game.Players[seat].FreeSpellsUntilTurn=Math.Max(game.Players[seat].FreeSpellsUntilTurn,throughTurn);}
 public static int Apply(MatchEngine game,int seat,CardRecord card,int cost){if(card.Type=="SPELL")cost=Math.Max(0,cost-game.Players[seat].PreparationDiscount);return card.Type=="SPELL"&&game.Players[seat].FreeSpellsUntilTurn>=game.Turn?0:cost;}
 public static void Expire(MatchEngine game){foreach(var player in game.Players){player.PreparationDiscount=0;if(player.FreeSpellsUntilTurn<=game.Turn)player.FreeSpellsUntilTurn=-1;}}
}
public sealed partial class MatchEngine {
 internal bool TriggerCoinFlip(){return random.Next(2)==0;}
 public void CopySpellToHand(int seat,CardRecord card){var player=Players[seat];if(player.Hand.Count>=10){Log.Add("玩家 "+(seat+1)+" · 手牌已满，未获得法术复制。");return;}player.Hand.Add(new HandCard{Id=nextId++,CardId=card.Id,Generated=true});Log.Add("玩家 "+(seat+1)+" · 获得法术复制 1 张。");}
}
}
