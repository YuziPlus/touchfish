using System;
using System.Linq;
using System.Collections.Generic;
using Touchfish;
internal static class TacticalProbe {
 static CardCatalog catalog;
 static MatchEngine Make(int seed,string family){
  var d=FreezeMageDeck.Create(catalog);
  var g=new MatchEngine(catalog,d,d,seed);
  g.Mulligan(0,null);g.Mulligan(1,null);
  foreach(var p in g.Players){p.Hand.Clear();p.Deck.Clear();p.Mana=0;p.PowerUsed=true;p.Health=1;}
  // Ending the turn loses: enemy survives fatigue and starts with a ready lethal attacker.
  g.Players[1].Health=4+seed%3;
  if(family=="attack")g.Players[1].Health=1+seed%3;
  g.Players[1].MaxMana=9;
  g.Players[1].Deck.Add("VAN_CS2_231");
  g.Summon(1,"VAN_CS2_182");
  if(family=="effects"||family=="effect_draw"){
   g.Players[1].Health=2+seed%2;
   g.Players[1].Board.Clear();
   g.Summon(1,"VAN_NEW1_030");
   g.Players[0].Mana=2;
   var spells=family=="effect_draw"
    ?(seed<116?new[]{"VAN_CS2_024","VAN_CS2_023"}:new[]{"VAN_CS2_029","VAN_CS2_077"})
    :(seed<116?new[]{"VAN_CS2_024","VAN_CS2_089"}:new[]{"VAN_CS2_029","VAN_CS2_007"});
   if(seed%2==0)Array.Reverse(spells);
   foreach(var id in spells)g.GiveNaxxCard(0,id);
   if(seed>=116||family=="effect_draw"){
    int cost=seed>=116?4:2;
    g.Players[0].Mana=cost;
    foreach(var card in g.Players[0].Hand)card.CostAdjustment=cost-g.Card(card.CardId).Cost;
   }
   if(family=="effect_draw"){
    for(int i=0;i<4;i++)g.Players[0].Deck.Add("VAN_EX1_559");
   }
  }else if(family=="spell"){
   g.Players[0].Mana=4;
   g.GiveNaxxCard(0,"VAN_CS2_029");
   if(seed%2==0)g.GiveNaxxCard(0,"VAN_EX1_559");
  }else{
   var attacker=g.Summon(0,"VAN_CS2_182");attacker.SummonTurn=0;
   if(family=="two"){
    g.Players[1].Health=3+seed%2;
    attacker=g.Summon(0,"VAN_CS2_182");attacker.SummonTurn=0;
   }
  }
  if(seed%3==0)g.Summon(0,"VAN_CS2_231");
  return g;
 }
 sealed class Node {
  public float[] x,x2,x3;
  public float[][] a,a3;
  public string[] keys;
  public Node[] children;
  public double? reward;
  public double best;
 }
 static Node Tree(MatchEngine g,int depth){
  if(g.Finished){double r=g.Winner<0?.5:g.Winner==0?1:0;return new Node{reward=r,best=r};}
  if(depth>=4)throw new Exception("Unexpected fixture depth");
  var v=AgentBridge.Observe(g,g.Active,false,true);
  var actions=SearchPolicy.Actions(v);
  if(g.Active==1){
   // Exhaust the opponent reply: one proven terminal win suffices for its minimum.
   foreach(var a in actions){
    var fork=g.CopyForSimulation();
    if(!a.Execute(fork,1).Success)throw new Exception("Rejected opponent reply");
    if(fork.Winner==1)return new Node{reward=0,best=0};
   }
   throw new Exception("Missing forced opponent lethal");
  }
  var children=actions.Select(a=>{
   var fork=g.CopyForSimulation();
   if(!a.Execute(fork,0).Success)throw new Exception("Rejected candidate");
   return Tree(fork,depth+1);
  }).ToArray();
  return new Node{x=ValueNetwork.Encode(v),x2=TacticalFeatures.Encode(v,g.Card),x3=EffectFeatures.Encode(v,g),
   a=actions.Select((a,i)=>PolicyNetwork.EncodeAction(v,a,i)).ToArray(),
   a3=actions.Select((a,i)=>PolicyNetwork.EncodeEffectAction(g,v,a,i)).ToArray(),keys=actions.Select(a=>a.ActionKey).ToArray(),children=children,best=children.Max(c=>c.best)};
 }
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 sealed class Parity {
  public float[] x{get;set;}
  public float[][] a{get;set;}
  public double[] logits{get;set;}
 }
 static object Audit(){
  var g=Make(123,"spell");var h=g.Players[0].Hand[0];
  h.CardId="VAN_CS2_029";
  var a=AgentBridge.Observe(g,0,false,true);
  h.CardId="VAN_CS2_032"; // Flamestrike discounted to the same current cost does not match base cost.
  h.CardId="VAN_EX1_275"; // Cone of Cold is a four-cost spell in Classic.
  var b=AgentBridge.Observe(g,0,false,true);
  var g2=Make(124,"attack");
  var unit=g2.Players[0].Board[0];
  var before=AgentBridge.Observe(g2,0,false,true);
  unit.AttacksUsed=1;
  var after=AgentBridge.Observe(g2,0,false,true);
  unit.Frozen=true;unit.Shield=true;
  var status=AgentBridge.Observe(g2,0,false,true);
  g2.Players[1].Hand.Add(new HandCard{Id=900,CardId="VAN_CS2_029"});
  var hiddenA=AgentBridge.Observe(g2,0,false,true);
  g2.Players[1].Hand[0].CardId="VAN_CS2_023";
  var hiddenB=AgentBridge.Observe(g2,0,false,true);
  Check(TacticalFeatures.Encode(hiddenA,g2.Card).SequenceEqual(TacticalFeatures.Encode(hiddenB,g2.Card)),"Hidden leak");
  Check(EffectFeatures.Encode(hiddenA,g2).SequenceEqual(EffectFeatures.Encode(hiddenB,g2)),"Effect schema hidden leak");
  var cards=catalog.Cards.Values.Where(c=>c.Collectible&&c.SetId=="classic-2014"&&c.Type=="MINION").GroupBy(c=>new{c.Cost,c.Attack,c.Health});
  var pair=cards.SelectMany(group=>group.SelectMany(left=>group.Where(right=>left.Id!=right.Id&&
    MatchEngine.Has(left,"BATTLECRY")!=MatchEngine.Has(right,"BATTLECRY")&&MatchEngine.Has(left,"CHARGE")==MatchEngine.Has(right,"CHARGE")).Select(right=>new[]{left,right}))).First();
  h.CardId=pair[0].Id;var m0=AgentBridge.Observe(g,0,false,true);
  h.CardId=pair[1].Id;var m1=AgentBridge.Observe(g,0,false,true);
  bool fixedCollision=!TacticalFeatures.Encode(m0,g.Card).SequenceEqual(TacticalFeatures.Encode(m1,g.Card));
  Check(fixedCollision&&ValueNetwork.Encode(m0).SequenceEqual(ValueNetwork.Encode(m1)),"Missing same-stat collision");
  Func<AgentObservation,object> parity=v=>new{v.Hand,v.Self,v.Enemy,
   cards=v.Hand.Select(c=>c.CardId).Concat(v.Self.Board.Select(c=>c.CardId)).Concat(v.Enemy.Board.Select(c=>c.CardId))
    .Distinct().ToDictionary(id=>id,id=>g.Card(id)),baseline=ValueNetwork.Encode(v),enhanced=TacticalFeatures.Encode(v,g.Card)};
  var targetA=new SearchAction("first",new AgentMove{Kind="play",Source=h.Id,Position=0,Target=new MatchTarget(1,g.Players[1].Board[0].Id)});
  var targetB=new SearchAction("second",new AgentMove{Kind="play",Source=h.Id,Position=1,Target=new MatchTarget(1)});
  Check(!PolicyNetwork.EncodeAction(m1,targetA,0).SequenceEqual(PolicyNetwork.EncodeAction(m1,targetB,0)),"Action collision");
  return new{same_stat=pair.Select(c=>new{c.Id,c.Name,c.Cost,c.Attack,c.Health,c.Mechanics}),fixedCollision,
   spell_collision=ValueNetwork.Encode(a).SequenceEqual(ValueNetwork.Encode(b)),
   attack_distinct=!ValueNetwork.Encode(before).SequenceEqual(ValueNetwork.Encode(after)),
   status_distinct=!ValueNetwork.Encode(after).SequenceEqual(ValueNetwork.Encode(status)),hidden_isolated=true,parity=new[]{parity(m0),parity(m1),parity(a),parity(b)},
   effects=new[]{"VAN_CS2_024","VAN_CS2_029","VAN_CS2_023","VAN_CS2_077","VAN_CS2_089","VAN_CS2_007","VAN_EX1_581","VAN_CS2_027","VAN_CS2_235","VAN_EX1_341"}
    .ToDictionary(id=>id,id=>EffectFeatures.Card(g.Rules,g.Card(id)))};
 }
 public static int Main(string[] args){
  catalog=CardCatalog.Load(args[0]);
  if(args.Length==3){
   string reason;
   var model=PolicyNetwork.TryLoad(args[2],out reason);
   Check(model!=null&&(model.ObservationSchema==2||model.ObservationSchema==3),reason??"Invalid schema");
   var expected=JsonData.Read<Parity>(args[1]);
   Check(model.Logits(expected.x,expected.a).Zip(expected.logits,(a,b)=>Math.Abs(a-b)).Max()<2e-5,"Python/C# schema 2 logit mismatch");
   Console.WriteLine("PASS schema "+model.ObservationSchema+" native inference parity");
   return 0;
  }
  var scenarios=new List<object>();
  foreach(string family in new[]{"spell","attack","two","effects","effect_draw"})for(int seed=100;seed<124;seed++)
   scenarios.Add(new{family,seed,tree=Tree(Make(seed,family),0)});
  JsonData.Write(args[1],new{audit=Audit(),scenarios});
  return 0;
 }
}
