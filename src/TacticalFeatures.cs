using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
internal enum EffectOperation { Damage, Draw, Heal, Freeze, Return, Summon }
internal sealed class EffectInstruction {
 internal readonly EffectOperation Operation;
 internal readonly int Amount;
 internal readonly string Token;
 internal EffectInstruction(EffectOperation operation,int amount,string token=null){Operation=operation;Amount=amount;Token=token;}
}
public static class EffectFeatures {
 public const int Schema=3, EffectWidth=25, Width=ValueNetwork.Features+24*EffectWidth;
 internal static float[] Card(MatchRules rules,CardRecord card){
  var x=new float[EffectWidth];
  var program=rules.EffectProgram(card);
  if(program!=null){
   x[0]=1;
   foreach(var op in program)x[1+(int)op.Operation]+=op.Amount/10f;
  }
  if(card.Mechanics!=null){
   x[7]=card.Mechanics.Contains("BATTLECRY")?1:0;
   x[8]=card.Mechanics.Contains("DEATHRATTLE")?1:0;
   x[9]=card.Mechanics.Contains("COMBO")?1:0;
  }
  foreach(var trigger in rules.Triggers)
   if(trigger.CardId==MatchRules.EffectKey(card))x[10+(int)trigger.Timing]=1;
  x[23]=card.Attack/12f;x[24]=card.Health/12f;
  return x;
 }
 internal static float[] Encode(AgentObservation v,MatchEngine game){
  var x=new List<float>(Width);
  x.AddRange(ValueNetwork.Encode(v));
  for(int i=0;i<10;i++)x.AddRange(i<v.Hand.Length?Card(game.Rules,game.Card(v.Hand[i].CardId)):new float[EffectWidth]);
  foreach(var p in new[]{v.Self,v.Enemy})
   for(int i=0;i<7;i++)x.AddRange(i<p.Board.Length?Card(game.Rules,game.Card(p.Board[i].CardId)):new float[EffectWidth]);
  return x.ToArray();
 }
}
public static class TacticalFeatures {
 public const int Schema=2;
 public const int Width=735;
 static readonly string[] Tags={"BATTLECRY","DEATHRATTLE","COMBO","TAUNT","DIVINE_SHIELD","CHARGE","STEALTH","WINDFURY","FREEZE","POISONOUS","SECRET","OVERLOAD","ENRAGE","SILENCE"};
 // Catalog lookup is restricted to cards already in the actor's observation.
 public static float[] Encode(AgentObservation view,Func<string,CardRecord> card){
  var x=new List<float>(ValueNetwork.Encode(view));
  for(int i=0;i<10;i++){
   var h=i<view.Hand.Length?view.Hand[i]:null;
   Append(x,h==null?null:card(h.CardId));
   x.Add(h==null?0:h.UndiscountedCost/10f);
  }
  foreach(var player in new[]{view.Self,view.Enemy})
   for(int i=0;i<7;i++)Append(x,i<player.Board.Length?card(player.Board[i].CardId):null);
  return x.ToArray();
 }
 static void Append(List<float> x,CardRecord c){
  x.Add(c==null?0:c.Attack/12f);x.Add(c==null?0:c.Health/12f);
  x.Add(c==null?0:c.SpellDamage/5f);x.Add(c==null?0:c.Durability/5f);
  foreach(string tag in Tags)x.Add(c!=null&&c.Mechanics!=null&&c.Mechanics.Contains(tag)?1:0);
 }
}
}

