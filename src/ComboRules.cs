using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
public static class ComboRules {
 public static readonly string[] Ids={"CS2_073","EX1_124","EX1_131","EX1_133","EX1_134","EX1_137","EX1_613","NEW1_005"};
 public static bool Active(MatchEngine game){return game.CardsPlayed>0;}
 public static string Target(CardRecord card,bool combo){if(card.BaseId=="EX1_134")return combo?"ANY_CHARACTER":"NONE";if(card.BaseId=="NEW1_005")return combo?"MINION":"NONE";return null;}
 public static void Apply(EffectContext c){var g=c.Game;switch(c.Card.BaseId){
  case "CS2_073":g.Buff(c.Target,c.Combo?4:2,0);break;
  case "EX1_124":g.Damage(c.Target,(c.Combo?4:2)+g.SpellPower(c.Seat));break;
  case "EX1_131":if(c.Combo)g.Summon(c.Seat,"GAME_DEFIAS_BANDIT",g.Players[c.Seat].Board.IndexOf(c.Summoned)+1);break;
  case "EX1_133":g.Damage(c.Target,c.Combo?2:1);break;
  case "EX1_134":if(c.Combo)g.Damage(c.Target,2);break;
  case "EX1_137":g.Damage(new MatchTarget(1-c.Seat),2+g.SpellPower(c.Seat));if(c.Combo)g.ScheduleComboReturn(c.Seat,c.PlayedHand);break;
  case "EX1_613":if(c.Combo)g.Buff(new MatchTarget(c.Seat,c.Summoned.Id),2*c.PreviousCardsPlayed,2*c.PreviousCardsPlayed);if(c.Combo&&2+2*c.PreviousCardsPlayed>=12)g.EdwinEntrance(c.Summoned);break;
  case "NEW1_005":if(c.Combo)g.ReturnToHand(c.Target);break;
 }}
}
public sealed partial class MatchEngine {
 sealed class ComboReturn {public int Seat,Due;public HandCard Card;}
 readonly List<ComboReturn> comboReturns=new List<ComboReturn>();
 public void ScheduleComboReturn(int seat,HandCard card){comboReturns.Add(new ComboReturn{Seat=seat,Due=Turn+2,Card=card});Log.Add("玩家 "+(seat+1)+" · 裂颅之击将在下个己方回合返回手牌");}
 void ReturnComboCards(int seat){foreach(var item in comboReturns.Where(r=>r.Seat==seat&&r.Due<=Turn).ToArray()){comboReturns.Remove(item);var p=Players[seat];if(p.Hand.Count>=10){RecordGraveyard(seat,seat,item.Card.CardId,item.Card.Id,GraveyardReason.ReturnOverflow);Log.Add("玩家 "+(seat+1)+" · 裂颅之击回手失败：手牌已满");continue;}p.Hand.Add(new HandCard{Id=nextId++,CardId=item.Card.CardId,Generated=item.Card.Generated});Log.Add("玩家 "+(seat+1)+" · 裂颅之击返回手牌");}}
}
}
