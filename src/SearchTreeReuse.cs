using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Touchfish {
internal static class SearchTreeReuse {
 static readonly ConditionalWeakTable<MatchEngine,object> matches=new ConditionalWeakTable<MatchEngine,object>();
 internal static object MatchToken(MatchEngine game){return matches.GetValue(game,_=>new object());}

 internal static string Configuration(SearchBudget budget){
  return "p32-p2-fixed-ledger-v1|"+String.Join("|",new[]{budget.MaxTreeDepth,budget.MaxActions,budget.MaxTurns,budget.MaxNodes,
   budget.MaxConsecutiveFailures,(int)budget.Rollout,budget.ProgressiveWidening?1:0}.Select(n=>n.ToString(CultureInfo.InvariantCulture)))+
   "|"+budget.Exploration.ToString("R",CultureInfo.InvariantCulture)+"|"+
   budget.WideningConstant.ToString("R",CultureInfo.InvariantCulture)+"|"+budget.WideningExponent.ToString("R",CultureInfo.InvariantCulture);
 }

 internal static string History(PublicHistoryEvent[] events,int count){
  var key=new StringBuilder();
  for(int i=0;i<count;i++){
   var e=events[i];
   key.Append((int)e.Kind).Append(':').Append(e.Seat).Append(':').Append(e.CardId).Append(':')
    .Append(e.Count).Append(':').Append(e.Adjustment).Append(':').Append(e.VisibleTo).Append('|');
  }
  return key.ToString();
 }

 // Certify the posterior using P2's modeled ledgers, never the live deck.
 // One valid ledger per seat avoids altering weights or source ambiguity.
 internal static string BeliefKey(DeckBelief belief){
  if(belief==null||!belief.Ready||!belief.EnemyDeckKnown||belief.Fixed.Any(states=>states==null||states.Count!=1))return null;
  var info=belief.Information;
  var key=new StringBuilder(info.PoolId);
  for(int seat=0;seat<2;seat++){
   var state=belief.Fixed[seat][0];
   if(state.SecretSlots.Count!=0||state.ForcedDeck.Count!=0)return null;
   var bag=new Dictionary<string,int>(state.Bag);
   var known=state.KnownHand.ToList();
   key.Append('|').Append(info.Classes[seat]).Append(':').Append(state.Deck);
   if(seat==info.Seat){
    foreach(var card in info.OwnHand){
     int index=known.FindIndex(entry=>entry.CardId==card.CardId&&entry.CostAdjustment==card.CostAdjustment);
     if(index>=0)known.RemoveAt(index);
     else if(!BeliefLedger.Remove(bag,card.CardId))return null;
    }
    if(known.Count!=0||bag.Values.Sum()!=state.Deck)return null;
    foreach(string id in info.KnownOwnTop)key.Append("T:").Append(id).Append(';');
   }else{
    key.Append(':').Append(state.Hand);
    foreach(var card in known)
     key.Append("H:").Append(card.CardId).Append(':').Append(card.CostAdjustment).Append(':').Append(card.AttackBonus)
      .Append(':').Append(card.Generated).Append(';');
   }
   foreach(var pair in bag.OrderBy(entry=>entry.Key,StringComparer.Ordinal))
    key.Append(',').Append(pair.Key).Append(':').Append(pair.Value);
  }
  return key.ToString();
 }

 internal static long EdgeBytes(string key){return 112+2L*key.Length;}
 internal static long KeyBytes(string key){return key==null?0:24+2L*key.Length;}
 internal static void Measure(SearchNode root,out int nodes,out long bytes){
  nodes=0;bytes=0;
  if(root==null)return;
  var pending=new Stack<SearchNode>();
  pending.Push(root);
  while(pending.Count>0){
   var node=pending.Pop();nodes++;bytes+=128+KeyBytes(node.CompatibilityKey);
   foreach(var entry in node.Edges){
    bytes+=EdgeBytes(entry.Key);
    var outcomes=entry.Value.Outcomes;
    if(outcomes==null)continue;
    bytes+=104;
    foreach(var outcome in outcomes){
     bytes+=32+2L*outcome.Key.Length;
     pending.Push(outcome.Value);
    }
   }
  }
 }
}

public sealed partial class MatchEngine {
 internal long SearchStateVersion{get;private set;}
 internal void MarkSearchStateChanged(){if(!headless)SearchStateVersion++;}

 internal string ReuseObservationKey(AgentObservation view,SearchAction[] actions=null){
  var key=new StringBuilder(SearchPolicy.InformationKey(view));
  key.Append('|').Append(Mode.Id).Append('|').Append(view.CardPoolId).Append('|').Append(view.FirstSeat)
   .Append('|').Append(view.MulliganComplete).Append('|').Append(view.Finished).Append('|').Append(view.CanAct);
  foreach(var player in Players){
   key.Append('|').Append(player.SpellCostPenalty).Append(':').Append(player.SpellCostPenaltyUntilTurn)
    .Append(':').Append(player.FreeSecretThisTurn).Append(':').Append(player.FreeSpellsUntilTurn)
    .Append(':').Append(player.CommandingShoutTurn).Append(':').Append(player.SecretImmuneTurn)
    .Append(':').Append(player.FrozenTurn).Append(':').Append(player.HeroMaxHealth).Append(':').Append(player.HeroDestroyed)
    .Append(':').Append(player.MinionsPlayedThisTurn).Append(':').Append(player.PreparationDiscount);
   foreach(var unit in player.Board){
    key.Append('|').Append(unit.OriginalOwner).Append(':').Append(unit.BaseAttack).Append(':').Append(unit.BaseHealth)
     .Append(':').Append(unit.DamageTaken).Append(':').Append(unit.AuraHealth).Append(':').Append(unit.FrozenTurn);
    foreach(var item in unit.Enchantments.Items)
     key.Append(':').Append(item.Kind).Append(',').Append(item.Amount).Append(',').Append(item.Attack).Append(',')
      .Append(item.Health).Append(',').Append(item.ExpiresTurn).Append(',').Append(item.RemovedBySilence);
   }
  }
  foreach(var card in Players[view.Seat].Hand)
   key.Append("|h:").Append(card.CostAdjustment).Append(':').Append(card.AttackBonus).Append(':').Append(card.Generated);
  if(view.Decision!=null&&decisionFrame!=null)
   key.Append("|frame:").Append(decisionFrame.CardId).Append(':').Append(decisionFrame.NextStep);
  foreach(var action in (actions??SearchPolicy.Actions(view)).Select(action=>action.ActionKey).OrderBy(value=>value,StringComparer.Ordinal))
   key.Append("|a:").Append(action);
  return key.ToString();
 }

 internal string ReuseTransitionRestriction(int seat,SearchAction action){
  if(Players.Any(player=>player.SecretCount!=0))return "secrets";
  if(Players.Any(player=>player.WeaponId!=null&&!PlainReuseCard(Card(player.WeaponId))))return "uncertified-weapon";
  if(Players.SelectMany(player=>player.Board).Any(unit=>!PlainReuseCard(Card(unit.CardId))||
   unit.Enchantments.Items.Any(item=>item.Kind!="stats"&&item.Kind!="temporary-attack")))
   return "uncertified-board-effects";
  if(action.OptionId!=null){
   var selected=pendingDecision==null?null:pendingDecision.Candidates.FirstOrDefault(option=>option.Id==action.OptionId);
   return pendingDecision!=null&&pendingDecision.Seat==seat&&pendingDecision.Id==action.DecisionId&&
    selected!=null&&selected.Effect==DecisionEffect.GiveCard&&
    Rules.DynamicProgram(Card(decisionFrame.CardId)).Length>decisionFrame.NextStep?"":"uncertified-decision";
  }
  var move=action.SimulatedMove;
  if(move.Kind=="end")return "turn-boundary";
  if(move.Kind=="play"){
   var card=Players[seat].Hand.FirstOrDefault(entry=>entry.Id==move.Source);
   if(card==null)return "action-mismatch";
   var record=Card(card.CardId);
   return record.Id=="GAME_COIN"||record.Type=="MINION"&&PlainReuseCard(record)?"":"uncertified-card-effect";
  }
  if(move.Kind=="attack")return "";
  if(move.Kind=="power"){
   var p=Players[seat];
   if(p.HeroAbilityId!=null)return "uncertified-power";
   if(p.ClassId=="WARLOCK")return "draw";
   return p.ClassId=="WARRIOR"||p.ClassId=="DRUID"||p.ClassId=="MAGE"||p.ClassId=="PRIEST"||p.ClassId=="ROGUE"?"":"uncertified-power";
  }
  return "uncertified-action";
 }

 bool PlainReuseCard(CardRecord card){
  if(card.Id=="GAME_DAGGER")return true;
  if(card.Type!="MINION"&&card.Type!="WEAPON"||!String.IsNullOrEmpty(card.EffectId)||
   Rules.RegisteredIds.Contains(MatchRules.EffectKey(card))||Rules.Triggers.Any(trigger=>trigger.CardId==MatchRules.EffectKey(card)))return false;
  return Rules.Supports(card);
 }
}
}
