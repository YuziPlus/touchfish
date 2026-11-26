using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
public static class ExplorerRules {
 public const string PoolId="league-of-explorers-2015";
 internal const string LockedMessage="探险者协会暂未开放。";
 internal static bool IsLockedPool(PoolDefinition pool){return pool.Id==PoolId&&pool.Archived;}
 internal static bool IsLockedCard(CardCatalog catalog,CardRecord card){
  return card.SetId==PoolId&&catalog.Pools.Any(IsLockedPool);
 }
 internal const string HatDeathrattle="explorer-hat";
 internal const string CopiedDeathrattle="copied-deathrattle";
 internal const string AncientCurse="LOE_110t";
 const string CurseInHand="LOE_007t";
 const string CurseBlade="LOE_118";
 const string Armor="LOE_119";

 static readonly string[] Artifacts={"LOEA16_3","LOEA16_4","LOEA16_5"};
 static readonly string[] Runts={"LOE_089t","LOE_089t2","LOE_089t3"};
 static readonly DecisionCandidate[] BasicPowers={
  new DecisionCandidate("tgt:basic-DRUID","变形","本回合英雄获得 1 点攻击力，并获得 1 点护甲。"),
  new DecisionCandidate("tgt:basic-HUNTER","稳固射击","对敌方英雄造成 2 点伤害。"),
  new DecisionCandidate("tgt:basic-MAGE","火焰冲击","对一个角色造成 1 点伤害。"),
  new DecisionCandidate("tgt:basic-PALADIN","援军","召唤一个 1/1 白银之手新兵。"),
  new DecisionCandidate("tgt:basic-PRIEST","次级治疗术","为一个角色恢复 2 点生命。"),
  new DecisionCandidate("tgt:basic-ROGUE","匕首精通","装备一把 1/2 匕首。"),
  new DecisionCandidate("tgt:basic-SHAMAN","图腾召唤","随机召唤一个场上没有的基础图腾。"),
  new DecisionCandidate("tgt:basic-WARLOCK","生命分流","抽一张牌，英雄受到 2 点伤害。"),
  new DecisionCandidate("tgt:basic-WARRIOR","全副武装","获得 2 点护甲。")
 };

 static string Key(string id){return "loe:"+id;}
 static MatchTarget Target(BattleUnit unit){return new MatchTarget(unit.Owner,unit.Id);}
 static bool OwnSource(MatchTriggerContext c){return c.Seat==c.Source.Owner;}

