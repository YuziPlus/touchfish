using System;
using System.Linq;
namespace Touchfish {
public static class TurnEndRules {
 public static readonly string[] Ids={"CS2_059","EX1_004","EX1_249","EX1_274","EX1_298","EX1_316","EX1_334","EX1_571","EX1_572","EX1_575","EX1_597","NEW1_037","NEW1_038","NEW1_040"};
 public static readonly string[] DreamIds={"VAN_DREAM_01","VAN_DREAM_02","VAN_DREAM_03","VAN_DREAM_04","VAN_DREAM_05"};
 public static readonly string[] TriggerIds=new[]{"NEW1_009","EX1_tk9b","CS2_059","EX1_004","EX1_249","EX1_274","EX1_298","EX1_572","EX1_575","EX1_597","NEW1_037","NEW1_038","NEW1_040"};
 public static bool HasTrigger(string id){return TriggerIds.Contains(id);}
}
public sealed partial class MatchEngine {
 public bool CanUseCard(int seat,CardRecord card,HandCard hand=null){return hand!=null&&hand.Generated|| card.Type=="SPELL"|| card.CardClass=="NEUTRAL"||card.CardClass==Players[seat].ClassId||!card.Collectible&&(TurnEndRules.DreamIds.Contains(card.Id)||card.Id=="GAME_WORTHLESS_IMP");}
 public void GenerateCard(int seat,string id){var p=Players[seat];if(p.Hand.Count>=Mode.HandLimit){Log.Add(PlayerLabel(seat)+" · 手牌已满，未获得梦境牌。");return;}NoteHistory(HistoryKind.Unmodeled,seat);p.Hand.Add(new HandCard{Id=nextId++,CardId=id});Log.Add(PlayerLabel(seat)+" · 获得梦境牌 1 张。");}
 public void DelayDestroy(MatchTarget target,bool atStart,int seat){var unit=Unit(target);if(unit==null)return;if(!atStart)unit.DestroyAtEnd=Turn;else if(seat==0)unit.DestroyAtStart0=Turn+2;else unit.DestroyAtStart1=Turn+2;}
 public void TakeTemporaryControl(int seat,MatchTarget target){var unit=Unit(target);int previous=unit.Owner;Players[previous].Board.Remove(unit);unit.ReturnSeat=previous;unit.ReturnTurn=Turn;unit.Owner=seat;unit.SummonTurn=Turn;unit.AttacksUsed=0;Players[seat].Board.Add(unit);RefreshAuras();}
 void DestroyUnit(BattleUnit unit){unit.DamageTaken=unit.MaxHealth;}
 void ResolveStartDelayed(int seat){foreach(var unit in Players.SelectMany(p=>p.Board).Where(u=>(seat==0?u.DestroyAtStart0:u.DestroyAtStart1)>=0&&(seat==0?u.DestroyAtStart0:u.DestroyAtStart1)<=Turn).ToArray()){Log.Add("梦魇到期 · "+Card(unit.CardId).Name);DestroyUnit(unit);}Cleanup();}
 void ResolveEndTriggers(int seat){ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.TurnEnd,seat);}
 public void ApplyTurnEndTrigger(BattleUnit source){int owner=source.Owner;string id=Card(source.CardId).BaseId;switch(id){
  case "CS2_059":case "EX1_004":case "NEW1_037":var allies=Players[owner].Board.Where(u=>u.Id!=source.Id&&u.Health>0).ToArray();if(allies.Length>0){var chosen=allies[random.Next(allies.Length)];Buff(new MatchTarget(owner,chosen.Id),id=="NEW1_037"?1:0,id=="NEW1_037"?0:1);Log.Add("目标 · "+Card(chosen.CardId).Name);}break;
  case "EX1_249":foreach(var target in Characters(0).Concat(Characters(1)).Where(t=>t.Hero||t.UnitId!=source.Id).ToArray())Damage(target,2);break;
  case "EX1_274":Buff(new MatchTarget(owner,source.Id),2,2);break;
  case "EX1_298":var enemy=RandomTarget(Characters(1-owner));if(enemy!=null){Log.Add("目标 · "+TargetLabel(enemy));Damage(enemy,8);}break;
  case "EX1_572":GenerateCard(owner,TurnEndRules.DreamIds[random.Next(TurnEndRules.DreamIds.Length)]);break;
  case "EX1_575":Draw(owner,1);break;
  case "EX1_597":int position=Players[owner].Board.IndexOf(source);Damage(new MatchTarget(owner,source.Id),1);Cleanup();if(!Finished)Summon(owner,"VAN_EX1_598",Players[owner].Board.Contains(source)?Players[owner].Board.IndexOf(source)+1:position);break;
  case "NEW1_038":Buff(new MatchTarget(owner,source.Id),1,1);break;
  case "NEW1_040":Summon(owner,"GAME_GNOLL",Players[owner].Board.IndexOf(source)+1);break;
  case "NEW1_009":foreach(var unit in Players[owner].Board.ToArray())RestoreHealth(owner,new MatchTarget(owner,unit.Id),1);break;
  case "EX1_tk9b":DestroyUnit(source);break;
 }}
 void ResolveEndDelayed(){foreach(var unit in Players.SelectMany(p=>p.Board).Where(u=>u.DestroyAtEnd>=0&&u.DestroyAtEnd<=Turn).ToArray()){Log.Add("回合结束到期 · "+Card(unit.CardId).Name);DestroyUnit(unit);}Cleanup();if(Finished)return;foreach(var unit in Players.SelectMany(p=>p.Board).Where(u=>u.ReturnTurn>=0&&u.ReturnTurn<=Turn).ToArray()){int owner=unit.Owner,back=unit.ReturnSeat;Players[owner].Board.Remove(unit);unit.ReturnSeat=unit.ReturnTurn=-1;if(Players[back].Board.Count>=Mode.BoardSlots){Log.Add("返还时战场已满 · "+Card(unit.CardId).Name);DestroyUnit(unit);Players[owner].Board.Add(unit);Cleanup();}else{unit.Owner=back;unit.AttacksUsed=0;unit.SummonTurn=Turn;Players[back].Board.Add(unit);Log.Add("返还控制权 · "+Card(unit.CardId).Name);RefreshAuras();}}Cleanup();}
}
}
