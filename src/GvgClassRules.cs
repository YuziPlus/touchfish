using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
// December 2014 class cards. Generation is restricted to the installed multiplayer pool.
public static class GvgClassRules {
 public const string SetId="gvg-2014-classes";
 public const string PoolId=GvgRules.SetId;
 public static readonly string[] Parts=Enumerable.Range(1,7).Select(i=>"PART_"+i.ToString("000")).ToArray();
 static readonly string[] PassiveIds={"GVG_072","GVG_034","GVG_035","GVG_002","GVG_007","GVG_122","GVG_123","GVG_058","GVG_062","GVG_049","GVG_087","GVG_051","GVG_054","GVG_086","GVG_018","GVG_020","GVG_021","GVG_077","GVG_100","GVG_024","GVG_025","GVG_027","GVG_028","GVG_088","GVG_036","GVG_037","GVG_039","GVG_040","GVG_066","GVG_045t","GVG_080t"};
 static MatchTarget T(BattleUnit u){return new MatchTarget(u.Owner,u.Id);}
 static bool Mech(BattleUnit u){return u.Race=="MECHANICAL";}
 static IEnumerable<BattleUnit> Own(MatchEngine g,int seat){return g.Players[seat].Board.Where(u=>u.Health>0);}
 public static bool Has(MatchEngine g,int seat,string id){return Own(g,seat).Any(u=>!u.Silenced&&g.Card(u.CardId).BaseId==id);}
 public static bool HeroImmune(MatchEngine g,int seat){return Has(g,seat,"GVG_021");}
 public static bool Protected(MatchEngine g,BattleUnit u){return Own(g,u.Owner).Any(s=>!s.Silenced&&g.Card(s.CardId).BaseId=="GVG_122"&&Math.Abs(g.Players[u.Owner].Board.IndexOf(s)-g.Players[u.Owner].Board.IndexOf(u))==1);}
 public static void Register(MatchRules r,CardCatalog catalog){
  foreach(var c in catalog.Cards.Values.Where(c=>c.SetId==SetId))r.Register(c.Id,"NONE",Apply);
  foreach(string id in new[]{"GVG_031"})r.Register(id,"ENEMY_MINION",Apply);
  foreach(string id in new[]{"GVG_073","GVG_010","GVG_011","GVG_014","GVG_019","GVG_045","GVG_052"})r.Register(id,"MINION",Apply);
  foreach(string id in new[]{"GVG_012","GVG_015","GVG_038"})r.Register(id,"ANY_CHARACTER",Apply);
  foreach(string id in new[]{"GVG_083","GVG_055"})r.Register(id,"FRIENDLY_MECH",Apply);
  // Spare parts remain owned by the upstream neutral module.
  r.Register("GVG_001","NONE",Apply,(g,s)=>Own(g,1-s).Any()?null:"对方没有随从。");
  r.Register("GVG_050","NONE",Apply,(g,s)=>g.Players.Any(p=>p.Board.Any())?null:"战场没有随从。");
  r.Register("GVG_047","NONE",Apply,(g,s)=>Own(g,1-s).Any()?null:"对方没有随从。");
  foreach(var trigger in Triggers)r.RegisterTrigger(trigger);
 }
 public static MatchChoiceOption[] Choices(CardRecord c){switch(c.BaseId){
  case "GVG_030":return new[]{new MatchChoiceOption("attack","攻击模式","获得+1攻击力。"),new MatchChoiceOption("health","坦克模式","获得+1生命值。")};
  case "GVG_032":return new[]{new MatchChoiceOption("mana","获得法力水晶","每个玩家获得一个法力水晶。"),new MatchChoiceOption("draw","双方抽牌","每个玩家抽一张牌。")};
  case "GVG_041":return new[]{new MatchChoiceOption("wisps","召唤小精灵","召唤5个1/1小精灵。"),new MatchChoiceOption("buff","增益与嘲讽","使一个随从获得+5/+5和嘲讽。")};
  default:return new MatchChoiceOption[0];}}
 static void RandomBuff(MatchEngine g,int seat,int a,int h,Func<BattleUnit,bool> predicate=null,Action<BattleUnit> after=null){var eligible=Own(g,seat).Where(predicate??(u=>true)).ToArray();if(eligible.Length==0)return;var chosen=eligible[g.RandomIndex(eligible.Length)];g.Buff(T(chosen),a,h);if(after!=null)after(chosen);}
 public static IEnumerable<CardRecord> GenerationPool(MatchEngine g){return g.GenerationPool();}
 static void RandomCard(MatchEngine g,int seat,Func<CardRecord,bool> condition,int adjustment=0){var pool=GenerationPool(g).Where(condition).Where(g.Rules.Supports).ToArray();if(pool.Length>0)g.GiveGvgCard(seat,pool[g.RandomIndex(pool.Length)].Id,adjustment);}
 public static void Apply(EffectContext c){var g=c.Game;int s=c.Seat;var p=g.Players[s];var u=g.Unit(c.Target);switch(c.Card.BaseId){
  // Druid, Mage, Paladin: batch 1.
  case "GVG_030":g.Buff(T(c.Summoned),c.Choice=="attack"?1:0,c.Choice=="health"?1:0);break;
  case "GVG_031":g.Players[u.Owner].Board.Remove(u);g.ShuffleGvgCard(u.Owner,u.CardId);g.RefreshAuras();break;
  case "GVG_032":if(c.Choice=="draw"){g.Draw(s,1);g.Draw(1-s,1);}else foreach(var player in g.Players){if(player.MaxMana<g.Mode.ManaLimit){player.MaxMana++;player.Mana++;}}break;
  case "GVG_033":foreach(var t in g.Characters(0).Concat(g.Characters(1)).ToArray())g.RestoreHealth(s,t,t.Hero?g.HeroHealthLimit(t.Seat):g.Unit(t).MaxHealth,true,true);break;
  case "GVG_041":if(c.Choice=="wisps"){for(int i=0;i<5;i++)g.Summon(s,"VAN_CS2_231");}else{g.Buff(c.Target,5,5);u.Taunt=true;}break;
  case "GVG_080":if(Own(g,s).Any(b=>b.Id!=c.Summoned.Id&&b.Race=="BEAST"))g.Transform(T(c.Summoned),"GVG_080t");break;
  case "GVG_001":g.DamageRandomMinions(1-s,1,g.SpellDamageAmount(s,4));break;
  case "GVG_003":RandomCard(g,s,b=>b.Type=="MINION",-3);break;
  case "GVG_004":if(Own(g,s).Any(Mech))for(int i=0;i<4&&!g.Finished;i++){var t=g.RandomTarget(g.Characters(1-s));if(t!=null)g.DamageSequenceStep(t,1);}break;
  case "GVG_005":foreach(var b in Own(g,s).ToArray())g.GiveGvgCard(s,b.CardId);break;
  case "GVG_057":g.RestoreHealth(s,new MatchTarget(s),4,true,true);p.TempAttack+=2;break;
  case "GVG_059":RandomBuff(g,s,0,0,after:b=>{b.Shield=true;b.Taunt=true;});break;
  case "GVG_060":foreach(var b in Own(g,s).Where(b=>g.Card(b.CardId).BaseId=="CS2_101t").ToArray())g.Buff(T(b),2,2);break;
  case "GVG_061":for(int i=0;i<3;i++)g.Summon(s,"VAN_CS2_101t");g.EquipRaw(s,"VAN_CS2_091",1,4,false);break;
  case "GVG_063":if(c.PlayedHand!=null)c.Summoned.BuffAttack+=c.PlayedHand.AttackBonus;break;
  case "GVG_101":foreach(var b in g.Players.SelectMany(player=>player.Board).Where(b=>HasDeathrattle(g,b)).ToArray())g.Damage(T(b),2);break;
  // Hunter, Warrior, Warlock: batch 2.
  case "GVG_017":int next=g.NextGvgInstance;g.Draw(s,1);var drawn=p.Hand.FirstOrDefault(h=>h.Id==next);if(drawn!=null&&g.Card(drawn.CardId).Race=="BEAST")drawn.CostAdjustment-=4;break;
  case "GVG_026":foreach(var b in Own(g,s).ToArray()){if(!p.Board.Contains(b)||b.Health<=0)continue;int at=p.Board.IndexOf(b);b.DeathAnchor=at+1<p.Board.Count?p.Board[at+1].Id:0;DeathrattleRules.Resolve(g,b);g.Cleanup();}break;
  case "GVG_043":RandomBuff(g,s,1,0);break;
  case "GVG_046":g.Buff(T(c.Summoned),Own(g,s).Count(b=>b.Id!=c.Summoned.Id&&b.Race=="BEAST"),0);break;
  case "GVG_048":foreach(var b in Own(g,s).Where(b=>b.Id!=c.Summoned.Id&&Mech(b)).ToArray())g.Buff(T(b),2,0);break;
  case "GVG_073":int cobra=g.SpellDamageAmount(s,3);g.Damage(c.Target,cobra);g.Damage(new MatchTarget(1-s),cobra);break;
  case "GVG_050":BouncingBlade(g,s);break;
  case "GVG_052":u.DamageTaken=u.MaxHealth;break;
  case "GVG_053":g.GainGvgArmor(s,5);break;
  case "GVG_055":g.Buff(c.Target,2,2);break;
  case "GVG_056":g.ShuffleGvgCard(1-s,"GVG_056t");break;
  case "GVG_015":g.Damage(c.Target,g.SpellDamageAmount(s,3));break;
  case "GVG_019":if(u.Owner==s&&u.Race=="DEMON")g.Buff(c.Target,5,5);else g.Damage(c.Target,g.SpellDamageAmount(s,5));break;
  case "GVG_045":int imps=g.Damage(c.Target,g.SpellDamageAmount(s,2+g.RandomIndex(3)));for(int i=0;i<imps;i++)g.Summon(s,"GVG_045t");break;
  // Priest, Rogue, Shaman: batch 3.
  case "GVG_008":var targets=g.Players.SelectMany(player=>player.Board).Where(b=>b.Health>0).Select(b=>new{Target=T(b),Amount=g.SpellDamageAmount(s,g.AttackValue(b))}).ToArray();foreach(var t in targets)g.Damage(t.Target,t.Amount);break;
  case "GVG_009":g.Damage(new MatchTarget(0),3);g.Damage(new MatchTarget(1),3);break;
  case "GVG_010":g.Buff(c.Target,2,4);u.SpellDamage++;break;
  case "GVG_011":u.TempAttack-=2;break;
  case "GVG_012":g.RestoreHealth(s,c.Target,3,true,true);if(c.Target.Hero?g.Players[c.Target.Seat].Health>0&&g.Players[c.Target.Seat].Health<g.HeroHealthLimit(c.Target.Seat):u.Health>0&&u.DamageTaken>0)g.Summon(s,"VAN_EX1_001");break;
  case "GVG_014":int hp=c.Summoned.Health,other=u.Health;g.SetHealth(c.Target,hp);g.SetHealth(T(c.Summoned),other);break;
  case "GVG_083":g.Buff(c.Target,0,4);break;
  case "GVG_022":if(p.WeaponDurability>0)p.WeaponAttack+=3;if(c.Combo)RandomBuff(g,s,3,0);break;
  case "GVG_023":if(p.WeaponDurability>0)p.WeaponAttack++;break;
  case "GVG_047":var victim=g.RandomTarget(g.Characters(1-s,false));if(victim!=null)g.Unit(victim).DamageTaken=g.Unit(victim).MaxHealth;if(c.Combo)g.BreakWeapon(1-s);break;
  case "GVG_028t":OverloadRules.GainTemporaryMana(g,s,1);break;
  case "GVG_029":for(int player=s,n=0;n<2;n++,player=1-s){if(g.Players[player].Board.Count>=g.Mode.BoardSlots)continue;var hand=g.Players[player].Hand.Where(h=>g.Card(h.CardId).Type=="MINION").ToArray();if(hand.Length>0){var chosen=hand[g.RandomIndex(hand.Length)];g.Players[player].Hand.Remove(chosen);g.Summon(player,chosen.CardId);}}break;
  case "GVG_038":g.Damage(c.Target,g.SpellDamageAmount(s,3+g.RandomIndex(4)));break;
  case "GVG_042":for(int i=0;i<4;i++)RandomCard(g,s,b=>b.Race=="MURLOC");break;
  // Seven uncollectible spare parts.
  case "PART_001":g.Buff(c.Target,0,1);break;
  case "PART_002":g.ReturnToHand(c.Target);break;
  case "PART_003":u.Taunt=true;break;
  case "PART_004":u.Stealth=true;u.StealthExpirySeat=s;u.StealthUntilTurn=g.Turn+2;break;
  case "PART_005":g.Freeze(c.Target);break;
  case "PART_006":int attack=g.AttackValue(u),health=u.Health;g.SetHealth(c.Target,attack);u.BuffAttack=health-u.BaseAttack-u.TempAttack-EnrageRules.AttackBonus(u,g.Card(u.CardId).BaseId)-g.AuraAttack(u);if(attack==0)u.DamageTaken=u.MaxHealth;break;
  case "PART_007":g.Buff(c.Target,1,0);break;
  default:if(!PassiveIds.Contains(c.Card.BaseId))throw new InvalidOperationException("该效果尚未实现。");break;
 }}
 public static bool HasDeathrattle(BattleUnit u){return u.ForestDeathrattles>0||u.AncestralSpirits>0;}
 public static bool HasDeathrattle(MatchEngine g,BattleUnit u){return HasDeathrattle(u)||!u.Silenced&&MatchEngine.Has(g.Card(u.CardId),"DEATHRATTLE");}
 static void BouncingBlade(MatchEngine g,int seat){for(int i=0;i<10000&&!g.Finished;i++){var living=g.Players.SelectMany(p=>p.Board).Where(u=>u.Health>0).ToArray();if(living.Length==0||living.All(u=>u.ImmuneUntilTurn==g.Turn||u.CommandingShoutTurn==g.Turn&&u.Health<=1))break;var t=living[g.RandomIndex(living.Length)];if(g.GvgBladeDamage(t,g.SpellDamageAmount(seat,1)))break;}}
 public static void Death(MatchEngine g,BattleUnit u){if(g.Card(u.CardId).BaseId=="GVG_035")g.ShuffleGvgCard(u.Owner,u.CardId);}
 public static void WeaponRemoved(MatchEngine g,int seat,string id){if(id!=null&&g.Card(id).BaseId=="GVG_036")RandomBuff(g,seat,2,2,Mech);}
 public static void HeroDamaged(MatchEngine g,int seat,int amount){if(g.Active!=seat||amount<=0)return;foreach(var u in Own(g,seat).Where(u=>!u.Silenced&&g.Card(u.CardId).BaseId=="GVG_100").ToArray())g.Buff(T(u),2,2);}
 public static void MinionsDied(MatchEngine g,BattleUnit[] dead){foreach(var unit in dead)foreach(var h in g.Players[unit.Owner].Hand.Where(h=>g.Card(h.CardId).BaseId=="GVG_063"))h.AttackBonus++;}
 public static readonly ConditionalTrigger[] Triggers={
  new ConditionalTrigger{CardId="GVG_034",Timing=MatchTriggerTiming.MinionDamaged,Condition=c=>c.Source.Id==c.Damaged.Id,Apply=c=>c.Game.GiveGvgCard(c.Source.Owner,Parts[c.Game.RandomIndex(Parts.Length)])},
  new ConditionalTrigger{CardId="GVG_049",Timing=MatchTriggerTiming.MinionDamaged,Condition=c=>c.Source.Id==c.Damaged.Id,Apply=c=>c.Source.BuffAttack+=c.Game.AttackValue(c.Source)},
  new ConditionalTrigger{CardId="GVG_062",Timing=MatchTriggerTiming.MinionSummoned,Condition=c=>c.Source.Owner==c.Seat&&Mech(c.Summoned),Apply=c=>c.Source.Shield=true},
  new ConditionalTrigger{CardId="GVG_025",Timing=MatchTriggerTiming.MinionSummoned,Condition=c=>c.Source.Owner==c.Seat&&c.Summoned.Race=="PIRATE",Apply=c=>c.Source.Stealth=true},
  new ConditionalTrigger{CardId="GVG_027",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>c.Seat==c.Source.Owner,Apply=c=>RandomBuff(c.Game,c.Source.Owner,2,2,b=>b.Id!=c.Source.Id&&Mech(b))},
  new ConditionalTrigger{CardId="GVG_039",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>c.Seat==c.Source.Owner,Apply=c=>c.Game.RestoreHealth(c.Source.Owner,new MatchTarget(c.Source.Owner),4)},
  new ConditionalTrigger{CardId="GVG_020",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>c.Seat==c.Source.Owner,Apply=c=>{var chosen=c.Game.RandomTarget(c.Game.Characters(0,false).Concat(c.Game.Characters(1,false)).Where(t=>!Mech(c.Game.Unit(t))));if(chosen!=null)c.Game.Damage(chosen,2);}},
  new ConditionalTrigger{CardId="GVG_077",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>Own(c.Game,c.Source.Owner).Count()==1,Apply=c=>c.Source.DamageTaken=c.Source.MaxHealth},
  new ConditionalTrigger{CardId="GVG_040",Timing=MatchTriggerTiming.MinionDied,Condition=c=>c.Died.Owner==c.Source.Owner&&c.Died.Race=="MURLOC",Apply=c=>c.Game.Draw(c.Source.Owner,1)},
  new ConditionalTrigger{CardId="GVG_072",Timing=MatchTriggerTiming.CharacterHealed,Condition=c=>true,Apply=c=>{var t=c.Game.RandomTarget(c.Game.Characters(1-c.Source.Owner));if(t!=null)c.Game.Damage(t,1);}},
  new ConditionalTrigger{CardId="GVG_028",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Seat!=c.Source.Owner&&c.Card.BaseId!="GVG_028t",Apply=c=>{c.Game.GiveGvgCard(c.Source.Owner,c.Card.Id);c.Game.GiveGvgCard(c.Seat,"GVG_028t");}}
 };
}
public sealed partial class MatchEngine {
 internal bool GvgBladeDamage(BattleUnit unit,int amount){var prior=damageListeners;var queue=damageTriggers;damageListeners=null;damageTriggers=null;try{Damage(new MatchTarget(unit.Owner,unit.Id),amount);bool lethal=Players.Any(p=>p.Board.Any(u=>u.Health<=0));Cleanup();return lethal;}finally{damageListeners=prior;damageTriggers=queue;}}
 int GvgAttackAura(BattleUnit unit){return unit.Race=="DEMON"?2*Players[unit.Owner].Board.Count(s=>s.Id!=unit.Id&&!s.Silenced&&s.Health>0&&Card(s.CardId).BaseId=="GVG_021"):0;}
 int GvgHealthAura(BattleUnit unit){return GvgAttackAura(unit);}
 void GvgCombatHeal(BattleUnit source,int amount){if(source!=null&&!source.Silenced&&Card(source.CardId).BaseId=="GVG_018"&&amount>0)RestoreHealth(source.Owner,new MatchTarget(source.Owner),amount);}
 internal int NextGvgInstance{get{return nextId;}}
 public void GiveGvgCard(int seat,string id,int adjustment=0){int before=nextId;GiveNaxxCard(seat,id);var hand=Players[seat].Hand.FirstOrDefault(h=>h.Id==before);if(hand!=null)hand.CostAdjustment=adjustment;}
 public void ShuffleGvgCard(int seat,string id){var deck=Players[seat].Deck;deck.Insert(random.Next(deck.Count+1),id);}
 public void GainGvgArmor(int seat,int amount){if(amount<=0)return;Players[seat].Armor+=amount;foreach(var u in Players[seat].Board.Where(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="GVG_086").ToArray())Buff(new MatchTarget(seat,u.Id),1,0);}
 internal void GvgDrawn(int seat,string id){if(!MulliganComplete||id!="GVG_007")return;DamageBatch(()=>{foreach(var t in Characters(0).Concat(Characters(1)).ToArray())Damage(t,2);});Cleanup();}
}
}

