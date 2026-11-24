using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
// Original 2015 Blackrock Mountain. New cards use explicit registrations only.
public static class BlackrockRules {
 public const string CurrentPoolId="blackrock-mountain-2015-9554";
 public const string SetId="blackrock-mountain-2015",Ragnaros="brm:ragnaros@1",DieInsect="brm:die-insect@1";
 public static readonly string[] CardIds={"BRM_001","BRM_002","BRM_003","BRM_004","BRM_005","BRM_006","BRM_007","BRM_008","BRM_009","BRM_010","BRM_011","BRM_012","BRM_013","BRM_014","BRM_015","BRM_016","BRM_017","BRM_018","BRM_019","BRM_020","BRM_022","BRM_024","BRM_025","BRM_026","BRM_027","BRM_028","BRM_029","BRM_030","BRM_031","BRM_033","BRM_034","BRM_006t","BRM_010t","BRM_010t2","BRM_022t"};
 static BlackrockRules(){HeroReplacementRules.RegisterAbility(new HeroAbilityDefinition(DieInsect,"死吧，虫子！",2,"NONE",(g,s,t)=>{var victim=g.RandomTarget(g.Characters(1-s));if(victim!=null)g.Damage(victim,8*g.AbilityMultiplier(s));}));HeroReplacementRules.RegisterHero(new HeroReplacementDefinition(Ragnaros,"炎魔之王拉格纳罗斯",8,DieInsect));}
 static MatchTarget T(BattleUnit u){return new MatchTarget(u.Owner,u.Id);}
 public static bool HoldingDragon(MatchEngine g,int seat){return g.Players[seat].Hand.Any(h=>g.Card(h.CardId).Race=="DRAGON");}
 public static void Register(MatchRules r){foreach(var id in CardIds)r.Register(id,"NONE",Apply);foreach(var id in new[]{"BRM_003","BRM_011","BRM_013"})r.Register(id,"ANY_CHARACTER",Apply);r.Register("BRM_007","MINION",Apply);r.Register("BRM_029","LEGENDARY_MINION",Apply);r.Register("BRM_034","ANY_CHARACTER",Apply);foreach(var trigger in Triggers)r.RegisterTrigger(trigger);}
 public static string Target(CardRecord card,MatchEngine g,int seat){if(card.BaseId=="BRM_029"||card.BaseId=="BRM_034")return HoldingDragon(g,seat)?(card.BaseId=="BRM_029"?"LEGENDARY_MINION":"ANY_CHARACTER"):"NONE";return null;}
 public static MatchChoiceOption[] Choices(){return new[]{new MatchChoiceOption("attack","烈焰形态","变形成为5/2的野兽。"),new MatchChoiceOption("health","坚韧形态","变形成为2/5的野兽。")};}
 static void SummonBeside(MatchTriggerContext c,string id){c.Game.Summon(c.Source.Owner,id,c.Game.Players[c.Source.Owner].Board.IndexOf(c.Source)+1);}
 public static void Apply(EffectContext c){var g=c.Game;int s=c.Seat;var p=g.Players[s];switch(c.Card.BaseId){
  case "BRM_001":g.Draw(s,2);break;
  case "BRM_003":g.Damage(c.Target,g.SpellDamageAmount(s,4));break;
  case "BRM_004":if(HoldingDragon(g,s))g.Buff(T(c.Summoned),0,2);break;
  case "BRM_005":foreach(var t in g.Characters(0,false).Concat(g.Characters(1,false)).Where(t=>g.Unit(t).Race!="DEMON").ToArray())g.Damage(t,g.SpellDamageAmount(s,2));break;
  case "BRM_007":var copied=g.Unit(c.Target);if(copied!=null)for(int i=0;i<3;i++)g.ShuffleBlackrockCopy(s,copied.CardId);break;
  case "BRM_008":foreach(var u in g.Players[1-s].Board.Where(u=>u.Health>0&&u.DamageTaken==0).ToArray())g.Damage(T(u),2);break;
  case "BRM_010":g.Transform(T(c.Summoned),c.Choice=="attack"?"BRM_010t":"BRM_010t2");break;
  case "BRM_011":g.Damage(c.Target,g.SpellDamageAmount(s,2));p.Mana=Math.Min(p.MaxMana,p.Mana+p.LockedMana);p.LockedMana=p.Overload=0;break;
  case "BRM_012":g.Buff(T(c.Summoned),1+g.RandomIndex(4),0);break;
  case "BRM_013":g.Damage(c.Target,g.SpellDamageAmount(s,3));if(p.Hand.Count==0)g.Draw(s,1);break;
  case "BRM_014":if(p.Hand.Count==0)g.Buff(T(c.Summoned),3,3);break;
  case "BRM_015":int damage=g.SpellDamageAmount(s,p.Health<=12?3:1);foreach(var t in g.Characters(0,false).Concat(g.Characters(1,false)).ToArray())g.Damage(t,damage);break;
  case "BRM_017":var dead=g.GraveyardFor(s,GraveyardReason.MinionDied,"MINION");if(dead.Length>0)g.Summon(s,dead[g.RandomIndex(dead.Length)].CardId);break;
  case "BRM_018":p.NextDragonDiscount+=2;break;
  case "BRM_024":if(g.Players[1-s].Health<=15)g.Buff(T(c.Summoned),3,3);break;
  case "BRM_026":var pool=g.GenerationPool().Where(x=>x.Type=="MINION"&&x.Cost==1&&g.Rules.Supports(x)).ToArray();if(pool.Length>0)g.Summon(1-s,pool[g.RandomIndex(pool.Length)].Id);break;
  case "BRM_029":if(c.Target!=null){var u=g.Unit(c.Target);if(u!=null)u.DamageTaken=u.MaxHealth;}break;
  case "BRM_030":var spells=g.GenerationPool().Where(x=>x.Type=="SPELL"&&x.CardClass==g.Players[1-s].ClassId&&g.Rules.Supports(x)).ToArray();for(int i=0;i<2&&spells.Length>0;i++)g.GiveGvgCard(s,spells[g.RandomIndex(spells.Length)].Id);break;
  case "BRM_033":if(HoldingDragon(g,s))g.Buff(T(c.Summoned),1,1);break;
  case "BRM_034":if(c.Target!=null)g.Damage(c.Target,3);break;
 }}
 static bool HitSelf(MatchTriggerContext c){return c.Source.Id==c.Damaged.Id;}
 public static readonly ConditionalTrigger[] Triggers={
  new ConditionalTrigger{CardId="BRM_006",Timing=MatchTriggerTiming.MinionDamaged,Condition=HitSelf,Apply=c=>SummonBeside(c,"BRM_006t")},
  new ConditionalTrigger{CardId="BRM_016",Timing=MatchTriggerTiming.MinionDamaged,Condition=HitSelf,Apply=c=>c.Game.Damage(new MatchTarget(1-c.Source.Owner),2)},
  new ConditionalTrigger{CardId="BRM_019",Timing=MatchTriggerTiming.MinionDamaged,Condition=c=>HitSelf(c)&&c.Damaged.Health>0,Apply=c=>SummonBeside(c,"BRM_019")},
  new ConditionalTrigger{CardId="BRM_022",Timing=MatchTriggerTiming.MinionDamaged,Condition=HitSelf,Apply=c=>SummonBeside(c,"BRM_022t")},
  new ConditionalTrigger{CardId="BRM_002",Timing=MatchTriggerTiming.SpellResolved,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>{for(int i=0;i<2&&!c.Game.Finished;i++){var t=c.Game.RandomTarget(c.Game.Characters(1-c.Source.Owner));if(t!=null)c.Game.DamageSequenceStep(t,1);}}},
  new ConditionalTrigger{CardId="BRM_028",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>c.Source.Owner==c.Seat,Apply=c=>{c.Game.ShiftHandCosts(c.Source.Owner,-1);}}
 };
 // Called after payment but before spell resolution, so the targeted spell sees the new stats.
 public static void SpellTargeted(MatchEngine g,int seat,MatchTarget target){var u=g.Unit(target);if(u!=null&&u.Owner==seat&&u.Health>0&&!u.Silenced&&g.Card(u.CardId).BaseId=="BRM_020")g.Buff(target,1,1);}
 public static void Death(MatchEngine g,BattleUnit u){g.ReplaceHero(u.Owner,Ragnaros);}
}
public sealed partial class MatchEngine {
 public int DeathsThisTurn;
 readonly HashSet<string>[] blackrockGeneratedDeck={new HashSet<string>(),new HashSet<string>()};
 public void ShuffleBlackrockCopy(int seat,string id){if(Players[seat].Deck.Count>=60)return;ShuffleGvgCard(seat,id);blackrockGeneratedDeck[seat].Add(id);}
 internal int BlackrockDiscount(int seat,CardRecord c){int discount=c.Type=="MINION"&&c.Race=="DRAGON"?Players[seat].NextDragonDiscount:0;if(c.BaseId=="BRM_001"||c.BaseId=="BRM_003"||c.BaseId=="BRM_009"||c.BaseId=="BRM_025")discount+=DeathsThisTurn;return discount;}
 internal void BlackrockDrawn(int seat,HandCard drawn){var listeners=Players[seat].Board.Where(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="BRM_031").OrderBy(u=>u.Id).ToArray();foreach(var listener in listeners)GiveGvgCard(seat,drawn.CardId);}
}
}
