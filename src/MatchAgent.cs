using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
// Agents receive detached, explicitly selected information; never an authoritative engine.
public interface IMatchAgent {
 int[] Mulligan(AgentObservation view);
 AgentMove Choose(AgentObservation view);
}
public interface IDecisionMatchAgent : IMatchAgent {
 string ChooseDecision(DecisionObservation decision);
}
public sealed class AgentMove {
 public string Kind,Key,Choice;public int Source,Cost,Position=-1,Damage;public MatchTarget Target;
}
public sealed class AgentCard {public int Id,Cost,BaseCost,UndiscountedCost;public string CardId,Key,Type;public bool Charge,Secret;}
// Owner-only unordered composition; no card positions, instance IDs or opponent deck information.
public sealed class AgentDeckCard {public string Key,Type;public int Count,Cost;}
public sealed class AgentUnit {
 public int Id,Attack,Health,MaxHealth,SpellDamage,AttacksRemaining,Cost;
 public int SummonTurn,AttacksUsed,AttackLimit;
 public string CardId,Key,Race;
 public bool Charge,AbilityTargetProtected;
 public bool Frozen,Shield,Taunt,Silenced,Stealth,Poison,Windfury,Immune;
}
public sealed class AgentPlayer {
 public int Health,Armor,Mana,MaxMana,DeckCount,HandCount,HeroAttack,HeroMaxHealth,Fatigue,SecretCount,WeaponAttack,WeaponDurability,HeroAttacks;public bool WeaponWindfury;
 public string ClassId,HeroFormId,HeroAbilityId,HeroPowerTarget,WeaponId;
 public int HeroPowerCost,Shadowform,LockedMana,Overload,NextDragonDiscount;
 public bool Frozen,Immune,PowerUsed;
 public AgentUnit[] Board;
}
public sealed class AgentObservation {
 public int Seat,Turn,FirstSeat,SpellPower,PowerCost,PowerDamage,CardsPlayed,PreparationDiscount;public bool CanAct;
 public DecisionObservation Decision;
 public int ActiveSeat,BoardSlots,HandLimit,ManaLimit,DeathsThisTurn;
 public bool MulliganComplete,Finished;
 public string CardPoolId;
 public AgentPlayer Self,Enemy;public AgentCard[] Hand;public string[] OwnSecrets;public AgentDeckCard[] RemainingDeck;
 public AgentMove[] Moves;
}
public static class AgentBridge {
 public static AgentObservation Observe(MatchEngine g,int seat){return Observe(g,seat,true,true);}
 internal static AgentObservation Observe(MatchEngine g,int seat,bool includeDeckComposition,bool includeMoves){
  if(seat<0||seat>1)throw new ArgumentOutOfRangeException("seat");
  var view=new AgentObservation{
   Seat=seat,ActiveSeat=g.Active,Turn=g.Turn,FirstSeat=g.FirstSeat,
   Decision=g.ObserveDecision(seat),
   MulliganComplete=g.MulliganComplete,Finished=g.Finished,
   CanAct=!g.HasPendingDecision&&g.MulliganComplete&&!g.Finished&&g.Active==seat&&!g.AnimationPlaying&&!g.TurnClockExpired,
   CardPoolId=g.CardPoolId,BoardSlots=g.Mode.BoardSlots,HandLimit=g.Mode.HandLimit,ManaLimit=g.Mode.ManaLimit,
   DeathsThisTurn=g.DeathsThisTurn,CardsPlayed=g.CardsPlayed,PreparationDiscount=g.Players[seat].PreparationDiscount,
   SpellPower=g.SpellPower(seat),PowerCost=g.HeroPowerCost(seat),PowerDamage=g.AbilityMultiplier(seat),
   Self=Player(g,seat),Enemy=Player(g,1-seat),OwnSecrets=g.AgentOwnSecrets(seat)
  };
  view.Hand=g.Players[seat].Hand.Select(h=>new AgentCard{Id=h.Id,CardId=h.CardId,Cost=g.HandCost(seat,h),BaseCost=g.Card(h.CardId).Cost,UndiscountedCost=g.HandCostWithoutPreparation(seat,h),Key=MatchRules.EffectKey(g.Card(h.CardId)),Type=g.Card(h.CardId).Type,Charge=MatchEngine.Has(g.Card(h.CardId),"CHARGE"),Secret=MatchEngine.Has(g.Card(h.CardId),"SECRET")}).ToArray();
  view.RemainingDeck=!includeDeckComposition?new AgentDeckCard[0]:g.Players[seat].Deck.Where(id=>id!="HIDDEN").Select(id=>g.Card(id)).GroupBy(c=>new{Key=MatchRules.EffectKey(c),Type=c.Type,Cost=c.Cost}).OrderBy(group=>group.Key.Key,StringComparer.Ordinal).ThenBy(group=>group.Key.Cost).ThenBy(group=>group.Key.Type,StringComparer.Ordinal).Select(group=>new AgentDeckCard{Key=group.Key.Key,Type=group.Key.Type,Cost=group.Key.Cost,Count=group.Count()}).ToArray();
  view.Moves=includeMoves&&view.CanAct?LegalMoves(g,seat,view).ToArray():new AgentMove[0];
  return view;
 }
 static IEnumerable<AgentMove> LegalMoves(MatchEngine game,int seat,AgentObservation view){
  var player=game.Players[seat];
  var targets=game.Characters(0).Concat(game.Characters(1)).ToArray();
  foreach(var hand in player.Hand){
   var card=game.Card(hand.CardId);
   int cost=game.HandCost(seat,hand);
   if(cost>player.Mana||!game.CanUseCard(seat,card,hand)||!game.Rules.Supports(card))continue;
   bool minion=card.Type=="MINION";
   if(minion&&player.Board.Count>=game.Mode.BoardSlots)continue;
   var options=game.Rules.Choices(card,game,seat);
   var choices=options.Length==0?new string[]{null}:options.Select(option=>option.Key).ToArray();
   foreach(string choice in choices){
    string mask=game.Rules.Target(card,choice,game,seat);
    var legal=mask=="NONE"?new MatchTarget[]{null}:targets.Where(target=>Valid(game,seat,target,mask,card.Type=="SPELL")).ToArray();
    if(legal.Length==0&&minion)legal=new MatchTarget[]{null};
    foreach(var target in legal){
     if(!ValidPlay(game,seat,hand,card,cost,target,choice))continue;
     // Offer the previous default placement first for agents that break ties by move order.
     int last=player.Board.Count;
     yield return PlayMove(hand,card,cost,target,choice,minion?last:-1);
     if(minion)for(int position=0;position<last;position++)
      yield return PlayMove(hand,card,cost,target,choice,position);
    }
   }
  }
  bool taunt=game.Players[1-seat].Board.Any(unit=>unit.Taunt&&!unit.Stealth);
  var attackTargets=game.Characters(1-seat).Where(target=>Valid(game,seat,target,"ENEMY_CHARACTER",false)
   &&(!taunt||!target.Hero&&game.Unit(target).Taunt)).ToArray();
  foreach(var unit in player.Board.Where(unit=>game.CanAttack(seat,unit)==null))
   foreach(var target in attackTargets.Where(t=>!t.Hero||unit.Silenced||game.Card(unit.CardId).BaseId!="AT_125"))
    yield return new AgentMove{Kind="attack",Source=unit.Id,Target=CopyTarget(target),Damage=game.AttackValue(unit)};
  if(game.CanHeroAttack(seat)==null)
   foreach(var target in attackTargets)
    yield return new AgentMove{Kind="attack",Target=CopyTarget(target),Damage=game.HeroAttack(seat)};
  if(game.HeroPowerAvailability(seat)==null){
   string mask=game.HeroPowerTargetMask(seat);
   if(player.HeroAbilityId=="tgt:upgraded-SHAMAN"){foreach(var id in MatchEngine.TournamentTotems)yield return new AgentMove{Kind="power",Cost=view.PowerCost,Choice=id};}
   else if(mask=="NONE")yield return new AgentMove{Kind="power",Cost=view.PowerCost};
   else foreach(var target in targets.Where(target=>Valid(game,seat,target,mask,true)))
    yield return new AgentMove{Kind="power",Cost=view.PowerCost,Target=CopyTarget(target),Damage=TargetedPowerDamage(game,seat,view)};
  }
  yield return new AgentMove{Kind="end"};
 }
 static int TargetedPowerDamage(MatchEngine game,int seat,AgentObservation view){
  var player=game.Players[seat];
  switch(player.HeroAbilityId){
   case "tgt:basic-MAGE":return game.TournamentPowerDamage(seat,1);
   case "tgt:basic-HUNTER":case "tgt:upgraded-MAGE":case "tgt:lightning-jolt":return game.TournamentPowerDamage(seat,2);
   case "tgt:upgraded-HUNTER":return game.TournamentPowerDamage(seat,3);
  }
  if(HeroReplacementRules.Ability(player.HeroAbilityId)!=null)return 0;
  if(player.Shadowform>0)return game.TournamentPowerDamage(seat,player.Shadowform+1);
  if(player.ClassId=="HUNTER")return game.TournamentPowerDamage(seat,2);
  return player.ClassId=="MAGE"?game.TournamentPowerDamage(seat,1):0;
 }
 static MatchTarget CopyTarget(MatchTarget target){
  return target==null?null:new MatchTarget(target.Seat,target.UnitId);
 }
 static AgentMove PlayMove(HandCard hand,CardRecord card,int cost,MatchTarget target,string choice,int position){
  return new AgentMove{Kind="play",Key=MatchRules.EffectKey(card),Source=hand.Id,Cost=cost,Target=CopyTarget(target),Position=position,Choice=choice};
 }
 static bool ValidPlay(MatchEngine game,int seat,HandCard hand,CardRecord card,int cost,MatchTarget target,string choice){
  try{game.ValidatePlay(seat,hand,card,cost,target,choice);return true;}
  catch(InvalidOperationException){return false;}
 }
 static bool Valid(MatchEngine g,int seat,MatchTarget target,string mask,bool ability){try{g.ValidateTarget(seat,target,mask,ability);return true;}catch(InvalidOperationException){return false;}}
 static AgentPlayer Player(MatchEngine game,int seat){
  var player=game.Players[seat];
  return new AgentPlayer{
   ClassId=player.ClassId,HeroFormId=player.HeroFormId,HeroAbilityId=player.HeroAbilityId,
   HeroPowerTarget=game.HeroPowerTargetMask(seat),HeroPowerCost=game.HeroPowerCost(seat),Shadowform=player.Shadowform,
   Health=player.Health,HeroMaxHealth=game.HeroHealthLimit(seat),Armor=player.Armor,
   Mana=player.Mana,MaxMana=player.MaxMana,LockedMana=player.LockedMana,Overload=player.Overload,
   NextDragonDiscount=player.NextDragonDiscount,Fatigue=player.Fatigue,SecretCount=player.SecretCount,
   DeckCount=player.Deck.Count,HandCount=player.HandCount,
   WeaponId=player.WeaponId,WeaponAttack=game.WeaponAttackValue(seat),WeaponDurability=player.WeaponDurability,
   HeroAttacks=player.HeroAttacks,WeaponWindfury=player.WeaponWindfury,HeroAttack=game.HeroAttack(seat),
   Frozen=player.Frozen,Immune=player.SecretImmuneTurn==game.Turn||GvgClassRules.HeroImmune(game,seat),
   PowerUsed=game.PowerSpent(seat),Board=player.Board.Select(unit=>ObserveUnit(game,seat,unit)).ToArray()
  };
 }
 static AgentUnit ObserveUnit(MatchEngine game,int seat,BattleUnit unit){
  var card=game.Card(unit.CardId);
  int attackLimit=game.MinionAttackLimit(unit);
  bool targetProtected=GvgClassRules.Protected(game,unit)||
   !unit.Silenced&&(MatchEngine.Has(card,"CANT_BE_TARGETED_BY_ABILITIES")||MatchEngine.Has(card,"ELUSIVE"));
  return new AgentUnit{
   Id=unit.Id,CardId=card.Id,Key=MatchRules.EffectKey(card),Race=unit.Race,Cost=card.Cost,
   Attack=game.AttackValue(unit),Health=unit.Health,MaxHealth=unit.MaxHealth,SpellDamage=unit.SpellDamage,
   SummonTurn=unit.SummonTurn,AttacksUsed=unit.AttacksUsed,AttackLimit=attackLimit,Charge=game.HasCharge(unit),
   AttacksRemaining=game.CanAttack(seat,unit)==null?Math.Max(0,attackLimit-unit.AttacksUsed):0,
   AbilityTargetProtected=targetProtected,Frozen=unit.Frozen,Shield=unit.Shield,Taunt=unit.Taunt,
   Silenced=unit.Silenced,Stealth=unit.Stealth,Poison=unit.Poison,Immune=unit.ImmuneUntilTurn==game.Turn,
   Windfury=game.HasWindfury(unit)
  };
 }
 public static ActionResult SubmitDecision(MatchEngine game,int seat,int decisionId,string optionId){
  return game.SubmitDecision(seat,decisionId,optionId);
 }
 public static ActionResult Execute(MatchEngine g,int seat,AgentMove move){
  if(move==null||g.Active!=seat||!g.MulliganComplete||g.Finished)return new ActionResult{Message="人机操作已失效。"};
  switch(move.Kind){case "play":return g.Play(seat,move.Source,move.Target,move.Position,move.Choice);case "attack":return g.Attack(seat,move.Source,move.Target);case "power":return g.HeroPower(seat,move.Target,move.Choice);case "end":return g.EndTurn();default:return new ActionResult{Message="未知人机操作。"};}
 }
}
public sealed partial class MatchEngine {
 internal string[] AgentOwnSecrets(int seat){return secrets.Where(s=>s.Seat==seat).Select(s=>MatchRules.EffectKey(Card(s.CardId))).ToArray();}
}
}
