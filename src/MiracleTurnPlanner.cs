using System;using System.Linq;using System.Collections.Generic;
namespace Touchfish {
// Local public-information model. Unknown draws earn value but never become invented playable cards.
public sealed class MiracleTurnPlanner {
 public const int NodeLimit=5000,DepthLimit=8,BeamWidth=24;public int Nodes;
 sealed class Unit {public int Id,Attack,Health,MaxHealth,Ready,Cost,SpellDamage,DeferredBuff;public string Key;public bool Shield,Taunt,Stealth,Immune,Poison,Frozen,Silenced,Windfury;public Unit Copy(){return (Unit)MemberwiseClone();}}
 sealed class State {
  public int Mana,NextMana,SecretCount,ReachReserve,Prep,Played,Health,Armor,EnemyHealth,EnemyArmor,EnemyHeroThreat,Deck,HandCount,Fatigue,Unknown,Weapon,Durability,HeroUsed,HeroLimit,HeroTemp,PowerExtra,Next=-1;public double DrawValue;public bool HeroFrozen,PowerUsed,EnemyImmune,DrawConserve,RootPrepared;public AgentMove First;
  public List<AgentCard> Hand;public List<Unit> Own,Enemy;public Dictionary<string,int> DamageCache;public Dictionary<string,string[]> OutCache;public MiracleDrawOdds Odds;public int DrawMana,DrawPrep;public double Waste,RandomRisk;
  public State Copy(){var s=(State)MemberwiseClone();s.Hand=new List<AgentCard>(Hand);s.Own=Own.Select(u=>u.Copy()).ToList();s.Enemy=Enemy.Select(u=>u.Copy()).ToList();return s;}
 }
 static Unit U(AgentUnit u){return new Unit{Id=u.Id,Attack=u.Attack,Health=u.Health,MaxHealth=u.MaxHealth,Ready=u.AttacksRemaining,Cost=u.Cost,SpellDamage=u.SpellDamage,Key=u.Key,Shield=u.Shield,Taunt=u.Taunt,Stealth=u.Stealth,Immune=u.Immune,Poison=u.Poison,Frozen=u.Frozen,Silenced=u.Silenced,Windfury=u.Windfury};}
 static int Cost(State s,AgentCard h){return h.Type=="SPELL"?Math.Max(0,h.UndiscountedCost-s.Prep):h.Cost;}
 static int Power(State s){return s.PowerExtra+s.Own.Sum(u=>u.SpellDamage);}
 static bool Auction(State s){return s.Own.Any(u=>u.Key=="EX1_095"&&!u.Silenced);}
 public AgentMove Choose(AgentObservation v){
  Nodes=0;if(!v.CanAct)return null;var s=new State{Mana=v.Self.Mana,NextMana=Math.Min(10,v.Self.MaxMana+1),SecretCount=v.Enemy.SecretCount,Prep=v.PreparationDiscount,Played=v.CardsPlayed,Health=v.Self.Health,Armor=v.Self.Armor,EnemyHealth=v.Enemy.Health,EnemyArmor=v.Enemy.Armor,EnemyHeroThreat=v.Enemy.Frozen?0:v.Enemy.HeroAttack*(v.Enemy.WeaponWindfury?2:1),Deck=v.Self.DeckCount,HandCount=v.Self.HandCount,Fatigue=v.Self.Fatigue,Weapon=v.Self.WeaponAttack,Durability=v.Self.WeaponDurability,HeroUsed=v.Self.HeroAttacks,HeroLimit=v.Self.WeaponWindfury?2:1,HeroTemp=v.Self.HeroAttack-v.Self.WeaponAttack,HeroFrozen=v.Self.Frozen,PowerUsed=v.Self.PowerUsed,EnemyImmune=v.Enemy.Immune,PowerExtra=v.SpellPower-v.Self.Board.Sum(u=>u.SpellDamage),Hand=v.Hand.ToList(),Own=v.Self.Board.Select(U).ToList(),Enemy=v.Enemy.Board.Select(U).ToList()};
  s.ReachReserve=v.Enemy.HandCount==0?2:Math.Min(8,2+v.Enemy.HandCount+(v.Enemy.MaxMana>=5?2:0));
  s.Odds=new MiracleDrawOdds(v.RemainingDeck,v.Self.DeckCount);s.DamageCache=new Dictionary<string,int>();s.OutCache=new Dictionary<string,string[]>();
  s.RootPrepared=Prepared(s);
  double initial=Evaluate(s),best=initial+.25;AgentMove answer=null;var beam=new List<State>{s};var seen=new HashSet<string>();
  for(int depth=0;depth<DepthLimit&&Nodes<NodeLimit;depth++){
   var nextBeam=new List<State>();foreach(var current in beam){
    IEnumerable<AgentMove> moves=current.First==null?v.Moves.Where(m=>m.Kind!="end"):Moves(current,v);
    foreach(var move in moves){if(Nodes>=NodeLimit)break;Nodes++;var next=Apply(current,move,v);if(next==null)continue;next.First=current.First??move;string key=Key(next);if(!seen.Add(key))continue;double value=Evaluate(next);if(value>best){best=value;answer=next.First;}nextBeam.Add(next);}
    if(Nodes>=NodeLimit)break;
   }
   beam=nextBeam.OrderByDescending(n=>Evaluate(n)+Development(n)).Take(BeamWidth).ToList();if(beam.Count==0)break;
  }
  return answer??(Prepared(s)?v.Moves.FirstOrDefault(m=>m.Kind=="end"):null);
 }
 static string Key(State s){return s.Waste+":"+s.RandomRisk+":"+s.Mana+":"+s.Prep+":"+s.Played+":"+s.Health+":"+s.Armor+":"+s.EnemyHealth+":"+s.EnemyArmor+":"+s.Deck+":"+s.HandCount+":"+s.DrawValue+":"+s.Fatigue+":"+s.Weapon+":"+s.Durability+":"+s.HeroUsed+":"+s.PowerUsed+"|"+String.Join(",",s.Hand.Select(h=>h.Id+":"+h.Key+":"+h.Cost))+"|"+String.Join(",",s.Own.Select(u=>u.Id+":"+u.Key+":"+u.Attack+":"+u.Health+":"+u.MaxHealth+":"+u.SpellDamage+":"+u.Ready+":"+u.DeferredBuff+":"+u.Shield+":"+u.Stealth))+"|"+String.Join(",",s.Enemy.Select(u=>u.Id+":"+u.Key+":"+u.Attack+":"+u.Health+":"+u.Shield));}
 static double Support(Unit u){return u.Silenced?0:u.Key=="NEW1_019"?5:u.Key=="EX1_004"||u.Key=="EX1_162"||u.Key=="EX1_565"?3:0;}
 static double Threat(State s){return s.EnemyHeroThreat+s.Enemy.Where(u=>u.Health>0&&!u.Frozen).Sum(u=>u.Attack*(u.Windfury?2:1));}
 static double Survival(State s,Unit u){if(u.Stealth)return 1.1;int largest=s.Enemy.Where(t=>t.Health>0&&!t.Frozen).Select(t=>t.Attack).DefaultIfEmpty(0).Max();return !u.Shield&&largest>=u.Health?.3:Threat(s)>=u.Health?.65:1;}
 // Next-turn pressure is an estimate, discounted for public trades, freezes and existing taunts.
 static double ProjectedBoard(State s){return s.Enemy.Any(u=>u.Health>0&&u.Taunt&&!u.Stealth&&u.Immune)?0:s.Own.Where(u=>u.Health>0).Sum(u=>u.Attack*Math.Min(1,Survival(s,u))*(u.Frozen?.25:1));}
 static double NextDamage(State s){
  bool immuneTaunt=s.Enemy.Any(u=>u.Health>0&&u.Taunt&&!u.Stealth&&u.Immune);double board=ProjectedBoard(s);int best=HandDamage(s,board>=1,!immuneTaunt&&s.Own.Count<7);
  double taunts=s.Enemy.Where(u=>u.Health>0&&u.Taunt&&!u.Stealth).Sum(u=>u.Health+(u.Shield?2:0));return Math.Max(0,board+best-taunts);
 }
 // Small resource DP: each spell is used once, each Preparation discounts one spell,
 // and the first damage spell has no combo unless a known starter is available.
 static int HandDamage(State s,bool body,bool summon,int manaOverride=-1,bool activeCombo=false,int activePrep=0,int spellPower=0){
  int available=manaOverride<0?s.NextMana:manaOverride;int tax=s.Own.Concat(s.Enemy).Count(u=>u.Key=="EX1_616"&&!u.Silenced);string key=available+":"+activeCombo+":"+activePrep+":"+spellPower+":"+body+":"+summon+":"+tax+"|"+String.Join(",",s.Hand.Select(h=>h.Key+":"+h.Cost+":"+h.BaseCost+":"+h.UndiscountedCost).OrderBy(x=>x));if(s.DamageCache==null)s.DamageCache=new Dictionary<string,int>();int cached;if(s.DamageCache.TryGetValue(key,out cached))return cached;
  int best=0,preps=s.Hand.Count(h=>h.Key=="EX1_145")+(activePrep>0?1:0);bool coin=s.Hand.Any(h=>h.Key=="GAME_COIN");var leeroy=s.Hand.FirstOrDefault(h=>h.Key=="EX1_116");
  for(int use=0;use<2;use++){
   int budget=Math.Min(10,available+(coin?1:0));if(use==1&&(!summon||leeroy==null||leeroy.Cost>budget))continue;if(use==1)budget-=leeroy.Cost;var dp=new int[budget+1,preps+1,2];for(int m=0;m<=budget;m++)for(int p=0;p<=preps;p++)for(int c=0;c<2;c++)dp[m,p,c]=-10000;dp[0,0,use==1||coin||preps>0||activeCombo?1:0]=use==1?6:0;
   foreach(var h in s.Hand){bool spell=h.Key=="EX1_124"||h.Key=="CS2_073"&&(use==1||body);bool step=h.Key=="EX1_144"&&use==1;if(!spell&&!step)continue;int normal=step?Math.Max(0,leeroy.BaseCost-2)+tax:h.UndiscountedCost;var next=(int[,,])dp.Clone();for(int m=0;m<=budget;m++)for(int p=0;p<=preps;p++)for(int c=0;c<2;c++){if(dp[m,p,c]<0)continue;int damage=(step?6:c==1?4:2)+(h.Key=="EX1_124"?spellPower:0);for(int discount=0;discount<=(spell&&p<preps?1:0);discount++){int cost=discount==1?Math.Max(0,normal-3):normal;if(m+cost<=budget)next[m+cost,p+discount,1]=Math.Max(next[m+cost,p+discount,1],dp[m,p,c]+damage);}}dp=next;}
   foreach(int damage in dp)best=Math.Max(best,damage);
  }
  s.DamageCache[key]=best;return best;
 }
 static bool Prepared(State s){return s.Health+s.Armor>(s.Deck==0?s.Fatigue+1:0)&&s.Own.Any(u=>u.Key=="EX1_095"&&!u.Silenced&&u.Stealth)&&Threat(s)<s.Health+s.Armor-s.ReachReserve&&NextDamage(s)>=s.EnemyHealth+s.EnemyArmor;}
 static double HandValue(AgentCard h,bool leeroy){return h.Key=="EX1_144"?leeroy?7:3:h.Key=="EX1_116"?7:h.Key=="EX1_095"?5:h.Key=="CS2_077"?5:h.Type=="MINION"?3:h.Key=="EX1_124"?4:3;}
 static double Evaluate(State s){
  if(s.Health<=0)return -10000;if(s.EnemyHealth<=0)return s.RandomRisk==0?10000:200-s.RandomRisk;double incoming=Threat(s),margin=s.Health+s.Armor-incoming;
  double own=s.Own.Sum(u=>(u.Attack*1.5+u.Health*.6+(u.Shield?2:0))*Survival(s,u)*(u.Frozen?.65:1)+(u.Key=="EX1_095"&&!u.Silenced?(u.Stealth?9:3):0));
  double enemy=s.Enemy.Sum(u=>u.Attack*1.6+u.Health*.4+(u.Shield?1.5:0)+(u.Taunt?2:0)+Support(u)+(u.Key=="NEW1_021"&&!u.Silenced&&s.Own.Count>0?35:0));
  bool leeroy=s.Hand.Any(h=>h.Key=="EX1_116");double hand=s.Hand.Sum(h=>HandValue(h,leeroy))+s.DrawValue;
  double health=s.Health+s.Armor;double risk=margin<=0?90-margin*5:margin<s.ReachReserve?(s.ReachReserve-margin)*5:0;
  double follow=margin>0&&health>(s.Deck==0?s.Fatigue+1:0)&&NextDamage(s)>=s.EnemyHealth+s.EnemyArmor?(s.SecretCount>0?6:12):0;
  return own-enemy-incoming*.7-risk+health*.7-(s.EnemyHealth+s.EnemyArmor)*.65+hand+s.Weapon*Math.Min(2,s.Durability)*.9+follow-s.Waste-s.RandomRisk-s.Own.Sum(u=>u.DeferredBuff*(1-Math.Min(1,Survival(s,u)))*1.5);
 }
 static double Development(State s){var edwin=s.Hand.FirstOrDefault(h=>h.Key=="EX1_613");double grow=edwin!=null&&edwin.Cost<=s.Mana?Math.Min(16,(2+2*s.Played)*2.1-3):0;return grow;}
 static AgentMove Move(string key,int id,int cost,MatchTarget target=null,int position=-1){return new AgentMove{Kind="play",Key=key,Source=id,Cost=cost,Target=target,Position=position};}
 static IEnumerable<AgentMove> Moves(State s,AgentObservation v){
  var visible=s.Enemy.Where(u=>!u.Stealth&&!u.Immune).ToArray();bool blocked=s.Enemy.Any(u=>u.Taunt&&!u.Stealth);var taunts=visible.Where(u=>u.Taunt).ToArray();
  foreach(var unit in s.Own.Where(u=>u.Ready>0&&!u.Frozen)){
   if(!blocked&&!s.EnemyImmune)yield return new AgentMove{Kind="attack",Source=unit.Id,Target=new MatchTarget(1-v.Seat)};
   foreach(var t in (blocked?taunts:visible))yield return new AgentMove{Kind="attack",Source=unit.Id,Target=new MatchTarget(1-v.Seat,t.Id)};
  }
  if(!s.HeroFrozen&&s.HeroUsed<s.HeroLimit&&s.Weapon+s.HeroTemp>0){if(!blocked&&!s.EnemyImmune)yield return new AgentMove{Kind="attack",Target=new MatchTarget(1-v.Seat)};foreach(var t in (blocked?taunts:visible))yield return new AgentMove{Kind="attack",Target=new MatchTarget(1-v.Seat,t.Id)};}
  foreach(var h in s.Hand.Where(h=>Cost(s,h)<=s.Mana)){
   int cost=Cost(s,h);if(h.Type=="MINION"&&s.Own.Count>=7)continue;
   if(h.Key=="CS2_072"||h.Key=="EX1_581"){foreach(var t in visible.Where(t=>h.Key!="CS2_072"||t.Health==t.MaxHealth))yield return Move(h.Key,h.Id,cost,new MatchTarget(1-v.Seat,t.Id));}
   else if(h.Key=="EX1_124"||h.Key=="EX1_278"||h.Key=="EX1_134"&&s.Played>0){if(!s.EnemyImmune)yield return Move(h.Key,h.Id,cost,new MatchTarget(1-v.Seat),h.Type=="MINION"?s.Own.Count:-1);foreach(var t in visible)yield return Move(h.Key,h.Id,cost,new MatchTarget(1-v.Seat,t.Id),h.Type=="MINION"?s.Own.Count:-1);}
   else if(h.Key=="CS2_073"||h.Key=="EX1_144"){foreach(var t in s.Own)yield return Move(h.Key,h.Id,cost,new MatchTarget(v.Seat,t.Id));}
   else if(h.Key=="CS2_117"){yield return Move(h.Key,h.Id,cost,new MatchTarget(v.Seat),s.Own.Count);foreach(var t in s.Own.Where(t=>t.Health<t.MaxHealth))yield return Move(h.Key,h.Id,cost,new MatchTarget(v.Seat,t.Id),s.Own.Count);}
   else if((h.Key!="CS2_074"&&h.Key!="CS2_233"||s.Durability>0)&&(h.Key!="GAME_COIN"||s.Mana<10||Auction(s)))yield return Move(h.Key,h.Id,cost,null,h.Type=="MINION"?s.Own.Count:-1);
  }
  if(!s.PowerUsed&&s.Mana>=v.PowerCost)yield return new AgentMove{Kind="power",Cost=v.PowerCost};
 }
 static void Hit(Unit u,int damage){if(u.Immune||damage<=0)return;if(u.Shield)u.Shield=false;else u.Health-=damage;}
 static void Hero(State s,bool enemy,int hit){if(enemy){if(s.EnemyImmune)return;int armor=Math.Min(s.EnemyArmor,hit);s.EnemyArmor-=armor;s.EnemyHealth-=hit-armor;}else{int armor=Math.Min(s.Armor,hit);s.Armor-=armor;s.Health-=hit-armor;}}
 static string[] FinishOuts(State s){
  double board=ProjectedBoard(s),taunts=s.Enemy.Where(u=>u.Health>0&&u.Taunt&&!u.Stealth).Sum(u=>u.Health+(u.Shield?2:0));int need=(int)Math.Ceiling(s.EnemyHealth+s.EnemyArmor-board+taunts);bool body=board>=1,summon=s.Own.Count<7&&!s.Enemy.Any(u=>u.Health>0&&u.Taunt&&!u.Stealth&&u.Immune);if(HandDamage(s,body,summon)>=need)return new string[0];
  int tax=s.Own.Concat(s.Enemy).Count(u=>u.Key=="EX1_616"&&!u.Silenced);string key=need+":"+body+":"+summon+":"+tax+":"+s.NextMana+"|"+String.Join(",",s.Hand.Select(h=>h.Key+":"+h.Cost+":"+h.BaseCost+":"+h.UndiscountedCost).OrderBy(x=>x));string[] found;if(s.OutCache.TryGetValue(key,out found))return found;
  // These are probability-weighted scoring hypotheses only, never cards in the action tree.
  var outs=new List<string>();foreach(var card in s.Odds.Cards){if(card.Key!="EX1_116"&&card.Key!="EX1_144"&&card.Key!="EX1_124"&&card.Key!="CS2_073"&&card.Key!="EX1_145")continue;var forecast=new State{Hand=new List<AgentCard>(s.Hand),Own=s.Own,Enemy=s.Enemy,NextMana=s.NextMana,DamageCache=s.DamageCache};forecast.Hand.Add(new AgentCard{Id=-999,Key=card.Key,Type=card.Type,BaseCost=card.Cost,Cost=card.Cost,UndiscountedCost=card.Cost});if(HandDamage(forecast,body,summon)>=need)outs.Add(card.Key+"@"+card.Cost);}found=outs.ToArray();s.OutCache[key]=found;return found;
 }
 static bool Playable(State s,AgentDeckCard c){int tax=c.Type=="MINION"?s.Own.Concat(s.Enemy).Count(u=>u.Health>0&&u.Key=="EX1_616"&&!u.Silenced):0;return (c.Type!="MINION"||s.Own.Count(u=>u.Health>0)<7)&&Math.Max(0,c.Cost+tax-(c.Type=="SPELL"?s.DrawPrep:0))<=s.DrawMana;}
 static string[] ImmediateOuts(State s){
  if(s.EnemyImmune)return new string[0];bool face=!s.Enemy.Any(u=>u.Health>0&&u.Taunt&&!u.Stealth);int ready=face?s.Own.Where(u=>u.Health>0&&!u.Frozen).Sum(u=>u.Attack*u.Ready):0;if(face&&!s.HeroFrozen){int hits=Math.Max(0,s.HeroLimit-s.HeroUsed);ready+=s.HeroTemp*hits+s.Weapon*Math.Min(hits,s.Durability);}int need=s.EnemyHealth+s.EnemyArmor-ready;bool body=ready>0,summon=face&&s.Own.Count(u=>u.Health>0)<7;int power=Power(s);if(need<=0||HandDamage(s,body,summon,s.DrawMana,s.Played>0,s.DrawPrep,power)>=need)return new string[0];
  int tax=s.Own.Concat(s.Enemy).Count(u=>u.Health>0&&u.Key=="EX1_616"&&!u.Silenced);string key="now:"+need+":"+body+":"+summon+":"+tax+":"+s.DrawMana+":"+s.DrawPrep+":"+(s.Played>0)+":"+power+"|"+String.Join(",",s.Hand.Select(h=>h.Key+":"+h.Cost+":"+h.BaseCost+":"+h.UndiscountedCost).OrderBy(x=>x));string[] found;if(s.OutCache.TryGetValue(key,out found))return found;var outs=new List<string>();
  foreach(var card in s.Odds.Cards){if(card.Key!="EX1_116"&&card.Key!="EX1_144"&&card.Key!="EX1_124"&&card.Key!="CS2_073"&&card.Key!="EX1_145"&&card.Key!="EX1_134")continue;var forecast=new State{Hand=new List<AgentCard>(s.Hand),Own=s.Own,Enemy=s.Enemy,NextMana=s.NextMana,DamageCache=s.DamageCache};int cost=card.Cost+(card.Type=="MINION"?tax:0),damage;if(card.Key=="EX1_134"){if(s.Played==0||s.Own.Count(u=>u.Health>0)>=7||cost>s.DrawMana)continue;damage=2+HandDamage(forecast,body,summon&&s.Own.Count(u=>u.Health>0)<6,s.DrawMana-cost,true,s.DrawPrep,power);}else{forecast.Hand.Add(new AgentCard{Id=-999,Key=card.Key,Type=card.Type,BaseCost=card.Cost,Cost=cost,UndiscountedCost=cost});damage=HandDamage(forecast,body,summon,s.DrawMana,s.Played>0,s.DrawPrep,power);}if(damage>=need)outs.Add(card.Key+"@"+card.Cost);}
  found=outs.ToArray();s.OutCache[key]=found;return found;
 }
 static double DrawPotential(State s,int n){
  double usable=s.Odds.HitWhere(n,c=>Playable(s,c));bool danger=Threat(s)+s.ReachReserve>=s.Health+s.Armor;double control=danger?s.Odds.HitWhere(n,c=>Playable(s,c)&&(c.Key=="CS2_117"&&s.Health<=20||c.Key=="CS2_072"&&s.Enemy.Any(u=>u.Health>0&&!u.Stealth&&!u.Immune&&u.Health==u.MaxHealth)||c.Key=="EX1_124"&&s.Enemy.Any(u=>u.Health>0&&!u.Stealth&&!u.Immune)||c.Key=="EX1_581"&&s.Enemy.Any(u=>u.Health>0&&!u.Stealth&&!u.Immune)||c.Key=="EX1_129"&&s.Enemy.Any(u=>u.Health>0)||c.Key=="CS2_233"&&s.Durability>0&&s.Enemy.Any(u=>u.Health>0))):0;
  var outs=new HashSet<string>(FinishOuts(s));double finish=s.Odds.HitWhere(n,c=>outs.Contains(c.Key+"@"+c.Cost));double pair=!s.Hand.Any(h=>h.Key=="EX1_116"||h.Key=="EX1_144")?s.Odds.Both(n,"EX1_116","EX1_144"):0;
  var immediate=new HashSet<string>(ImmediateOuts(s));double now=s.Odds.HitWhere(n,c=>immediate.Contains(c.Key+"@"+c.Cost));
  return usable+control*6+finish*10+pair*2+now*60;
 }
 static void Draw(State s,int n){for(int i=0;i<n;i++){if(s.Deck>0){s.Deck--;if(s.HandCount<10){bool conserve=s.RootPrepared||s.DrawConserve||Prepared(s);double value=conserve?1:s.HandCount<7?5:3;if(!conserve&&s.Odds.Available)value=(s.HandCount<7?4.5:2.5)+Math.Max(0,DrawPotential(s,s.Unknown+1)-DrawPotential(s,s.Unknown));s.DrawValue+=value;s.HandCount++;s.Unknown++;}}else Hero(s,false,++s.Fatigue);}}
 static void Adjacency(List<Unit> board,int direction){for(int i=0;i<board.Count;i++){var u=board[i];int bonus=u.Silenced?0:u.Key=="EX1_162"?1:u.Key=="EX1_565"?2:0;if(i>0)board[i-1].Attack+=direction*bonus;if(i+1<board.Count)board[i+1].Attack+=direction*bonus;}if(direction>0)foreach(var u in board)u.Attack=Math.Max(0,u.Attack);}
 static void Cleanup(State s){Adjacency(s.Own,-1);Adjacency(s.Enemy,-1);foreach(var u in s.Own.Where(u=>u.Health<=0).ToArray()){s.Own.Remove(u);if(!u.Silenced&&u.Key=="EX1_012")Draw(s,1);if(!u.Silenced&&u.Key=="EX1_029")Hero(s,true,2);}foreach(var u in s.Enemy.Where(u=>u.Health<=0).ToArray()){s.Enemy.Remove(u);if(!u.Silenced&&u.Key=="EX1_029")Hero(s,false,2);}Adjacency(s.Own,1);Adjacency(s.Enemy,1);}
 static Unit Basic(string key,int id,int cost){int attack=key=="EX1_116"?6:key=="EX1_134"||key=="CS2_117"?3:key=="EX1_284"||key=="EX1_095"?4:key=="EX1_613"?2:1;int hp=key=="EX1_116"||key=="EX1_613"?2:attack;return new Unit{Id=id,Key=key,Attack=attack,Health=hp,MaxHealth=hp,Cost=cost,SpellDamage=key=="EX1_012"||key=="EX1_284"?1:0,Ready=key=="EX1_116"?1:0};}
 static State Apply(State before,AgentMove m,AgentObservation v){
  var s=before.Copy();s.DrawConserve=Prepared(before);s.DrawMana=s.Mana;s.DrawPrep=s.Prep;if(m.Kind=="attack"){
   var own=s.Own.FirstOrDefault(u=>u.Id==m.Source);int hit=own==null?s.Weapon+s.HeroTemp:own.Attack;if(own==null){s.HeroUsed++;if(s.Durability>0&&--s.Durability==0)s.Weapon=0;}else{own.Ready--;own.Stealth=false;}
   if(m.Target.Hero)Hero(s,true,hit);else{var t=s.Enemy.FirstOrDefault(u=>u.Id==m.Target.UnitId);if(t==null)return null;bool shield=t.Shield;Hit(t,hit);if(own==null){Hero(s,false,t.Attack);if(t.Key=="CS2_033"&&t.Attack>0)s.HeroFrozen=true;}else{bool ownShield=own.Shield;Hit(own,t.Attack);if(own.Poison&&!t.Immune&&!shield&&hit>0)t.Health=0;if(t.Poison&&!own.Immune&&!ownShield&&t.Attack>0)own.Health=0;if(t.Key=="CS2_033"&&!own.Immune&&!ownShield&&t.Attack>0){own.Frozen=true;own.Ready=0;}}}Cleanup(s);return s.Health>0?s:null;
  }
  if(m.Target!=null&&m.Target.Seat==v.Seat&&(m.Key=="CS2_072"||m.Key=="EX1_124"||m.Key=="EX1_278"||m.Key=="EX1_134"))return null;
  if(m.Kind=="power"){s.Mana-=v.PowerCost;s.PowerUsed=true;s.Weapon=1;s.Durability=2;s.HeroLimit=1;return s;}
  var card=s.Hand.FirstOrDefault(h=>h.Id==m.Source);if(card==null)return null;s.Hand.Remove(card);s.HandCount--;s.Mana-=Cost(before,card);int prior=s.Played++;
  s.DrawMana=card.Key=="GAME_COIN"?Math.Min(10,s.Mana+1):s.Mana;s.DrawPrep=card.Key=="EX1_145"?3:card.Type=="SPELL"?0:s.Prep;
  if(card.Type=="SPELL"){s.Prep=0;Draw(s,s.Own.Count(u=>u.Key=="EX1_095"&&!u.Silenced));if(s.Health<=0)return null;}
  Unit target=m.Target==null||m.Target.Hero?null:(m.Target.Seat==v.Seat?s.Own:s.Enemy).FirstOrDefault(u=>u.Id==m.Target.UnitId);int power=Power(s);Unit summoned=null;if(card.Type=="MINION"){Adjacency(s.Own,-1);summoned=Basic(card.Key,s.Next--,card.BaseCost);s.Own.Add(summoned);Adjacency(s.Own,1);}
  switch(card.Key){
   case "GAME_COIN":s.Mana=Math.Min(10,s.Mana+1);break;
   case "EX1_145":s.Prep=3;break;
   case "CS2_074":s.Weapon+=2;break;
   case "CS2_072":if(target==null)return null;Hit(target,2+power);break;
   case "EX1_124":case "EX1_278":int damage=(card.Key=="EX1_278"?1:prior>0?4:2)+power;if(m.Target.Hero)Hero(s,m.Target.Seat!=v.Seat,damage);else{if(target==null)return null;Hit(target,damage);}if(card.Key=="EX1_278")Draw(s,1);break;
   case "EX1_134":if(prior>0&&m.Target!=null){if(m.Target.Hero)Hero(s,m.Target.Seat!=v.Seat,2);else if(target!=null)Hit(target,2);}break;
   case "EX1_613":summoned.Attack=summoned.Health=summoned.MaxHealth=2+2*prior;break;
   case "EX1_116":int knives=s.Enemy.Count(u=>u.Key=="NEW1_019"&&!u.Silenced),whelps=Math.Min(2,7-s.Enemy.Count);double chance=1.0/(s.Own.Count(u=>!u.Stealth)+1),shots=knives*whelps;double miss=Math.Pow(1-chance,shots),one=shots==0?0:shots*chance*Math.Pow(1-chance,shots-1);s.RandomRisk+=30*Math.Max(0,1-miss-one);Adjacency(s.Enemy,-1);for(int i=0;i<whelps;i++)s.Enemy.Add(new Unit{Id=s.Next--,Key="GAME_WHELP",Attack=1,Health=1,MaxHealth=1});Adjacency(s.Enemy,1);break;
   case "EX1_284":Draw(s,1);break;
   case "CS2_117":if(m.Target!=null){if(m.Target.Hero){if(m.Target.Seat==v.Seat)s.Health=Math.Min(v.Self.HeroMaxHealth,s.Health+3);else s.EnemyHealth=Math.Min(v.Enemy.HeroMaxHealth,s.EnemyHealth+3);}else if(target!=null)target.Health=Math.Min(target.MaxHealth,target.Health+3);}break;
   case "CS2_073":if(target==null)return null;int buff=prior>0?4:2;if(target.Ready==0)target.DeferredBuff+=buff;if(target.Ready>0&&s.Enemy.Any(u=>u.Taunt&&!u.Stealth)&&s.Enemy.Where(u=>u.Taunt&&!u.Stealth).All(u=>u.Health<=target.Attack))s.Waste+=buff*.8;target.Attack+=buff;break;
   case "EX1_128":if(!s.Own.Any(u=>!u.Stealth)&&!Auction(s))s.Waste+=5;foreach(var u in s.Own)u.Stealth=true;break;
   case "EX1_129":foreach(var u in s.Enemy)Hit(u,1+power);Draw(s,1);break;
   case "CS2_233":int flurry=s.Weapon+power;s.Weapon=s.Durability=0;Hero(s,true,flurry);foreach(var u in s.Enemy)Hit(u,flurry);break;
   case "CS2_077":Draw(s,4);break;
   case "EX1_581":if(target==null)return null;Adjacency(s.Enemy,-1);s.Enemy.Remove(target);Adjacency(s.Enemy,1);break;
   case "EX1_144":if(target==null)return null;Adjacency(s.Own,-1);s.Own.Remove(target);Adjacency(s.Own,1);if(s.HandCount>=10)return null;int tax=s.Own.Concat(s.Enemy).Count(u=>u.Key=="EX1_616"&&!u.Silenced),cost=Math.Max(0,target.Cost-2)+tax;s.HandCount++;s.Hand.Add(new AgentCard{Id=s.Next--,Key=target.Key,Type="MINION",BaseCost=target.Cost,Cost=cost,UndiscountedCost=cost,Charge=target.Key=="EX1_116"});break;
   case "EX1_012":case "EX1_095":break;
   default:return null;
  }
  Cleanup(s);return s.Health>0?s:null;
 }
}
}




