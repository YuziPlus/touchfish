using System;using System.Linq;using System.Collections.Generic;
namespace Touchfish {
public sealed class MiracleBurstPlanner {
 public const int NodeLimit=4500,DepthLimit=12;public int Nodes;bool secretRisk;double bestFinish,bestPressure;AgentMove pressure;
 sealed class Unit {public int Id,Attack,Health,MaxHealth,Ready,SpellDamage,BaseCost;public string Key;public bool Shield,Taunt,Stealth,Immune,Poison,Silenced;public Unit Copy(){return (Unit)MemberwiseClone();}}
 sealed class State {
  public int Mana,Prep,Played,EnemyHp,PreHitHp,Power,Weapon,Durability,HeroTemp,HeroUsed,HeroLimit,HandCount,DeckCount,Health,Armor,Fatigue,Next=-1;public bool HeroFrozen,PowerUsed;
  public List<AgentCard> Hand=new List<AgentCard>();public List<Unit> Own=new List<Unit>(),Enemy=new List<Unit>();public AgentMove First;
  public State Copy(){return new State{Mana=Mana,Prep=Prep,Played=Played,EnemyHp=EnemyHp,Power=Power,Weapon=Weapon,Durability=Durability,HeroTemp=HeroTemp,HeroUsed=HeroUsed,HeroLimit=HeroLimit,HeroFrozen=HeroFrozen,PowerUsed=PowerUsed,HandCount=HandCount,DeckCount=DeckCount,Health=Health,Armor=Armor,Fatigue=Fatigue,Next=Next,Hand=new List<AgentCard>(Hand),Own=Own.Select(u=>u.Copy()).ToList(),Enemy=Enemy.Select(u=>u.Copy()).ToList(),First=First};}
 }
 static Unit U(AgentUnit u){return new Unit{Id=u.Id,Key=u.Key,BaseCost=u.Cost,Attack=u.Attack,Health=u.Health,MaxHealth=u.MaxHealth,SpellDamage=u.SpellDamage,Shield=u.Shield,Taunt=u.Taunt,Stealth=u.Stealth,Immune=u.Immune,Poison=u.Poison,Silenced=u.Silenced};}
 static int ReplayCost(State state,int baseCost){
  int tax=state.Own.Concat(state.Enemy).Count(unit=>unit.Key=="EX1_616"&&!unit.Silenced);
  return Math.Max(0,baseCost-2)+tax;
 }
 static bool Supported(string key){return new[]{"GAME_COIN","EX1_145","EX1_144","CS2_073","CS2_074","EX1_124","EX1_278","EX1_129","CS2_233","EX1_116","EX1_134","EX1_012","EX1_581","CS2_072"}.Contains(key);}
 static int Cost(State s,AgentCard h){return h.Type=="SPELL"?Math.Max(0,h.UndiscountedCost-s.Prep):h.Cost;}
 public AgentMove Choose(AgentObservation v){
  Nodes=0;secretRisk=v.Enemy.ClassId=="MAGE"&&v.Enemy.SecretCount>0;bestFinish=Double.MaxValue;bestPressure=Double.MinValue;pressure=null;if(!v.CanAct||v.Enemy.Immune)return null;
  // Do not plan through lethal fatigue; unknown draws and secrets are never fabricated.
  if(v.Self.DeckCount==0&&v.Self.Board.Any(u=>u.Key=="EX1_095"&&!u.Silenced))return null;
  var s=new State{HandCount=v.Self.HandCount,DeckCount=v.Self.DeckCount,Health=v.Self.Health,Armor=v.Self.Armor,Fatigue=v.Self.Fatigue,Mana=v.Self.Mana,Prep=v.PreparationDiscount,Played=v.CardsPlayed,EnemyHp=v.Enemy.Health+v.Enemy.Armor,Power=v.SpellPower,Weapon=v.Self.WeaponAttack,Durability=v.Self.WeaponDurability,HeroTemp=v.Self.HeroAttack-v.Self.WeaponAttack,HeroUsed=v.Self.HeroAttacks,HeroLimit=v.Self.WeaponWindfury?2:1,HeroFrozen=v.Self.Frozen,PowerUsed=v.Self.PowerUsed,Hand=v.Hand.Where(h=>Supported(h.Key)).ToList(),Own=v.Self.Board.Select(U).ToList(),Enemy=v.Enemy.Board.Select(U).ToList()};
  foreach(var unit in s.Own){var original=v.Self.Board.First(u=>u.Id==unit.Id);unit.Ready=original.AttacksRemaining;}
  AgentMove answer=null;Search(s,v,0,new HashSet<string>(),ref answer);return answer??pressure;
 }
 // A conservative hand-only follow-up estimate: no future draw, surviving board or opponent action is assumed.
 static int NextHandBurst(State s,int mana){
  var leeroy=s.Hand.FirstOrDefault(h=>h.Key=="EX1_116");if(leeroy==null||leeroy.Cost>mana||s.Own.Count>=7)return 0;int budget=mana-leeroy.Cost;var values=new int[budget+1];
  foreach(var card in s.Hand.Where(h=>h.Id!=leeroy.Id)){int cost,damage;if(card.Key=="EX1_144"){cost=ReplayCost(s,leeroy.BaseCost);damage=6;}else if(card.Key=="CS2_073"){cost=card.UndiscountedCost;damage=4;}else if(card.Key=="EX1_124"){cost=card.UndiscountedCost;damage=4;}else continue;for(int i=budget;i>=cost;i--)values[i]=Math.Max(values[i],values[i-cost]+damage);}
  return 6+values.Max();
 }
 bool Search(State s,AgentObservation v,int depth,HashSet<string> seen,ref AgentMove answer){
  if(Nodes>=NodeLimit)return false;Nodes++;if(s.EnemyHp<=0){double finish=s.PreHitHp*100+(v.Self.Mana-s.Mana)+(v.Hand.Length-s.Hand.Count)*2;if(!secretRisk||finish<bestFinish){answer=s.First;bestFinish=finish;}return !secretRisk;}
  if(s.First!=null&&v.Enemy.ClassId=="MAGE"&&v.Enemy.MaxMana>=8&&v.Self.Health+v.Self.Armor<=15&&v.Enemy.Health+v.Enemy.Armor-s.EnemyHp>=6){int follow=NextHandBurst(s,Math.Min(10,v.Self.MaxMana+1));if(follow>=s.EnemyHp){double quality=follow-s.EnemyHp+s.Mana*.1;if(quality>bestPressure){pressure=s.First;bestPressure=quality;}}}
  if(depth>=DepthLimit)return false;
  string key=s.Mana+":"+s.Prep+":"+(s.Played>0)+":"+s.EnemyHp+":"+s.Power+":"+s.Weapon+":"+s.Durability+":"+s.HeroUsed+":"+s.PowerUsed+":"+s.HandCount+":"+s.DeckCount+":"+s.Health+":"+s.Armor+"|"+String.Join(",",s.Hand.Select(h=>h.Id+":"+h.Cost))+"|"+String.Join(",",s.Own.Select(u=>u.Id+":"+u.Attack+":"+u.Health+":"+u.Ready+":"+u.Shield))+"|"+String.Join(",",s.Enemy.Select(u=>u.Id+":"+u.Health+":"+u.Shield));
  if(!seen.Add(key))return false;
  foreach(var move in Moves(s,v).OrderByDescending(m=>m.Kind=="attack"?m.Target.Hero?100:30:m.Key=="EX1_124"?90:m.Key=="EX1_144"?70:m.Key=="EX1_116"?65:10)){
   if(s.First==null&&!v.Moves.Any(m=>Same(m,move)))continue;var next=Apply(s,move,v);if(next==null)continue;next.First=s.First??move;
   if(Search(next,v,depth+1,seen,ref answer))return true;if(Nodes>=NodeLimit)break;
  }return false;
 }
 static bool Same(AgentMove a,AgentMove b){return a.Kind==b.Kind&&a.Source==b.Source&&(a.Target==null?b.Target==null:b.Target!=null&&a.Target.Seat==b.Target.Seat&&a.Target.UnitId==b.Target.UnitId);}
 static IEnumerable<AgentMove> Moves(State s,AgentObservation v){
  var taunts=s.Enemy.Where(u=>u.Health>0&&u.Taunt&&!u.Stealth).ToArray();
  foreach(var u in s.Own.Where(u=>u.Ready>0&&u.Health>0)){if(taunts.Length==0)yield return new AgentMove{Kind="attack",Source=u.Id,Target=new MatchTarget(1-v.Seat)};foreach(var t in taunts.Where(t=>!t.Immune))yield return new AgentMove{Kind="attack",Source=u.Id,Target=new MatchTarget(1-v.Seat,t.Id)};}
  if(!s.HeroFrozen&&s.HeroUsed<s.HeroLimit&&s.Weapon+s.HeroTemp>0&&taunts.Length==0)yield return new AgentMove{Kind="attack",Target=new MatchTarget(1-v.Seat)};
  foreach(var h in s.Hand.Where(h=>Cost(s,h)<=s.Mana)){
   var move=new AgentMove{Kind="play",Source=h.Id,Key=h.Key,Cost=Cost(s,h),Position=h.Type=="MINION"?s.Own.Count:-1};
   if(h.Key=="CS2_073"||h.Key=="EX1_144"){foreach(var u in s.Own.Where(u=>h.Key=="EX1_144"?u.Key=="EX1_116"||u.Key=="EX1_134":u.Ready>0))yield return With(move,new MatchTarget(v.Seat,u.Id));}
   else if(h.Key=="EX1_134"&&s.Played>0&&s.Own.Count<7){yield return With(move,new MatchTarget(1-v.Seat));foreach(var u in taunts.Where(u=>!u.Immune))yield return With(move,new MatchTarget(1-v.Seat,u.Id));}
   else if(h.Key=="EX1_581"||h.Key=="CS2_072"){foreach(var u in taunts.Where(u=>!u.Immune&&(h.Key!="CS2_072"||u.Health==u.MaxHealth)))yield return With(move,new MatchTarget(1-v.Seat,u.Id));}
   else if(h.Key=="EX1_124"||h.Key=="EX1_278"){yield return With(move,new MatchTarget(1-v.Seat));foreach(var u in taunts.Where(u=>!u.Immune))yield return With(move,new MatchTarget(1-v.Seat,u.Id));}
   else if((h.Type!="MINION"||s.Own.Count<7)&&(h.Key!="CS2_074"&&h.Key!="CS2_233"||s.Durability>0))yield return move;
  }
  if(!s.PowerUsed&&s.Mana>=v.PowerCost&&!s.HeroFrozen&&s.HeroUsed<s.HeroLimit)yield return new AgentMove{Kind="power",Cost=v.PowerCost};
 }
 static AgentMove With(AgentMove m,MatchTarget t){return new AgentMove{Kind=m.Kind,Key=m.Key,Source=m.Source,Cost=m.Cost,Position=m.Position,Target=t};}
 static void Hit(Unit u,int amount){if(u.Immune||amount<=0)return;if(u.Shield)u.Shield=false;else u.Health-=amount;}
 static bool Draw(State s,int n){for(int i=0;i<n;i++){if(s.DeckCount>0){s.DeckCount--;s.HandCount=Math.Min(10,s.HandCount+1);}else{int hit=++s.Fatigue;int absorbed=Math.Min(s.Armor,hit);s.Armor-=absorbed;s.Health-=hit-absorbed;}}return s.Health>0;}
 static State Apply(State before,AgentMove m,AgentObservation v){
  var s=before.Copy();
  if(m.Kind=="attack"){var own=s.Own.FirstOrDefault(u=>u.Id==m.Source);int damage=own==null?s.Weapon+s.HeroTemp:own.Attack;if(own==null){s.HeroUsed++;if(s.Durability>0&&--s.Durability==0)s.Weapon=0;}else own.Ready--;
   if(m.Target.Hero){s.PreHitHp=s.EnemyHp;s.EnemyHp-=damage;}else{var enemy=s.Enemy.First(u=>u.Id==m.Target.UnitId);bool shield=own.Shield;Hit(enemy,damage);Hit(own,enemy.Attack);if(enemy.Poison&&!own.Immune&&!shield&&enemy.Attack>0)own.Health=0;if(enemy.Key=="CS2_033"&&!own.Immune&&!shield&&enemy.Attack>0)own.Ready=0;}s.Power-=s.Own.Where(u=>u.Health<=0).Sum(u=>u.SpellDamage);s.Own.RemoveAll(u=>u.Health<=0);s.Enemy.RemoveAll(u=>u.Health<=0);return s;}
  if(m.Kind=="power"){s.Mana-=v.PowerCost;s.PowerUsed=true;s.Weapon=1;s.Durability=2;s.HeroLimit=1;return s;}
  var card=s.Hand.First(h=>h.Id==m.Source);s.Hand.Remove(card);s.HandCount--;if(card.Type=="SPELL"&&!Draw(s,s.Own.Count(u=>u.Key=="EX1_095"&&!u.Silenced)))return null;s.Mana-=Cost(before,card);bool combo=s.Played>0;if(card.Type=="SPELL")s.Prep=0;s.Played++;
  switch(card.Key){
   case "GAME_COIN":if(before.Mana>=10)return null;s.Mana=Math.Min(10,s.Mana+1);break;
   case "EX1_145":if(before.Prep>0)return null;s.Prep=3;break;
   case "CS2_074":s.Weapon+=2;break;
   // Public knives make a new Leeroy's survival uncertain; do not certify this as guaranteed lethal.
   case "EX1_116":if(s.Enemy.Count<7&&s.Enemy.Any(u=>u.Key=="NEW1_019"&&!u.Silenced))return null;s.Own.Add(new Unit{Id=s.Next--,Key=card.Key,BaseCost=card.BaseCost,Attack=6,Health=2,MaxHealth=2,Ready=1});break;
   case "EX1_134":s.Own.Add(new Unit{Id=s.Next--,Key=card.Key,BaseCost=card.BaseCost,Attack=3,Health=3,MaxHealth=3});if(combo){if(m.Target.Hero){s.PreHitHp=s.EnemyHp;s.EnemyHp-=2;}else Hit(s.Enemy.First(u=>u.Id==m.Target.UnitId),2);}break;
   case "EX1_012":s.Power++;s.Own.Add(new Unit{Id=s.Next--,Key=card.Key,BaseCost=card.BaseCost,Attack=1,Health=1,MaxHealth=1,SpellDamage=1});break;
   case "EX1_144":var returned=s.Own.First(u=>u.Id==m.Target.UnitId);s.Own.Remove(returned);if(s.HandCount<10){int cost=ReplayCost(s,returned.BaseCost);s.HandCount++;s.Hand.Add(new AgentCard{Id=s.Next--,Key=returned.Key,Type="MINION",BaseCost=returned.BaseCost,Cost=cost,UndiscountedCost=cost,Charge=returned.Key=="EX1_116"});}break;
   case "CS2_073":s.Own.First(u=>u.Id==m.Target.UnitId).Attack+=combo?4:2;break;
   case "EX1_581":s.Enemy.RemoveAll(u=>u.Id==m.Target.UnitId);break;
   case "CS2_072":Hit(s.Enemy.First(u=>u.Id==m.Target.UnitId),2+s.Power);break;
   case "EX1_124":case "EX1_278":int amount=(card.Key=="EX1_278"?1:combo?4:2)+s.Power;if(m.Target.Hero){s.PreHitHp=s.EnemyHp;s.EnemyHp-=amount;}else Hit(s.Enemy.First(u=>u.Id==m.Target.UnitId),amount);break;
   case "EX1_129":foreach(var u in s.Enemy)Hit(u,1+s.Power);break;
   case "CS2_233":int hit=s.Weapon+s.Power;s.Weapon=s.Durability=0;s.PreHitHp=s.EnemyHp;s.EnemyHp-=hit;foreach(var u in s.Enemy)Hit(u,hit);break;
  }
  s.Enemy.RemoveAll(u=>u.Health<=0);if((card.Key=="EX1_129"||card.Key=="EX1_278")&&!Draw(s,1))return null;return s;
 }
}
}
