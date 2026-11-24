using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
// Full authoritative rules state at a completed resolution boundary. This is not a player view.
public sealed class SimulationSnapshot {
 readonly MatchEngine baseline;

 SimulationSnapshot(MatchEngine source){
  baseline=source.CopyForSimulation();
 }

 public static SimulationSnapshot Capture(MatchEngine source){
  if(source==null)throw new ArgumentNullException("source");
  return new SimulationSnapshot(source);
 }

 // A seed override replaces future random draws; existing deck order is retained.
 public FastSimulation Fork(int? seed=null){return new FastSimulation(baseline.CopyForSimulation(seed));}
}

public sealed partial class MatchEngine {
 readonly bool headless;
 long simulationClockMs;
 int resolvingActions;
 bool customSimulationCallbacks;

 internal MatchEngine CopyForSimulation(int? seed=null){
  ValidateSimulationBoundary();
  return new MatchEngine(this,seed);
 }

 void ValidateSimulationBoundary(){
  if(clockReplica)throw new InvalidOperationException("模拟快照需要房主的完整对局状态。");
  if(resolvingActions!=0||SpellImpactPending||damageListeners!=null||damageTriggers!=null||
     drainingSummons||(!HasPendingDecision&&summonTriggers!=null&&summonTriggers.Count!=0)||deferSummonDepth!=0||spellSource!=-1||heroAttackImmune)
   throw new InvalidOperationException("请在当前操作结算完成后创建模拟快照。");
  if(customSimulationCallbacks||Mode.TurnStarted!=null||Mode.TurnEnded!=null||Mode.EvaluateWinner!=null||Rules.HasCustomSimulationCallbacks)
   throw new InvalidOperationException("模拟快照需要内置规则或不含自定义结算回调的模式。");
  foreach(var player in Players){
   string ability=player.HeroAbilityId;
   if(ability!=null&&ability!=HeroReplacementRules.InfernoId&&ability!=BlackrockRules.DieInsect)
    throw new InvalidOperationException("模拟快照需要内置英雄技能。");
  }
 }

 MatchEngine(MatchEngine source,int? seed,int viewer=-1){
  headless=true;
  // Opaque branches share their detached, read-only definitions.
  Catalog=source.headless?source.Catalog:source.Catalog.CopyForSimulation();
  Rules=source.headless?source.Rules:source.Rules.CopyForSimulation(Catalog);
  Mode=source.Mode;
  TestMode=source.TestMode;
  CardPoolId=source.CardPoolId;
  Seed=seed??source.Seed;
  random=seed.HasValue?new SimulationRandom(seed.Value):source.random.Copy();
  secretRevealVersion=viewer<0?source.secretRevealVersion:0;
  FirstSeat=source.FirstSeat;
  Active=source.Active;
  Turn=source.Turn;
  Winner=source.Winner;
  CardsPlayed=source.CardsPlayed;
  DeathsThisTurn=source.DeathsThisTurn;
  if(viewer<0)nextId=source.nextId;
  openingEmotesPlayed=source.openingEmotesPlayed;
  defeatEmotePlayed=source.defeatEmotePlayed;
  ClockNowMs=()=>simulationClockMs;
  simulationClockMs=source.simulationClockMs;
  clockEnabled=source.clockEnabled;
  clockTurn=source.clockTurn;
  clockSequence=source.clockSequence;
  clockDeadline=!clockEnabled?0:source.headless?source.clockDeadline:Math.Max(0,source.clockDeadline-source.ClockNowMs());

  for(int seat=0;seat<Players.Length;seat++){
   var original=source.Players[seat];
   var copy=Players[seat];
   PublicPlayerState.From(original).Apply(copy);
   if(viewer<0)copy.Deck.AddRange(original.Deck);
   if(viewer<0||viewer==seat)foreach(var card in original.Hand)copy.Hand.Add(card.CopyForSimulation());
   foreach(var unit in original.Board)copy.Board.Add(unit.CopyForSimulation());
   if(viewer<0){
    PrivateDeckPreview[seat]=(string[])source.PrivateDeckPreview[seat].Clone();
    blackrockGeneratedDeck[seat].UnionWith(source.blackrockGeneratedDeck[seat]);
   }
  }
  foreach(var secret in source.secrets)if(viewer<0||secret.Seat==viewer)secrets.Add(new SecretInstance{Id=secret.Id,Seat=secret.Seat,CardId=secret.CardId});
  if(viewer<0)foreach(var item in source.comboReturns)comboReturns.Add(new ComboReturn{Seat=item.Seat,Due=item.Due,Card=item.Card.CopyForSimulation()});
  // Entries are immutable. Each branch owns its history list.
  if(viewer<0)graveyard.AddRange(source.graveyard);
  else{
   // Only minion deaths are rule inputs and public. Discarded private deck cards
   // and unrevealed spells are deliberately absent from the hypothetical history.
   foreach(var entry in source.graveyard){
    if(entry.Reason!=GraveyardReason.MinionDied)continue;
    graveyard.Add(new GraveyardEntry(graveyard.Count+1,entry.Turn,entry.Seat,entry.OriginalOwner,
     entry.CardId,entry.CardType,entry.InstanceId,entry.Reason,entry.Attack,entry.MaxHealth));
   }
   nextId=1+Players.SelectMany(p=>p.Board.Select(unit=>unit.Id).Concat(p.Hand.Select(card=>card.Id)).Concat(new[]{p.WeaponInstanceId}))
    .Concat(secrets.Select(secret=>secret.Id)).Concat(graveyard.Select(entry=>entry.InstanceId))
    .Concat(source.HasPendingDecision?new[]{source.pendingDecision.Id}:new int[0]).Max();
  }
  if(source.HasPendingDecision)CopyPausedResolution(source);
 }

 void CopyPausedResolution(MatchEngine source){
  var units=new Dictionary<BattleUnit,BattleUnit>();
  for(int seat=0;seat<Players.Length;seat++)
   for(int i=0;i<Players[seat].Board.Count;i++)units.Add(source.Players[seat].Board[i],Players[seat].Board[i]);
  Func<BattleUnit,BattleUnit> copyUnit=unit=>{
   BattleUnit copy;
   if(!units.TryGetValue(unit,out copy)){copy=unit.CopyForSimulation();units.Add(unit,copy);}
   return copy;
  };
  pendingDecision=source.pendingDecision.Copy();
  decisionFrame=source.decisionFrame.Copy(copyUnit);
  if(source.summonTriggers!=null&&source.summonTriggers.Count!=0){
   summonTriggers=new Queue<SummonNotification>(source.summonTriggers.Count);
   foreach(var item in source.summonTriggers)
    summonTriggers.Enqueue(new SummonNotification{Seat=item.Seat,Unit=copyUnit(item.Unit),Listeners=item.Listeners.Select(copyUnit).ToArray()});
  }
 }

 internal bool AdvanceSimulationTime(int milliseconds){
  if(milliseconds<0)throw new ArgumentOutOfRangeException("milliseconds");
  SyncTurnClock();
  simulationClockMs+=milliseconds;
  return TryEndTimedTurn();
 }
}
}
