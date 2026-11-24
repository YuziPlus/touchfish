using System;
using System.Collections.Generic;

namespace Touchfish {
// Events are public by default; owner-only events must be filtered before LAN serialization.
public sealed class MatchVisualEvent {
 public int RecipientSeat=-1;
 public int Sequence,Action,Seat,UnitId,Slot=-1,TargetSeat=-1,TargetUnitId,Amount,CardCost,PaidCost,HealthBefore,Overkill;public string Kind,CardName,CardId,CardText;
}
public sealed class MatchVisualCursor {
 string session;int sequence;
 public MatchVisualEvent[] Take(string nextSession,IEnumerable<MatchVisualEvent> events,bool display){
  if(session!=nextSession){session=nextSession;sequence=0;}
  var result=new List<MatchVisualEvent>();foreach(var item in events){if(item.Sequence<=sequence)continue;sequence=item.Sequence;if(display)result.Add(item);}return result.ToArray();
 }
}
public partial class MatchEngine {
 public string VisualSession;public readonly List<MatchVisualEvent> VisualEvents=new List<MatchVisualEvent>();int visualSequence,visualAction;
 readonly Dictionary<int,int[]> lethalVisualDamage=new Dictionary<int,int[]>();
 void RememberLethalDamage(BattleUnit unit,int amount){if(!headless&&unit.Health>0&&amount>=unit.Health)lethalVisualDamage[unit.Id]=new[]{amount,unit.Health};}
 void EmitMinionDeath(BattleUnit unit){if(headless)return;int[] hit;lethalVisualDamage.TryGetValue(unit.Id,out hit);lethalVisualDamage.Remove(unit.Id);EmitVisual("death",unit.Owner,unit.Id,Players[unit.Owner].Board.IndexOf(unit),hit==null?0:hit[0],Card(unit.CardId).Name);var item=VisualEvents[VisualEvents.Count-1];item.CardId=unit.CardId;item.HealthBefore=hit==null?0:hit[1];item.Overkill=hit==null?0:Math.Max(0,hit[0]-hit[1]);}
 public void EmitSpell(CardRecord card,int seat,int paidCost,string choice){
  if(headless)return;
  if(MatchEngine.Has(card,"SECRET")){EmitVisual("spell",seat,name:"一张奥秘");return;}
  EmitVisual("spell",seat,name:card.Name);var item=VisualEvents[VisualEvents.Count-1];item.CardId=card.Id;item.CardCost=card.Cost;item.PaidCost=paidCost;item.CardText=card.Text;
  if(Rules.IsChoice(card)&&choice!=null)item.CardText+="\n本次抉择："+System.Linq.Enumerable.First(Rules.Choices(card),option=>option.Key==choice).Title;
 }
 public void EmitVisual(string kind,int seat,int unitId=0,int slot=-1,int amount=0,string name=null){if(headless)return;VisualEvents.Add(new MatchVisualEvent{Sequence=++visualSequence,Action=visualAction,Kind=kind,Seat=seat,UnitId=unitId,Slot=slot,Amount=amount,CardName=name});if(VisualEvents.Count>128)VisualEvents.RemoveAt(0);}
}
}
