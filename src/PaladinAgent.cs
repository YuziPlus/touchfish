using System;
using System.Linq;

namespace Touchfish {
public static class TournamentPaladinDecks {
 public static DeckDocument Secret(CardCatalog catalog){
  return Load(catalog,"佛祖骑","AAEDAZ8FDIEO+g7rD54Q2BSMlgSOlgSQlgTloQSsogS0ogS2ogQJ9Q2MDuoP7Q+QEKYVzaEE4KEEw6MEAAA=");
 }
 public static DeckDocument Murloc(CardCatalog catalog){
  return Load(catalog,"铺场鱼人骑","AAEDAZ8FBPoOjJYEjpYEupcEDeoP7Q/fFJCWBKuWBK6WBPSWBPaWBNmhBKyiBMeiBMiiBMOjBAAA");
 }
 static DeckDocument Load(CardCatalog catalog,string name,string code){
  var deck=DeckCode.Decode(catalog,code,TournamentClassRules.PoolId);
  deck.Name=name;
  return DeckRules.ForCurrentPlay(catalog,deck);
 }
}

public sealed class PaladinAgent : IMatchAgent {
 static readonly string[] secretKeys={"FP1_020","EX1_130","EX1_136","EX1_379","AT_073"};
 readonly bool murloc;

 public PaladinAgent(bool murloc){this.murloc=murloc;}

 public int[] Mulligan(AgentObservation view){
  bool hasEarlyMinion=view.Hand.Any(card=>card.Type=="MINION"&&card.Cost<=2);
  return view.Hand.Where(card=>!(card.Type=="MINION"&&card.Cost<=2)
   &&!(card.Key=="GVG_061"&&hasEarlyMinion)).Select(card=>card.Id).ToArray();
 }

 public AgentMove Choose(AgentObservation view){
  if(!view.CanAct)return null;
  var position=new Position(view);
  return view.Moves.OrderByDescending(move=>Score(position,move)).First();
 }

 double Score(Position p,AgentMove move){
  var v=p.View;
  if(move.Kind=="end")return 0;
  if(move.Kind=="attack")return AttackScore(p,move);
  if(move.Kind=="power"){
   if(p.FreeSlots==0)return -10;
   return murloc&&p.Knights>0&&p.FreeSlots>=2?32+7*Math.Min(p.Knights,p.FreeSlots-1):8;
  }

  var card=v.Hand.First(hand=>hand.Id==move.Source);
  var target=move.Target==null||move.Target.Hero?null:v.Self.Board.FirstOrDefault(u=>u.Id==move.Target.UnitId);
  switch(move.Key){
   case "GAME_COIN":
    if(v.Self.Mana>=v.ManaLimit||p.FreeSlots==0)return -10;
    bool unlocksCard=v.Hand.Any(hand=>hand.Cost==v.Self.Mana+1&&(hand.Type=="MINION"||hand.Key=="GVG_061"));
    bool unlocksInspire=murloc&&v.Self.Mana+1==v.PowerCost+4&&p.FreeSlots>=3
     &&!v.Self.PowerUsed&&v.Hand.Any(hand=>hand.Key=="AT_076"&&hand.Cost==4);
    return unlocksCard||unlocksInspire?12:-10;
   case "AT_079":
    int secrets=v.RemainingDeck.Count(entry=>secretKeys.Contains(entry.Key)&&!v.OwnSecrets.Contains(entry.Key));
    return 28+secrets*9;
   case "AT_076":
    bool canInspire=!v.Self.PowerUsed&&v.Self.Mana-move.Cost>=v.PowerCost&&p.FreeSlots>=3;
    return canInspire?56:31;
   case "GVG_061":
    if(p.FreeSlots==0&&v.Self.WeaponDurability>0)return -10;
    return 10+Math.Min(p.FreeSlots,3)*8+(v.Self.WeaponDurability==0?6:0)
     +(v.Self.Board.Any(unit=>unit.Key=="NEW1_019"&&!unit.Silenced)?10:0);
   case "CS2_093":
    int damage=2+v.SpellPower;
    if(!v.Enemy.Immune&&damage>=p.EnemyLife)return 1000;
    int kills=v.Enemy.Board.Count(unit=>!unit.Shield&&!unit.Immune&&unit.Health<=damage);
    return kills*17+v.Enemy.Board.Count(unit=>!unit.Immune)*4+(p.Danger?7:0)-4;
   case "EX1_349":
    int draws=Math.Max(0,v.Enemy.HandCount-v.Self.HandCount+1);
    return draws>v.Self.DeckCount?-20:Math.Min(draws,v.HandLimit-v.Self.HandCount+1)*12-7;
   case "CS2_092":
    if(target==null||move.Target.Seat!=v.Seat)return -20;
    bool canHitFace=v.Moves.Any(attack=>attack.Kind=="attack"&&attack.Source==target.Id&&attack.Target.Hero);
    if(canHitFace&&!v.Enemy.Immune&&p.ReadyFaceDamage+4>=p.EnemyLife)return 900;
    return 15+Math.Min(target.Health,5)+(target.Shield?4:0)+(target.AttacksRemaining>0?16:0);
   case "CS2_097":
   case "GVG_059":
    if(v.Self.WeaponDurability>0)return -2;
    return move.Key=="CS2_097"?34:25+(v.Self.Board.Length>0?9:0);
   case "AT_073":return v.Self.Board.Length>=2?20+v.Self.Board.Length:3;
   case "FP1_020":return v.Self.Board.Length>=2?20:3;
   case "EX1_136":return v.Self.Board.Length>=1?14:2;
   case "EX1_130":return 10+(!murloc&&v.Self.Board.Any(u=>u.Key=="EX1_080"&&!u.Silenced)?9:0);
   case "EX1_379":return 9;
   case "EX1_080":return 33;
   case "NEW1_019":
    return v.Moves.Any(other=>other.Kind=="play"&&other.Source!=move.Source
     &&other.Cost+move.Cost<=v.Self.Mana&&(other.Key=="GVG_061"||other.Key=="EX1_506"||other.Position>=0))?48:27;
   case "EX1_509":return 37;
   case "EX1_508":return 22+3*p.AlliedMurlocs-3*p.EnemyMurlocs;
   case "EX1_507":return 26+5*p.AlliedMurlocs-5*p.EnemyMurlocs;
   case "EX1_103":return 20+4*p.AlliedMurlocs-4*p.EnemyMurlocs;
   case "EX1_062":return 28+Math.Min(12,2*(p.AlliedMurlocs+p.EnemyMurlocs));
  }
  return card.Type=="MINION"?27+move.Cost*1.5:-5;
 }

