using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
public enum HistoryKind { Draw, Mulligan, Played, Discard, Burn, Destroy, Gain, Return, ShuffleIn, ArmSecret, RevealSecret, NoSecretTrigger, HandGrowth, DelayReturn, ReturnDue, Unmodeled, HandCostFloorShift, SummonFromHand, ArmSecretFromDeck }
public sealed class PublicHistoryEvent {
 public readonly int Sequence,Seat,Count,Adjustment,VisibleTo;
 public readonly HistoryKind Kind;
 public readonly string CardId;
 internal readonly string Origin;
 internal PublicHistoryEvent(int sequence,HistoryKind kind,int seat,string cardId,int count,int adjustment,int visibleTo,string origin=null){
  Sequence=sequence;Kind=kind;Seat=seat;CardId=cardId;Count=count;Adjustment=adjustment;VisibleTo=visibleTo;Origin=origin;
 }
}
public sealed partial class MatchEngine {
 internal void ShiftHandCosts(int seat,int delta){
  if(delta==0)return;
  NoteHistory(HistoryKind.HandCostFloorShift,seat,adjustment:delta);
  var naga=Players[seat].Board.Where(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="LOE_038").OrderByDescending(u=>u.Id).FirstOrDefault();
  foreach(var card in Players[seat].Hand){
   card.CostAdjustment=ShiftCardCost(Card(card.CardId).Cost,card.CostAdjustment,delta);
   if(naga!=null)card.ApplyAfterFixedCost(naga.Id,delta);
  }
 }
 internal static int ShiftCardCost(int baseCost,int adjustment,int delta){
  return delta<0&&baseCost+adjustment<=0?adjustment:Math.Max(-baseCost,adjustment+delta);
 }
 List<PublicHistoryEvent> publicHistory;
 internal void NoteHistory(HistoryKind kind,int seat,string cardId=null,int count=1,int adjustment=0,int visibleTo=-1,
  [System.Runtime.CompilerServices.CallerMemberName] string origin=null){
  if(headless)return;
  if(publicHistory==null)publicHistory=new List<PublicHistoryEvent>();
  publicHistory.Add(new PublicHistoryEvent(publicHistory.Count+1,kind,seat,cardId,count,adjustment,visibleTo,kind==HistoryKind.Unmodeled?origin:null));
 }
 public PublicHistoryEvent[] ObserveHistory(int seat){
  if(seat<0||seat>1)throw new ArgumentOutOfRangeException("seat");
  return publicHistory==null?new PublicHistoryEvent[0]:publicHistory.Where(e=>e.VisibleTo<0||e.VisibleTo==seat)
   .Select((e,i)=>new PublicHistoryEvent(i+1,e.Kind,e.Seat,e.CardId,e.Count,e.Adjustment,e.VisibleTo,e.Origin)).ToArray();
 }
}
}
