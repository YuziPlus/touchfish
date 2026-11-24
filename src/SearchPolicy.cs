using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Touchfish {
public static class SearchPolicy {
 public static SearchAction Choose(AgentObservation view,RolloutStyle style,int seed){
  var actions=Actions(view);
  return actions.Length==0?null:Pick(view,actions,style,new SimulationRandom(seed));
 }

 internal static SearchAction[] Actions(AgentObservation view){
  if(view.Decision!=null)return view.Decision.Options.Select(option=>new SearchAction("decision:"+option.Id,null,view.Decision.Id,option.Id)).ToArray();
  var seen=new HashSet<string>(StringComparer.Ordinal);
  var list=new List<SearchAction>();
  foreach(var move in view.Moves){
   string key=ActionKey(view,move);
   if(seen.Add(key))list.Add(new SearchAction(key,move));
  }
  return list.ToArray();
 }

 internal static string ActionKey(AgentObservation view,AgentMove move){
  var source=move.Kind=="play"?Array.FindIndex(view.Hand,card=>card.Id==move.Source):
   move.Kind=="attack"&&move.Source!=0?Array.FindIndex(view.Self.Board,unit=>unit.Id==move.Source):-1;
  string sourceCard=source<0?null:move.Kind=="play"?view.Hand[source].CardId:view.Self.Board[source].CardId;
  string target="-";
  if(move.Target!=null){
   bool own=move.Target.Seat==view.Seat;
   var board=own?view.Self.Board:view.Enemy.Board;
   int slot=move.Target.Hero?-1:Array.FindIndex(board,unit=>unit.Id==move.Target.UnitId);
   target=(own?"S":"E")+slot+(slot>=0?":"+board[slot].CardId:"");
  }
  return move.Kind+"|"+source+"|"+sourceCard+"|"+target+"|"+move.Position+"|"+move.Choice;
 }

 // Per-path root observation. Entity references are canonicalized by hand/board
 // position, so sample-only runtime instance IDs cannot split an information set.
 internal static string InformationKey(AgentObservation view){
  var key=new StringBuilder();
  key.Append(view.ActiveSeat).Append('/').Append(view.Turn).Append('/').Append(view.CardsPlayed)
   .Append('/').Append(view.DeathsThisTurn).Append('/').Append(view.PreparationDiscount);
  AppendPlayer(key,view.Self);
  AppendPlayer(key,view.Enemy);
  foreach(var card in view.Hand)key.Append('|').Append(card.CardId).Append(':').Append(card.Cost).Append(':').Append(card.UndiscountedCost);
  foreach(string secret in view.OwnSecrets)key.Append("|s:").Append(secret);
  if(view.Decision!=null)foreach(var option in view.Decision.Options)key.Append("|d:").Append(option.Id);
  return key.ToString();
 }

 static void AppendPlayer(StringBuilder key,AgentPlayer player){
  key.Append('|').Append(player.ClassId).Append('/').Append(player.HeroFormId).Append('/').Append(player.HeroAbilityId)
   .Append('/').Append(player.Health).Append('/').Append(player.Armor).Append('/').Append(player.Mana).Append('/').Append(player.MaxMana)
   .Append('/').Append(player.HandCount).Append('/').Append(player.DeckCount).Append('/').Append(player.SecretCount)
   .Append('/').Append(player.WeaponId).Append('/').Append(player.WeaponAttack).Append('/').Append(player.WeaponDurability)
   .Append('/').Append(player.HeroAttack).Append('/').Append(player.HeroAttacks).Append('/').Append(player.Shadowform)
   .Append('/').Append(player.Overload).Append('/').Append(player.LockedMana).Append('/').Append(player.Fatigue).Append('/')
   .Append(player.NextDragonDiscount).Append('/').Append(player.PowerUsed).Append('/').Append(player.Frozen).Append('/').Append(player.Immune);
  foreach(var unit in player.Board){
   int flags=(unit.Taunt?1:0)|(unit.Shield?2:0)|(unit.Stealth?4:0)|(unit.Frozen?8:0)|(unit.Silenced?16:0)|
    (unit.Poison?32:0)|(unit.Immune?64:0)|(unit.Charge?128:0)|(unit.Windfury?256:0)|(unit.AbilityTargetProtected?512:0);
   key.Append(':').Append(unit.CardId).Append('/').Append(unit.Attack).Append('/').Append(unit.Health).Append('/').Append(unit.MaxHealth)
    .Append('/').Append(unit.SummonTurn).Append('/').Append(unit.AttacksUsed).Append('/').Append(unit.AttackLimit).Append('/').Append(unit.SpellDamage).Append('/').Append(flags);
  }
 }

 internal static string PublicResponse(AgentObservation view,SearchAction action){
  var move=action.SimulatedMove;
  if(move==null)return "response";
  if(move.Kind!="play")return move.Kind;
  var card=Array.Find(view.Hand,entry=>entry.Id==move.Source);
  return card==null||card.Secret?"play:secret":"play:"+card.CardId;
 }

 internal static SearchAction Pick(AgentObservation view,SearchAction[] actions,RolloutStyle style,SimulationRandom random){
  if(style==RolloutStyle.Random)return actions[random.Next(actions.Length)];
  SearchAction best=actions[0];double score=Double.NegativeInfinity;
  foreach(var action in actions){
   double candidate=Priority(view,action)+random.Next(1000)/4000.0;
   if(candidate>score){score=candidate;best=action;}
  }
  return best;
 }

 internal static double Priority(AgentObservation view,SearchAction action){
  if(action.OptionId!=null){
   var option=Array.Find(view.Decision.Options,candidate=>candidate.Id==action.OptionId);
   return option!=null&&option.Cost.HasValue?3-0.15*Math.Abs(view.Self.MaxMana-option.Cost.Value):2;
  }
  var move=action.SimulatedMove;
  if(move.Kind=="end")return -1;
  bool enemyTarget=move.Target!=null&&move.Target.Seat!=view.Seat;
  bool enemyFace=enemyTarget&&move.Target.Hero;
  int life=view.Enemy.Health+view.Enemy.Armor;
  if(enemyFace&&!view.Enemy.Immune&&view.Enemy.SecretCount==0&&move.Damage>=life)return 20;
  if(move.Kind=="attack"){
   if(enemyFace)return 2+Math.Min(move.Damage,10)*0.15;
   var target=Array.Find(view.Enemy.Board,unit=>unit.Id==move.Target.UnitId);
   var source=Array.Find(view.Self.Board,unit=>unit.Id==move.Source);
   if(target==null)return 1;
   bool kills=!target.Immune&&!target.Shield&&(move.Damage>=target.Health||source!=null&&source.Poison);
   bool survives=source==null||source.Immune||source.Shield||target.Attack<source.Health&&!target.Poison;
   return 1+(kills?1.7:0)+(survives?0.7:0)+(target.Taunt?0.9:0);
  }
  if(move.Kind=="power")return 0.7+(enemyTarget?0.5:0);
  if(move.Kind=="play"){
   var card=Array.Find(view.Hand,hand=>hand.Id==move.Source);
   double score=1.4+Math.Min(move.Cost,8)*0.2+(move.Position>=0?0.8:0);
   if(enemyTarget&&!enemyFace)score+=0.4;
   if(card!=null&&card.Type=="SPELL"&&move.Target==null&&move.Cost==0)score-=0.5;
   return score;
  }
  return 0;
 }

 public static double Evaluate(AgentObservation view){
  double value=Strength(view.Self)-Strength(view.Enemy);
  if(view.Self.Health+view.Self.Armor<=view.Enemy.Board.Sum(unit=>unit.Frozen?0:unit.Attack)&&!view.Self.Board.Any(unit=>unit.Taunt))value-=2;
  if(view.Enemy.Health+view.Enemy.Armor<=view.Self.Board.Sum(unit=>unit.Frozen?0:unit.Attack)&&!view.Enemy.Board.Any(unit=>unit.Taunt))value+=2;
  return 0.5+0.48*Math.Tanh(value/13);
 }

 static double Strength(AgentPlayer player){
  return 0.35*(Math.Min(30,player.Health+player.Armor))+player.HandCount*0.85+player.DeckCount*0.025+
   player.MaxMana*0.24+player.Mana*0.05+player.WeaponAttack*Math.Min(2,player.WeaponDurability)*0.4+
   player.SecretCount*0.55+player.Board.Sum(unit=>0.6+unit.Attack*(unit.Frozen?0.25:0.65)+unit.Health*0.45+
    (unit.Shield?0.9:0)+(unit.Taunt?0.65:0)+(unit.Poison?0.7:0)+(unit.Windfury?0.4:0));
 }
}
}