 internal static void Register(MatchRules rules){
  foreach(var id in new[]{"LOE_007","LOE_011","LOE_019t","LOE_019t2","LOE_020","LOE_026","LOE_073","LOE_079","LOE_110","LOE_111","LOE_113","LOE_116","LOEA16_4"})
   rules.Register(Key(id),"NONE",Apply);
  foreach(var id in new[]{"LOE_022","LOE_007t",AncientCurse,"LOE_009t","LOE_010","LOE_038","LOE_051","LOE_077","LOE_107",CurseBlade,Armor,"LOEA10_3","LOEA16_5t"}.Concat(Runts))
   rules.Register(Key(id),"NONE",c=>{});
  foreach(var id in new[]{"LOE_012","LOE_046","LOE_050","LOE_061","LOE_089"})
   rules.Register(Key(id),"NONE",c=>{});
  rules.Register(Key("LOE_002"),"ANY_CHARACTER",Apply);
  rules.Register(Key("LOE_002t"),"ANY_CHARACTER",Apply);
  rules.Register(Key("LOE_017"),"MINION",Apply);
  rules.Register(Key("LOE_019"),"FRIENDLY_DEATHRATTLE_MINION",Apply);
  rules.Register(Key("LOE_104"),"ENEMY_MINION",Apply);
  rules.Register(Key("LOE_105"),"MINION",Apply);
  rules.Register(Key("LOEA16_3"),"MINION",Apply);
  rules.Register(Key("LOEA16_5"),"NONE",Apply,(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"战场已满。":null);
  foreach(var id in new[]{"LOE_021","LOE_027"}){
   string secretId=id;
   rules.Register(Key(id),"NONE",SecretRules.Apply,(g,s)=>g.SecretRequirement(s,g.Card(secretId)));
  }

  RegisterDiscover(rules,"LOE_003",c=>c.Type=="SPELL");
  RegisterDiscover(rules,"LOE_006",c=>MatchEngine.Has(c,"DEATHRATTLE"));
  RegisterDiscover(rules,"LOE_023",c=>c.Cost==1);
  RegisterDiscover(rules,"LOE_029",c=>c.Cost==3);
  RegisterDiscover(rules,"LOE_039",c=>c.Type=="MINION"&&c.Race=="MECHANICAL",
   (g,f)=>g.Players[f.Seat].Board.Any(u=>u!=f.Summoned&&u.Health>0&&u.Race=="MECHANICAL"));
  RegisterDiscover(rules,"LOE_047",c=>c.Type=="MINION"&&c.Race=="BEAST");
  rules.RegisterDynamic(Key("LOE_115"),new DecisionProgram(new DecisionStep("发现一张牌",3,(g,f)=>
   DiscoverCards(g,f.Seat,c=>c.Type==(f.Choice=="minion"?"MINION":"SPELL")))));
  rules.RegisterDynamic(Key("LOE_076"),new DecisionProgram(new DecisionStep("选择新的基础英雄技能",3,(g,f)=>
   BasicPowers.Where(option=>option.Id!=BasicPowerOf(g.Players[f.Seat])))));
  rules.RegisterDynamic(Key("LOE_092"),new DecisionProgram(new DecisionStep("选择一件神器",3,
   Artifacts.Select(id=>new DecisionCandidate(id,id)).ToArray())));

  RegisterTrigger(rules,"LOE_009",MatchTriggerTiming.TurnEnd,OwnSource,c=>
   c.Game.Summon(c.Seat,"LOE_009t",c.Game.Players[c.Seat].Board.IndexOf(c.Source)+1));
  RegisterTrigger(rules,"LOE_018",MatchTriggerTiming.OverloadQueued,OwnSource,c=>c.Game.Buff(Target(c.Source),c.Amount,0));
  RegisterTrigger(rules,"LOE_016",MatchTriggerTiming.CardResolved,
   c=>OwnSource(c)&&c.Summoned!=null&&c.Game.LivingUnit(Target(c.Summoned))==c.Summoned&&
    !c.Summoned.Silenced&&MatchEngine.Has(c.Game.Card(c.Summoned.CardId),"BATTLECRY"),
   c=>c.Game.Damage(c.Game.RandomTarget(c.Game.Characters(1-c.Seat)),2));
  RegisterTrigger(rules,"LOE_086",MatchTriggerTiming.SpellResolved,c=>OwnSource(c)&&c.PaidCost>=0,
   c=>SummonRandom(c.Game,c.Seat,c.PaidCost,c.Game.Players[c.Seat].Board.IndexOf(c.Source)+1));
  RegisterTrigger(rules,"LOE_053",MatchTriggerTiming.SpellResolved,
   c=>OwnSource(c)&&c.Spell!=null&&c.Spell.Target!=null&&!c.Spell.Target.Hero&&
    c.Spell.Target.Seat==c.Seat&&c.Spell.Target.UnitId!=c.Source.Id,
   c=>{
    var original=c.Spell;
    c.Game.ApplyCardEffect(new EffectContext{Game=c.Game,Seat=c.Seat,Card=original.Card,
     Target=Target(c.Source),Combo=original.Combo,PreviousCardsPlayed=original.PreviousCardsPlayed,
     Choice=original.Choice,PlayedHand=original.PlayedHand});
   });
 }

 static void RegisterTrigger(MatchRules rules,string id,MatchTriggerTiming timing,Func<MatchTriggerContext,bool> condition,Action<MatchTriggerContext> apply){
  rules.Register(Key(id),"NONE",c=>{});
  rules.RegisterTrigger(new ConditionalTrigger{CardId=Key(id),Timing=timing,Condition=condition,Apply=apply});
 }

 static void RegisterDiscover(MatchRules rules,string id,Func<CardRecord,bool> filter,Func<MatchEngine,ResolutionFrame,bool> condition=null){
  rules.RegisterDynamic(Key(id),new DecisionProgram(new DecisionStep("发现一张牌",3,(g,f)=>
   condition==null||condition(g,f)?DiscoverCards(g,f.Seat,filter):Enumerable.Empty<DecisionCandidate>())));
 }

 static IEnumerable<DecisionCandidate> DiscoverCards(MatchEngine game,int seat,Func<CardRecord,bool> filter){
  string profession=game.Players[seat].ClassId;
  return game.GenerationPool().Where(c=>(c.CardClass=="NEUTRAL"||c.CardClass==profession)&&filter(c))
   .Select(c=>new DecisionCandidate(c.Id,c.Id,c.CardClass==profession?4:1));
 }

 static string BasicPowerOf(MatchPlayer player){
  const string basic="tgt:basic-",upgraded="tgt:upgraded-";
  if(player.HeroAbilityId==null)return basic+player.ClassId;
  if(player.HeroAbilityId.StartsWith(upgraded,StringComparison.Ordinal))return basic+player.HeroAbilityId.Substring(upgraded.Length);
  return player.HeroAbilityId;
 }

 internal static string BasicPowerLabel(string id){var power=Array.Find(BasicPowers,p=>p.Id==id);return power==null?null:power.Title;}
 internal static bool IsBasicPower(string abilityId){return BasicPowerLabel(abilityId)!=null;}
 internal static bool Has(MatchEngine game,int seat,string id){
  return game.Players[seat].Board.Any(u=>u.Health>0&&!u.Silenced&&game.Card(u.CardId).BaseId==id);
 }
 internal static bool StatueBlocked(MatchEngine game,BattleUnit unit){
  return !unit.Silenced&&game.Card(unit.CardId).BaseId=="LOE_107"&&game.Players.Sum(p=>p.Board.Count(u=>u.Health>0))>1;
 }

 internal static int DamageToHero(MatchEngine game,int seat,int amount){
  if(Has(game,seat,Armor))amount=1;
  if(game.Players[seat].WeaponId==CurseBlade)amount*=2;
  return amount;
 }

 internal static void Death(MatchEngine game,BattleUnit unit,string id){
  switch(id){
   case "LOE_012":game.GiveNaxxCard(unit.Owner,"GAME_COIN",publicIdentity:true);break;
   case "LOE_046":game.Damage(game.RandomTarget(game.Characters(1-unit.Owner)),1);break;
   case "LOE_050":game.SummonGvgRandom(unit,c=>c.Type=="MINION"&&c.Cost==1);break;
   case "LOE_061":game.Buff(game.RandomTarget(game.Characters(unit.Owner,false)),3,3);break;
   case "LOE_089":
    foreach(var runt in Runts)DeathrattleRules.SummonAtDeath(game,unit,runt,1);
    break;
  }
 }

 static void SummonRandom(MatchEngine game,int seat,int cost,int position){
  if(game.Players[seat].Board.Count>=game.Mode.BoardSlots)return;
  var pool=game.GenerationPool().Where(c=>c.Type=="MINION"&&c.Cost==cost).ToArray();
  if(pool.Length>0)game.Summon(seat,pool[game.RandomIndex(pool.Length)].Id,position);
 }

 static void Apply(EffectContext c){
  var g=c.Game;int s=c.Seat;var p=g.Players[s];var target=c.Target;
  switch(c.Card.BaseId){
   case "LOE_002":
    g.Damage(target,g.SpellDamageAmount(s,3));
    g.ShuffleGvgCard(s,"LOE_002t",known:true);
    break;
   case "LOE_002t":g.Damage(target,g.SpellDamageAmount(s,6));break;
   case "LOE_007":g.GiveNaxxCard(1-s,CurseInHand,publicIdentity:true);break;
   case "LOE_011":
    if(p.Deck.Select(id=>g.Card(id).BaseId).Distinct().Count()==p.Deck.Count)
     g.RestoreHealth(s,new MatchTarget(s),g.HeroHealthLimit(s));
    break;
   case "LOE_017":
    var unit=g.Unit(target);
    unit.BuffAttack+=3-g.UnflooredAttack(unit);
    g.SetHealth(target,3);
    break;
   case "LOE_019":DeathrattleRules.Copy(g,g.Unit(target),c.Summoned);break;
   case "LOE_019t":
    g.ShuffleGvgCard(s,"LOE_019t2",known:true);
    g.Draw(s,1);
    break;
   case "LOE_019t2":g.ReplaceWithLegendaryMinions(s);break;
   case "LOE_020":
    g.SummonExplorerDeckMinion(s);
    g.SummonExplorerDeckMinion(1-s);
    break;
   case "LOE_026":g.ResurrectExplorerMurlocs(s);break;
   case "LOE_073":
    if(p.Board.Any(u=>u!=c.Summoned&&u.Health>0&&u.Race=="BEAST"))c.Summoned.Taunt=true;
    break;
   case "LOE_079":g.ShuffleGvgCard(s,"LOE_019t",known:true);break;
   case "LOE_104":g.EntombExplorerMinion(s,target);break;
   case "LOE_105":
    g.Buff(target,1,1);
    g.Unit(target).Enchantments.Items.Add(new Enchantment{Kind=HatDeathrattle,Amount=1});
    break;
   case "LOE_110":g.ShuffleGvgCard(s,AncientCurse,known:true);break;
   case "LOE_111":
    int damage=g.SpellDamageAmount(s,3);
    foreach(var t in g.Characters(0,false).Concat(g.Characters(1,false)))g.Damage(t,damage);
    g.ShuffleGvgCard(1-s,"LOE_111",known:true);
    break;
   case "LOE_113":
    foreach(var t in g.Characters(s,false))g.Buff(t,2,2);
    break;
   case "LOE_116":
    if(p.Board.Count(u=>u!=c.Summoned&&u.Health>0)>=6)g.Buff(Target(c.Summoned),4,4);
    break;
   case "LOEA16_3":g.Buff(target,10,10);break;
   case "LOEA16_4":
    for(int i=0;i<10&&!g.Finished;i++){
     var victim=g.RandomTarget(g.Characters(1-s));
     if(victim==null)break;
     g.DamageSequenceStep(victim,1);
    }
    break;
   case "LOEA16_5":
    int slots=g.Mode.BoardSlots-p.Board.Count;
    for(int i=0;i<slots;i++)if(g.Summon(s,"LOEA16_5t")==null)break;
    break;
  }
 }

 internal static bool IsCurse(string cardId){return cardId==CurseInHand;}
}

public sealed partial class MatchEngine {
 internal void ReplaceBasicHeroPower(int seat,string abilityId){
  var player=Players[seat];
  player.HeroAbilityId=abilityId;
  player.Shadowform=0;
  player.PowersThisTurn=0;
  player.PowerUsed=false;
  Log.Add(PlayerLabel(seat)+" · 已更换英雄技能");
 }

