using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
public static class DeathrattleRules {
 static readonly Dictionary<string,Action<MatchEngine,BattleUnit,string>> effects=new Dictionary<string,Action<MatchEngine,BattleUnit,string>>{
  {"AT_009",TournamentClassRules.Death},{"AT_019",TournamentClassRules.Death},{"AT_036",TournamentClassRules.Death},{"AT_038",TournamentClassRules.Death},{"AT_123",TournamentRules.Death},{"AT_128",TournamentRules.Death},
  {"BRM_027",(g,u,id)=>BlackrockRules.Death(g,u)},
  {"GVG_035",GvgClassRules.Death},
 {"GVG_076",GvgRules.Death}, {"GVG_078",GvgRules.Death}, {"GVG_082",GvgRules.Death}, {"GVG_096",GvgRules.Death}, {"GVG_105",GvgRules.Death}, {"GVG_110t",GvgRules.Death}, {"GVG_114",GvgRules.Death}, {"GVG_115",GvgRules.Death},
  {"FP1_001",NaxxRules.RemainingDeath},{"FP1_024",NaxxRules.RemainingDeath},{"FP1_029",NaxxRules.RemainingDeath},{"FP1_014",NaxxRules.RemainingDeath},{"FP1_015",NaxxRules.RemainingDeath},
  {"FP1_007",(g,u,id)=>SummonAtDeath(g,u,"FP1_007t",1)},{"FP1_012",(g,u,id)=>SummonAtDeath(g,u,"FP1_012t",1)},
  {"FP1_002",(g,u,id)=>SummonAtDeath(g,u,"FP1_002t",2)},
  {"FP1_011",NaxxRules.Death},{"FP1_023",NaxxRules.Death},{"FP1_022",NaxxRules.Death},{"FP1_026",NaxxRules.Death},{"FP1_004",NaxxRules.Death},{"FP1_009",NaxxRules.Death},
  {"EX1_556",(g,u,id)=>SummonAtDeath(g,u,"VAN_skele21",1)},
  {"EX1_096",(g,u,id)=>g.Draw(u.Owner,1)},
  {"EX1_012",(g,u,id)=>g.Draw(u.Owner,1)},
  {"EX1_029",(g,u,id)=>g.Damage(new MatchTarget(1-u.Owner),2)},
  {"EX1_383",(g,u,id)=>g.EquipRaw(u.Owner,"GAME_ASHBRINGER",5,3,false)},
  {"EX1_110",(g,u,id)=>SummonAtDeath(g,u,"VAN_EX1_110t",1)},
  {"EX1_016",(g,u,id)=>g.StealRandomEnemyMinion(u.Owner)},
  {"EX1_097",(g,u,id)=>{foreach(var target in g.Characters(0).Concat(g.Characters(1)).ToArray())g.Damage(target,2);}},
  {"EX1_534",(g,u,id)=>SummonAtDeath(g,u,"GAME_HYENA",2)},
  {"EX1_577",(g,u,id)=>g.Summon(1-u.Owner,"VAN_EX1_finkle")}
 };
 static DeathrattleRules(){
  foreach(var id in new[]{"LOE_012","LOE_046","LOE_050","LOE_061","LOE_089"})effects.Add(id,ExplorerRules.Death);
 }
 public static string[] CardIds{get{return effects.Keys.ToArray();}}
 internal static bool IsAttachment(Enchantment e){
  return e.Kind=="forest-deathrattle"||e.Kind=="ancestral-spirit"||e.Kind==ExplorerRules.HatDeathrattle||e.Kind==ExplorerRules.CopiedDeathrattle;
 }
 internal static bool HasDeathrattle(MatchEngine game,BattleUnit unit){
  return unit!=null&&unit.Health>0&&((!unit.Silenced&&effects.ContainsKey(game.Card(unit.CardId).BaseId))||
   unit.Enchantments.Items.Any(IsAttachment));
 }
 internal static void Copy(MatchEngine game,BattleUnit source,BattleUnit recipient){
  if(!HasDeathrattle(game,source))return;
  string id=game.Card(source.CardId).BaseId;
  var attachments=source.Enchantments.Items.Where(IsAttachment).Select(e=>new Enchantment{Kind=e.Kind,Amount=e.Amount,CardId=e.CardId}).ToArray();
  if(!source.Silenced&&effects.ContainsKey(id))
   recipient.Enchantments.Items.Add(new Enchantment{Kind=ExplorerRules.CopiedDeathrattle,CardId=id,Amount=1});
  recipient.Enchantments.Items.AddRange(attachments);
 }
 public static void Resolve(MatchEngine game,BattleUnit unit){
  int repeats=NaxxRules.DeathrattleCount(game,unit.Owner);
  string id=game.Card(unit.CardId).BaseId;
  if(!unit.Silenced)ResolveNative(game,unit,id,repeats);
  foreach(var e in unit.Enchantments.Items.Where(IsAttachment).ToArray()){
   if(e.Kind==ExplorerRules.CopiedDeathrattle){ResolveNative(game,unit,e.CardId,repeats);continue;}
   game.Log.Add(game.PlayerLabel(unit.Owner)+" · 附加亡语："+game.Card(unit.CardId).Name);
   for(int n=0;n<repeats;n++){
    if(e.Kind==ExplorerRules.HatDeathrattle)game.GiveNaxxCard(unit.Owner,"LOE_105",publicIdentity:true);
    else SummonAtDeath(game,unit,e.Kind=="ancestral-spirit"?unit.CardId:"VAN_EX1_tk9",e.Amount);
   }
  }
 }
 static void ResolveNative(MatchEngine game,BattleUnit unit,string id,int repeats){
  Action<MatchEngine,BattleUnit,string> effect;
  if(!effects.TryGetValue(id,out effect))return;
  game.Log.Add(game.PlayerLabel(unit.Owner)+" · 亡语："+game.Card(unit.CardId).Name);
  for(int n=0;n<repeats;n++)game.DamageBatch(()=>effect(game,unit,id));
 }
 internal static void SummonAtDeath(MatchEngine game,BattleUnit unit,string id,int count){var board=game.Players[unit.Owner].Board;int position=board.FindIndex(u=>u.Id==unit.DeathAnchor);if(position<0)position=board.Count;for(int i=0;i<count;i++){if(game.Summon(unit.Owner,id,position)==null)break;position++;}}
}
public sealed partial class MatchEngine {
 public void DestroyAllMinions(){foreach(var unit in Players.SelectMany(p=>p.Board).ToArray())unit.DamageTaken=unit.MaxHealth;}
}
}
