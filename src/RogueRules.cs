using System;
using System.Linq;
namespace Touchfish {
public static class RogueRules {
 public static void Apply(EffectContext c){var g=c.Game;switch(c.Card.BaseId){
  case "CS2_233":int damage=g.WeaponAttackValue(c.Seat)+g.SpellPower(c.Seat);g.BreakWeapon(c.Seat);foreach(var target in g.Characters(1-c.Seat).ToArray())g.Damage(target,damage);break;
  case "EX1_126":g.Betrayal(c.Target);break;
  case "EX1_128":foreach(var unit in g.Players[c.Seat].Board)if(!unit.Stealth){unit.Stealth=true;unit.StealthExpirySeat=c.Seat;unit.StealthUntilTurn=g.Turn+2;}break;
  case "EX1_144":g.BounceRogueUnits(new[]{g.Unit(c.Target)},-2);break;
  case "EX1_145":g.Players[c.Seat].PreparationDiscount=3;break;
  case "EX1_522":break;
  case "NEW1_004":g.BounceRogueUnits(g.Players.SelectMany(p=>p.Board).OrderBy(u=>u.Id).ToArray(),0);break;
  case "NEW1_014":var stealth=g.Unit(c.Target);stealth.Stealth=true;stealth.StealthExpirySeat=stealth.StealthUntilTurn=-1;break;
  case "NEW1_017":var murloc=g.Unit(c.Target);murloc.DamageTaken=murloc.MaxHealth;g.Buff(new MatchTarget(c.Seat,c.Summoned.Id),2,2);break;
 }}
}
public sealed partial class MatchEngine {
 internal void ExpireRogueStealth(int seat){foreach(var unit in Players.SelectMany(p=>p.Board).Where(u=>u.StealthExpirySeat==seat&&u.StealthUntilTurn<=Turn)){unit.Stealth=false;unit.StealthExpirySeat=unit.StealthUntilTurn=-1;}}
 public void Betrayal(MatchTarget target){var source=Unit(target);var board=Players[source.Owner].Board;int at=board.IndexOf(source),damage=AttackValue(source);foreach(var unit in board.Where((u,i)=>Math.Abs(i-at)==1).ToArray()){int dealt=Damage(new MatchTarget(unit.Owner,unit.Id),damage);if(dealt>0){if(source.Poison)unit.DamageTaken=unit.MaxHealth;if(!source.Silenced&&Card(source.CardId).BaseId=="CS2_033")Freeze(new MatchTarget(unit.Owner,unit.Id));}}}
 public void BounceRogueUnits(BattleUnit[] units,int adjustment){var batch=units.Where(u=>u!=null&&Players[u.Owner].Board.Contains(u)).ToArray();var positions=batch.ToDictionary(u=>u.Id,u=>Players[u.Owner].Board.IndexOf(u));foreach(var unit in batch)Players[unit.Owner].Board.Remove(unit);foreach(var unit in batch){var receiver=Players[unit.OriginalOwner];if(receiver.Hand.Count<10)receiver.Hand.Add(new HandCard{Id=nextId++,CardId=unit.CardId,CostAdjustment=adjustment});else{RecordGraveyard(unit.Owner,unit.OriginalOwner,unit.CardId,unit.Id,GraveyardReason.ReturnOverflow);unit.DamageTaken=unit.MaxHealth;Players[unit.Owner].Board.Insert(Math.Min(positions[unit.Id],Players[unit.Owner].Board.Count),unit);}}Cleanup();}
}
}
