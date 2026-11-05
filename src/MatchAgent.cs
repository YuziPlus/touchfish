using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
// Agents receive detached, explicitly selected information; never an authoritative engine.
public interface IMatchAgent {
 int[] Mulligan(AgentObservation view);
 AgentMove Choose(AgentObservation view);
}
public sealed class AgentMove {
 public string Kind,Key,Choice;public int Source,Cost,Position=-1,Damage;public MatchTarget Target;
}
public sealed class AgentCard {public int Id,Cost,BaseCost,UndiscountedCost;public string Key,Type;public bool Charge;}
// Owner-only unordered composition; no card positions, instance IDs or opponent deck information.
public sealed class AgentDeckCard {public string Key,Type;public int Count,Cost;}
public sealed class AgentUnit {
 public int Id,Attack,Health,MaxHealth,SpellDamage,AttacksRemaining,Cost;public string Key;
 public bool Frozen,Shield,Taunt,Silenced,Stealth,Poison,Windfury,Immune;
}
public sealed class AgentPlayer {
 public int Health,Armor,Mana,MaxMana,DeckCount,HandCount,HeroAttack,HeroMaxHealth,Fatigue,SecretCount,WeaponAttack,WeaponDurability,HeroAttacks;public bool WeaponWindfury;
 public string ClassId;public bool Frozen,Immune,PowerUsed;
 public AgentUnit[] Board;
}
public sealed class AgentObservation {
 public int Seat,Turn,FirstSeat,SpellPower,PowerCost,PowerDamage,CardsPlayed,PreparationDiscount;public bool CanAct;
 public AgentPlayer Self,Enemy;public AgentCard[] Hand;public string[] OwnSecrets;public AgentDeckCard[] RemainingDeck;
 public AgentMove[] Moves;
}
public static class AgentBridge {
 public static AgentObservation Observe(MatchEngine g,int seat){
  if(seat<0||seat>1)throw new ArgumentOutOfRangeException("seat");
  var view=new AgentObservation{Seat=seat,Turn=g.Turn,FirstSeat=g.FirstSeat,CardsPlayed=g.CardsPlayed,PreparationDiscount=g.Players[seat].PreparationDiscount,CanAct=g.MulliganComplete&&!g.Finished&&g.Active==seat&&!g.AnimationPlaying&&!g.TurnClockExpired,SpellPower=g.SpellPower(seat),PowerCost=g.HeroPowerCost(seat),PowerDamage=g.AbilityMultiplier(seat),Self=Player(g,seat),Enemy=Player(g,1-seat),OwnSecrets=g.AgentOwnSecrets(seat)};
  view.Hand=g.Players[seat].Hand.Select(h=>new AgentCard{Id=h.Id,Cost=g.HandCost(seat,h),BaseCost=g.Card(h.CardId).Cost,UndiscountedCost=g.HandCostWithoutPreparation(seat,h),Key=MatchRules.EffectKey(g.Card(h.CardId)),Type=g.Card(h.CardId).Type,Charge=MatchEngine.Has(g.Card(h.CardId),"CHARGE")}).ToArray();
  view.RemainingDeck=g.Players[seat].Deck.Where(id=>id!="HIDDEN").Select(id=>g.Card(id)).GroupBy(c=>new{Key=MatchRules.EffectKey(c),Type=c.Type,Cost=c.Cost}).OrderBy(group=>group.Key.Key,StringComparer.Ordinal).ThenBy(group=>group.Key.Cost).ThenBy(group=>group.Key.Type,StringComparer.Ordinal).Select(group=>new AgentDeckCard{Key=group.Key.Key,Type=group.Key.Type,Cost=group.Key.Cost,Count=group.Count()}).ToArray();
  var moves=new List<AgentMove>();if(view.CanAct){
   var targets=g.Characters(0).Concat(g.Characters(1)).ToArray();
   foreach(var hand in g.Players[seat].Hand){var card=g.Card(hand.CardId);int cost=g.HandCost(seat,hand);if(cost>g.Players[seat].Mana||!g.CanUseCard(seat,card,hand)||!g.Rules.Supports(card)||card.Type=="MINION"&&g.Players[seat].Board.Count>=g.Mode.BoardSlots)continue;var effect=g.Rules.Effect(card);if(effect.Requirement!=null&&effect.Requirement(g,seat)!=null)continue;
    var choices=g.Rules.IsChoice(card,g,seat)?g.Rules.Choices(card,g,seat).Select(c=>c.Key).ToArray():new string[]{null};
    foreach(string choice in choices){string mask=g.Rules.Target(card,choice,ComboRules.Active(g));var legal=mask=="NONE"?new MatchTarget[]{null}:targets.Where(t=>Valid(g,seat,t,mask,card.Type=="SPELL")).ToArray();if(legal.Length==0&&card.Type=="MINION")legal=new MatchTarget[]{null};foreach(var t in legal)moves.Add(new AgentMove{Kind="play",Key=MatchRules.EffectKey(card),Source=hand.Id,Cost=cost,Target=t==null?null:new MatchTarget(t.Seat,t.UnitId),Position=card.Type=="MINION"?g.Players[seat].Board.Count:-1,Choice=choice});}
   }
   bool taunt=g.Players[1-seat].Board.Any(u=>u.Taunt&&!u.Stealth);
   foreach(var unit in g.Players[seat].Board.Where(u=>g.CanAttack(seat,u)==null))foreach(var target in g.Characters(1-seat).Where(t=>Valid(g,seat,t,"ENEMY_CHARACTER",false)&&(!taunt||!t.Hero&&g.Unit(t).Taunt)))moves.Add(new AgentMove{Kind="attack",Source=unit.Id,Target=new MatchTarget(target.Seat,target.UnitId),Damage=g.AttackValue(unit)});
   if(g.CanHeroAttack(seat)==null)foreach(var target in g.Characters(1-seat).Where(t=>Valid(g,seat,t,"ENEMY_CHARACTER",false)&&(!taunt||!t.Hero&&g.Unit(t).Taunt)))moves.Add(new AgentMove{Kind="attack",Target=new MatchTarget(target.Seat,target.UnitId),Damage=g.HeroAttack(seat)});
   if(!g.Players[seat].PowerUsed&&g.Players[seat].Mana>=view.PowerCost&&g.Players[seat].ClassId=="MAGE")foreach(var target in targets.Where(t=>Valid(g,seat,t,"ANY_CHARACTER",true)))moves.Add(new AgentMove{Kind="power",Target=new MatchTarget(target.Seat,target.UnitId),Cost=view.PowerCost,Damage=view.PowerDamage});
   if(!g.Players[seat].PowerUsed&&g.Players[seat].Mana>=view.PowerCost&&g.Players[seat].ClassId=="WARLOCK"&&String.IsNullOrEmpty(g.Players[seat].HeroAbilityId))moves.Add(new AgentMove{Kind="power",Cost=view.PowerCost});
   if(!g.Players[seat].PowerUsed&&g.Players[seat].Mana>=view.PowerCost&&g.Players[seat].ClassId=="ROGUE"&&String.IsNullOrEmpty(g.Players[seat].HeroAbilityId))moves.Add(new AgentMove{Kind="power",Cost=view.PowerCost});
   moves.Add(new AgentMove{Kind="end"});
  }view.Moves=moves.ToArray();return view;
 }
 static bool Valid(MatchEngine g,int seat,MatchTarget target,string mask,bool ability){try{g.ValidateTarget(seat,target,mask,ability);return true;}catch(InvalidOperationException){return false;}}
 static AgentPlayer Player(MatchEngine g,int seat){var p=g.Players[seat];return new AgentPlayer{ClassId=p.ClassId,WeaponAttack=g.WeaponAttackValue(seat),WeaponDurability=p.WeaponDurability,HeroAttacks=p.HeroAttacks,WeaponWindfury=p.WeaponWindfury,Health=p.Health,HeroMaxHealth=g.HeroHealthLimit(seat),Fatigue=p.Fatigue,SecretCount=p.SecretCount,Armor=p.Armor,Mana=p.Mana,MaxMana=p.MaxMana,DeckCount=p.Deck.Count,HandCount=p.HandCount,HeroAttack=g.HeroAttack(seat),Frozen=p.Frozen,Immune=p.SecretImmuneTurn==g.Turn||GvgClassRules.HeroImmune(g,seat),PowerUsed=p.PowerUsed,Board=p.Board.Select(u=>new AgentUnit{Id=u.Id,Cost=g.Card(u.CardId).Cost,AttacksRemaining=g.CanAttack(seat,u)==null?Math.Max(0,g.MinionAttackLimit(u)-u.AttacksUsed):0,Key=MatchRules.EffectKey(g.Card(u.CardId)),Attack=g.AttackValue(u),Health=u.Health,MaxHealth=u.MaxHealth,SpellDamage=u.SpellDamage,Frozen=u.Frozen,Shield=u.Shield,Taunt=u.Taunt,Silenced=u.Silenced,Stealth=u.Stealth,Poison=u.Poison,Immune=u.ImmuneUntilTurn==g.Turn,Windfury=g.HasWindfury(u)}).ToArray()};}
 public static ActionResult Execute(MatchEngine g,int seat,AgentMove move){
  if(move==null||g.Active!=seat||!g.MulliganComplete||g.Finished)return new ActionResult{Message="人机操作已失效。"};
  switch(move.Kind){case "play":return g.Play(seat,move.Source,move.Target,move.Position,move.Choice);case "attack":return g.Attack(seat,move.Source,move.Target);case "power":return g.HeroPower(seat,move.Target);case "end":return g.EndTurn();default:return new ActionResult{Message="未知人机操作。"};}
 }
}
public sealed partial class MatchEngine {
 internal string[] AgentOwnSecrets(int seat){return secrets.Where(s=>s.Seat==seat).Select(s=>MatchRules.EffectKey(Card(s.CardId))).ToArray();}
}
}