 static double AttackScore(Position p,AgentMove move){
  var v=p.View;
  if(move.Target.Hero){
   if(v.Enemy.Immune)return -20;
   if(move.Damage>=p.EnemyLife)return 1000;
   if(p.ReadyFaceDamage>=p.EnemyLife)return 800;
   return 11+move.Damage-(move.Source==0&&v.Self.WeaponDurability>1?2:0);
  }
  var target=v.Enemy.Board.First(unit=>unit.Id==move.Target.UnitId);
  if(target.Immune)return -20;
  var source=v.Self.Board.FirstOrDefault(unit=>unit.Id==move.Source);
  bool kills=!target.Shield&&(move.Damage>=target.Health||source!=null&&source.Poison);
  if(source==null&&target.Attack>=v.Self.Health+v.Self.Armor)return -20;
  bool losesAttacker=source!=null&&!source.Immune&&!source.Shield&&(target.Attack>=source.Health||target.Poison);
  double cost=source==null?target.Attack*0.65:losesAttacker?5+source.Attack+source.Health*0.5:0;
  double value=kills?15+target.Attack*1.6+target.Health*0.7:target.Shield?7:Math.Min(5,move.Damage);
  if(target.Taunt)value+=5;
  if(p.Danger)value+=kills?target.Attack*2:0;
  if(kills&&target.Key=="NEW1_021"&&!target.Silenced)value+=60;
  if(!kills&&!target.Shield&&!target.Taunt&&p.ReadyFaceDamage>0)value-=4;
  return value-cost;
 }

 sealed class Position {
  public readonly AgentObservation View;
  public readonly int FreeSlots,Knights,AlliedMurlocs,EnemyMurlocs,EnemyLife,ReadyFaceDamage;
  public readonly bool Danger;

  public Position(AgentObservation view){
   View=view;
   FreeSlots=view.BoardSlots-view.Self.Board.Length;
   Knights=view.Self.Board.Count(unit=>unit.Key=="AT_076"&&!unit.Silenced);
   AlliedMurlocs=view.Self.Board.Count(unit=>unit.Race=="MURLOC");
   EnemyMurlocs=view.Enemy.Board.Count(unit=>unit.Race=="MURLOC");
   EnemyLife=view.Enemy.Health+view.Enemy.Armor;
   ReadyFaceDamage=view.Moves.Where(move=>move.Kind=="attack"&&move.Target.Hero).Sum(move=>{
    var unit=view.Self.Board.FirstOrDefault(attacker=>attacker.Id==move.Source);
    return move.Damage*(unit!=null?unit.AttacksRemaining:view.Self.WeaponWindfury?Math.Min(2-view.Self.HeroAttacks,view.Self.WeaponDurability):1);
   });
   int enemyAttack=view.Enemy.Board.Where(unit=>!unit.Frozen).Sum(unit=>unit.Attack*(unit.Windfury?2:1));
   if(!view.Enemy.Frozen)enemyAttack+=view.Enemy.HeroAttack;
   Danger=enemyAttack>=view.Self.Health+view.Self.Armor-2;
  }
 }
}
}