 internal void ResolveExplorerCurses(int seat){
  foreach(var card in Players[seat].Hand.Where(h=>ExplorerRules.IsCurse(h.CardId)).ToArray()){
   Damage(new MatchTarget(seat),2);
   Cleanup();
   if(Finished)break;
  }
 }

 internal void SummonExplorerDeckMinion(int seat){
  if(Players[seat].Board.Count>=Mode.BoardSlots)return;
  var deck=Players[seat].Deck;
  var indices=Enumerable.Range(0,deck.Count).Where(i=>{var c=Card(deck[i]);return c.Type=="MINION"&&c.Cost==1;}).ToArray();
  if(indices.Length==0)return;
  int index=indices[RandomIndex(indices.Length)];
  var id=deck[index];
  deck.RemoveAt(index);
  NoteHistory(HistoryKind.Destroy,seat,Card(id).Id);
  SummonHandCard(seat,new HandCard{CardId=id},-1);
 }

 internal void EntombExplorerMinion(int seat,MatchTarget target){
  var unit=LivingUnit(target);
  if(unit==null)return;
  Players[target.Seat].Board.Remove(unit);
  ShuffleBlackrockCopy(seat,Card(unit.CardId).Id);
  RefreshAuras();
 }

 internal void ResurrectExplorerMurlocs(int seat){
  var dead=graveyard.Where(e=>e.Reason==GraveyardReason.MinionDied&&Card(e.CardId).Race=="MURLOC")
   .Select(e=>Card(e.CardId).Id).ToList();
  for(int i=0;i<7&&dead.Count>0&&!Finished&&Players[seat].Board.Count<Mode.BoardSlots;i++){
   int index=RandomIndex(dead.Count);
   string id=dead[index];
   dead.RemoveAt(index);
   Summon(seat,id);
  }
 }

 internal void ReplaceWithLegendaryMinions(int seat){
  var pool=GenerationPool().Where(c=>c.Type=="MINION"&&c.Rarity=="LEGENDARY").ToArray();
  if(pool.Length==0)return;
  var p=Players[seat];
  for(int i=0;i<p.Hand.Count;i++)p.Hand[i]=new HandCard{Id=nextId++,CardId=pool[RandomIndex(pool.Length)].Id,Generated=true};
  for(int i=0;i<p.Deck.Count;i++){
   var id=pool[RandomIndex(pool.Length)].Id;
   p.Deck[i]=id;
   blackrockGeneratedDeck[seat].Add(id);
  }
  NoteHistory(HistoryKind.Unmodeled,seat);
 }
}
}
