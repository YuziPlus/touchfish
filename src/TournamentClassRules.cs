using System;
using System.Linq;
namespace Touchfish {
public static class TournamentClassRules {
 public const string SetId="grand-tournament-classes-2015",PoolId="grand-tournament-2015";
 public static void Register(MatchRules r){
  if(HeroReplacementRules.Ability("tgt:lightning-jolt")==null)HeroReplacementRules.RegisterAbility(new HeroAbilityDefinition("tgt:lightning-jolt","雷霆震击",2,"ANY_CHARACTER",(g,s,t)=>g.Damage(t,g.TournamentPowerDamage(s,2))));
  foreach(var id in new[]{"AT_001","AT_002","AT_003","AT_004","AT_005","AT_006","AT_007","AT_008","AT_009","AT_010","AT_011","AT_012","AT_013","AT_014","AT_015","AT_016","AT_018","AT_019","AT_020","AT_021","AT_022","AT_023","AT_024","AT_025","AT_026","AT_027","AT_028","AT_029","AT_030","AT_031","AT_032","AT_033","AT_034","AT_035","AT_036","AT_037","AT_038","AT_039","AT_040","AT_041","AT_042","AT_043","AT_044","AT_045","AT_046","AT_047","AT_048","AT_049","AT_050","AT_051","AT_052","AT_053","AT_054","AT_055","AT_056","AT_057","AT_058","AT_059","AT_060","AT_061","AT_062","AT_063","AT_063t","AT_064","AT_065","AT_066","AT_067","AT_068","AT_069","AT_071","AT_072","AT_073","AT_074","AT_075","AT_076","AT_077","AT_078","AT_079","AT_081","AT_104","AT_116","AT_130","AT_035t","AT_036t","AT_037t","AT_042t","AT_042t2","AT_005t"})r.Register(id,"NONE",Apply);
  foreach(var id in new[]{"AT_001","AT_004","AT_005","AT_013","AT_015","AT_024","AT_044","AT_056","AT_069","AT_074","AT_081"})r.Register(id,id=="AT_015"?"ENEMY_MINION":id=="AT_024"?"DEMON_MINION":id=="AT_081"?"NONE":"MINION",Apply);
  foreach(var id in new[]{"AT_048","AT_055","AT_064"})r.Register(id,"ANY_CHARACTER",Apply);
  foreach(var id in new[]{"AT_040","AT_057"})r.Register(id,"FRIENDLY_BEAST",Apply);
  foreach(var id in new[]{"AT_002","AT_060","AT_073"})r.Register(id,"NONE",SecretRules.Apply,(g,s)=>g.SecretRequirement(s,g.Card(id)));
  foreach(var id in new[]{"AT_006","AT_012","AT_018","AT_023","AT_039","AT_049","AT_059","AT_066","AT_076"})
   r.RegisterTrigger(new ConditionalTrigger{CardId=id,Timing=MatchTriggerTiming.HeroPowerUsed,Condition=c=>c.Source.Owner==c.Seat,Apply=Inspire});
  r.RegisterTrigger(new ConditionalTrigger{CardId="AT_011",Timing=MatchTriggerTiming.CharacterHealed,Condition=c=>true,Apply=c=>c.Game.Buff(T(c.Source),2,0)});
  r.RegisterTrigger(new ConditionalTrigger{CardId="AT_026",Timing=MatchTriggerTiming.MinionDamaged,Condition=c=>c.Source.Id==c.Damaged.Id,Apply=c=>c.Game.Damage(new MatchTarget(c.Source.Owner),c.Amount)});
  r.RegisterTrigger(new ConditionalTrigger{CardId="AT_063",Timing=MatchTriggerTiming.MinionDamaged,Condition=c=>c.Source.Id!=c.Damaged.Id,Apply=c=>c.Damaged.DamageTaken=c.Damaged.MaxHealth});
  r.RegisterTrigger(new ConditionalTrigger{CardId="AT_063t",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>c.Game.DamageBatch(()=>{foreach(var t in c.Game.Characters(0,false).Concat(c.Game.Characters(1,false)).Where(t=>t.UnitId!=c.Source.Id).ToArray())c.Game.Damage(t,1);})});
 }
 static MatchTarget T(BattleUnit u){return new MatchTarget(u.Owner,u.Id);}
 internal static void RandomCard(MatchEngine g,int s,Func<CardRecord,bool> predicate,bool summon=false){
  var pool=g.GenerationPool().Where(c=>predicate(c)&&g.Rules.Supports(c)).ToArray();
  if(pool.Length==0)return;
  var card=pool[g.RandomIndex(pool.Length)];
  if(summon)g.Summon(s,card.Id);else g.GiveGvgCard(s,card.Id);
 }
 static void Inspire(MatchTriggerContext c){
  var g=c.Game;int s=c.Source.Owner;
  switch(g.Card(c.Source.CardId).BaseId){
   case "AT_006":c.Source.SpellDamage++;break;
   case "AT_012":g.DamageBatch(()=>{g.Damage(new MatchTarget(0),4);g.Damage(new MatchTarget(1),4);});break;
   case "AT_018":RandomCard(g,s,x=>x.Type=="MINION"&&x.Rarity=="LEGENDARY",true);break;
   case "AT_023":var a=g.RandomTarget(g.Characters(s,false));var b=g.RandomTarget(g.Characters(1-s,false));if(a!=null)g.Unit(a).DamageTaken=g.Unit(a).MaxHealth;if(b!=null)g.Unit(b).DamageTaken=g.Unit(b).MaxHealth;break;
   case "AT_039":g.Players[s].TempAttack+=2;break;
   case "AT_049":foreach(var u in g.Players[s].Board.Where(u=>u.Race=="TOTEM").ToArray())g.Buff(T(u),2,0);break;
   case "AT_059":if(g.Players[s].Hand.Count==0)g.Damage(new MatchTarget(1-s),2);break;
   case "AT_066":if(g.Players[s].WeaponDurability>0)g.Players[s].WeaponAttack++;break;
   case "AT_076":RandomCard(g,s,x=>x.Type=="MINION"&&x.Race=="MURLOC",true);break;
  }
 }
 public static void Apply(EffectContext c){
  var g=c.Game;int s=c.Seat;var p=g.Players[s];var u=c.Summoned;var t=c.Target;
  switch(c.Card.BaseId){
   case "AT_001":g.Damage(t,g.SpellDamageAmount(s,8));break;
   case "AT_004":g.Damage(t,g.SpellDamageAmount(s,2+g.SpellPower(s)));break;
   case "AT_005":g.Transform(t,"AT_005t");break;
   case "AT_007":for(int n=0;n<2;n++)RandomCard(g,n,x=>x.Type=="SPELL");break;
   case "AT_010":if(p.Board.Any(x=>x!=u&&x.Race=="BEAST"))RandomCard(g,s,x=>x.Type=="MINION"&&x.Race=="BEAST",true);break;
   case "AT_013":g.Unit(t).Enchantments.Items.Add(new Enchantment{Kind="tgt-glory-"+s,Amount=1});break;
   case "AT_015":g.GiveGvgCard(s,g.Unit(t).CardId);break;
   case "AT_016":foreach(var x in g.Players.SelectMany(q=>q.Board).ToArray()){int attack=g.AttackValue(x),health=x.Health;g.SetHealth(T(x),attack);x.BuffAttack=health-x.BaseAttack-x.TempAttack-EnrageRules.AttackBonus(x,g.Card(x.CardId).BaseId)-g.AuraAttack(x);if(attack==0)x.DamageTaken=x.MaxHealth;}break;
   case "AT_022":g.Damage(g.RandomTarget(g.Characters(1-s)),g.SpellDamageAmount(s,4));break;
   case "AT_024":g.Buff(t,3,3);var opponent=g.Players[1-s];opponent.MaxMana=Math.Min(g.Mode.ManaLimit,opponent.MaxMana+1);opponent.Mana=Math.Min(opponent.MaxMana,opponent.Mana+1);break;
   case "AT_025":foreach(var victim in g.SelectRandomTargets(g.Characters(1-s,false),2))g.Unit(victim).DamageTaken=g.Unit(victim).MaxHealth;g.DiscardRandom(s,2);break;
   case "AT_028":if(c.Combo)g.Buff(T(u),3,0);break;
   case "AT_030":if(c.Combo)g.Damage(t,1);break;
   case "AT_032":if(p.Board.Any(x=>x!=u&&x.Race=="PIRATE"))g.Buff(T(u),1,1);break;
   case "AT_033":for(int n=0;n<2;n++)RandomCard(g,s,x=>x.CardClass==g.Players[1-s].ClassId);break;
   case "AT_035t":g.Summon(1-s,"AT_036t");g.Draw(s,1);break;
   case "AT_035":for(int n=0;n<3;n++)g.ShuffleGvgCard(1-s,"AT_035t");break;
   case "AT_037":if(c.Choice=="damage")g.Damage(t,g.SpellDamageAmount(s,2));else{g.Summon(s,"AT_037t");g.Summon(s,"AT_037t");}break;
   case "AT_038":p.MaxMana=Math.Min(g.Mode.ManaLimit,p.MaxMana+1);break;
   case "AT_040":g.Buff(t,0,3);break;
   case "AT_042":g.Transform(T(u),c.Choice=="charge"?"AT_042t":"AT_042t2");break;
   case "AT_043":p.MaxMana=g.Mode.ManaLimit;p.Mana=p.MaxMana;g.DiscardAll(s);break;
   case "AT_044":g.Unit(t).DamageTaken=g.Unit(t).MaxHealth;RandomCard(g,1-s,x=>x.Type=="MINION");break;
   case "AT_046":var totemPool=g.GenerationPool().Where(x=>x.Type=="MINION"&&x.Race=="TOTEM").Select(x=>x.Id).Concat(MatchEngine.TournamentTotems).ToArray();g.Summon(s,totemPool[g.RandomIndex(totemPool.Length)]);break;
   case "AT_047":int totems=p.Board.Count(x=>x!=u&&x.Race=="TOTEM");g.Buff(T(u),totems,totems);break;
   case "AT_048":g.RestoreHealth(s,t,TournamentRules.Joust(g,s)?14:7,true,true);break;
   case "AT_051":g.DamageBatch(()=>{foreach(var target in g.Characters(0,false).Concat(g.Characters(1,false)).ToArray())g.Damage(target,g.SpellDamageAmount(s,4+g.RandomIndex(2)));});break;
   case "AT_053":g.Draw(s,2);break;
   case "AT_054":g.ApplyMistcaller(s);break;
   case "AT_055":g.RestoreHealth(s,t,5,true,true);break;
   case "AT_056":var board=g.Players[t.Seat].Board;int at=board.FindIndex(x=>x.Id==t.UnitId);var targets=board.Skip(Math.Max(0,at-1)).Take(at==0?2:3).Select(T).ToArray();g.DamageBatch(()=>{foreach(var target in targets)g.Damage(target,g.SpellDamageAmount(s,2));});break;
   case "AT_057":g.Unit(t).ImmuneUntilTurn=g.Turn;break;
   case "AT_058":g.ElekkJoust(s);break;
   case "AT_061":p.LockAndLoadTurn=g.Turn;break;
   case "AT_062":for(int n=0;n<3;n++)g.Summon(s,"FP1_011");break;
   case "AT_064":g.Damage(t,g.SpellDamageAmount(s,3));g.GainGvgArmor(s,3);break;
   case "AT_065":if(p.Board.Any(x=>x.Taunt))p.WeaponDurability++;break;
   case "AT_068":foreach(var x in p.Board.Where(x=>x.Taunt).ToArray())g.Buff(T(x),2,2);break;
   case "AT_069":if(t!=null)g.Unit(t).Taunt=true;break;
   case "AT_071":if(BlackrockRules.HoldingDragon(g,s)){g.Buff(T(u),1,0);u.Charge=true;}break;
   case "AT_072":for(int n=0;n<3&&!g.Finished;n++){var ids=p.Hand.Select(h=>h.Id).ToArray();g.Draw(s,1);var drawn=p.Hand.FirstOrDefault(h=>!ids.Contains(h.Id));if(drawn!=null&&g.Card(drawn.CardId).Type=="MINION"&&p.Board.Count<g.Mode.BoardSlots){p.Hand.Remove(drawn);g.SummonHandCard(s,drawn);}}break;
   case "AT_074":g.Buff(t,3,0);g.Unit(t).Shield=true;break;
   case "AT_077":if(TournamentRules.Joust(g,s))p.WeaponDurability++;break;
   case "AT_078":var survivors=g.Players.Select(q=>q.Board.Where(x=>x.Health>0).OrderByDescending(g.AttackValue).ToArray()).Select(list=>list.Length==0?null:list.Where(x=>g.AttackValue(x)==g.AttackValue(list[0])).OrderBy(x=>g.RandomIndex(Int32.MaxValue)).First()).ToArray();foreach(var x in g.Players.SelectMany(q=>q.Board).Where(x=>!survivors.Contains(x)).ToArray())x.DamageTaken=x.MaxHealth;break;
   case "AT_079":g.PullTournamentSecrets(s);break;
   case "AT_081":foreach(var x in g.Players[1-s].Board) x.BuffAttack=1-x.BaseAttack-x.TempAttack-EnrageRules.AttackBonus(x,g.Card(x.CardId).BaseId)-g.AuraAttack(x);break;
   case "AT_104":if(TournamentRules.Joust(g,s))g.RestoreHealth(s,new MatchTarget(s),7);break;
   case "AT_116":if(BlackrockRules.HoldingDragon(g,s)){g.Buff(T(u),1,0);u.Taunt=true;}break;
  }
 }
 public static void Death(MatchEngine g,BattleUnit u,string id){
  int s=u.Owner;switch(id){
   case "AT_009":for(int n=0;n<3;n++)g.GiveGvgCard(s,"VAN_EX1_277");break;
   case "AT_019":g.Summon(s,id,g.Players[s].Board.FindIndex(x=>x.Id==u.DeathAnchor));break;
   case "AT_036":g.GiveGvgCard(s,u.CardId);g.Summon(s,"AT_036t");break;
   case "AT_038":var p=g.Players[s];p.MaxMana=Math.Max(0,p.MaxMana-1);p.Mana=Math.Min(p.Mana,p.MaxMana);break;
  }
 }
}
}
