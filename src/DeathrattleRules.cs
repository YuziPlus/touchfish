using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
public static class DeathrattleRules {
 static readonly Dictionary<string,Action<MatchEngine,BattleUnit>> effects=new Dictionary<string,Action<MatchEngine,BattleUnit>>{
 {"GVG_076",GvgRules.Death}, {"GVG_078",GvgRules.Death}, {"GVG_082",GvgRules.Death}, {"GVG_096",GvgRules.Death}, {"GVG_105",GvgRules.Death}, {"GVG_110t",GvgRules.Death}, {"GVG_114",GvgRules.Death}, {"GVG_115",GvgRules.Death},
  {"FP1_001",NaxxRules.RemainingDeath},{"FP1_024",NaxxRules.RemainingDeath},{"FP1_029",NaxxRules.RemainingDeath},{"FP1_014",NaxxRules.RemainingDeath},{"FP1_015",NaxxRules.RemainingDeath},
  {"FP1_007",(g,u)=>SummonAtDeath(g,u,"FP1_007t",1)},{"FP1_012",(g,u)=>SummonAtDeath(g,u,"FP1_012t",1)},
  {"FP1_002",(g,u)=>SummonAtDeath(g,u,"FP1_002t",2)},
  {"FP1_011",NaxxRules.Death},{"FP1_023",NaxxRules.Death},{"FP1_022",NaxxRules.Death},{"FP1_026",NaxxRules.Death},{"FP1_004",NaxxRules.Death},{"FP1_009",NaxxRules.Death},
  {"EX1_556",(g,u)=>SummonAtDeath(g,u,"VAN_skele21",1)},
  {"EX1_096",(g,u)=>g.Draw(u.Owner,1)},
  {"EX1_012",(g,u)=>g.Draw(u.Owner,1)},
  {"EX1_029",(g,u)=>g.Damage(new MatchTarget(1-u.Owner),2)},
  {"EX1_383",(g,u)=>g.EquipRaw(u.Owner,"GAME_ASHBRINGER",5,3,false)},
  {"EX1_110",(g,u)=>SummonAtDeath(g,u,"VAN_EX1_110t",1)},
  {"EX1_016",(g,u)=>g.StealRandomEnemyMinion(u.Owner)},
  {"EX1_097",(g,u)=>{foreach(var target in g.Characters(0).Concat(g.Characters(1)).ToArray())g.Damage(target,2);}},
  {"EX1_534",(g,u)=>SummonAtDeath(g,u,"GAME_HYENA",2)},
  {"EX1_577",(g,u)=>g.Summon(1-u.Owner,"VAN_EX1_finkle")}
 };
 public static string[] CardIds{get{return effects.Keys.ToArray();}}
 public static void Resolve(MatchEngine game,BattleUnit unit){int repeats=NaxxRules.DeathrattleCount(game,unit.Owner);Action<MatchEngine,BattleUnit> effect;if(!unit.Silenced&&effects.TryGetValue(game.Card(unit.CardId).BaseId,out effect)){game.Log.Add(game.PlayerLabel(unit.Owner)+" · 亡语："+game.Card(unit.CardId).Name);for(int n=0;n<repeats;n++)game.DamageBatch(()=>effect(game,unit));}foreach(var enchantment in unit.Enchantments.Items.Where(e=>e.Kind=="forest-deathrattle"||e.Kind=="ancestral-spirit").ToArray()){game.Log.Add(game.PlayerLabel(unit.Owner)+" · "+(enchantment.Kind=="ancestral-spirit"?"先祖之魂":"丛林之魂")+"亡语："+game.Card(unit.CardId).Name);for(int n=0;n<repeats;n++)SummonAtDeath(game,unit,enchantment.Kind=="ancestral-spirit"?unit.CardId:"VAN_EX1_tk9",enchantment.Amount);}}
 static void SummonAtDeath(MatchEngine game,BattleUnit unit,string id,int count){var board=game.Players[unit.Owner].Board;int position=board.FindIndex(u=>u.Id==unit.DeathAnchor);if(position<0)position=board.Count;for(int i=0;i<count;i++){if(game.Summon(unit.Owner,id,position)==null)break;position++;}}
}
public sealed partial class MatchEngine {
 public void DestroyAllMinions(){foreach(var unit in Players.SelectMany(p=>p.Board).ToArray())unit.DamageTaken=unit.MaxHealth;}
}
}
