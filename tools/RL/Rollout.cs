using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Touchfish;
internal static class Rollout {
 public sealed class Config {
  public int Games{get;set;}
  public int Seed{get;set;}
  public int Iterations{get;set;}
  public double Temperature{get;set;}
  public string[] Modes{get;set;}
  public string[] Models{get;set;}
  public string[] Pool{get;set;}
  public string EnemyDeck{get;set;}
  public Dictionary<string,string> Decks{get;set;}
 }
 static void Require(ActionResult r){if(!r.Success)throw new InvalidOperationException(r.Message);}
 public static int Main(string[] args){
  try{
   var cat=CardCatalog.Load(args[0]);var c=JsonData.Read<Config>(args[1]);string output=args[2];
   if(c.Games<1||!(c.Temperature>0)||double.IsInfinity(c.Temperature))throw new ArgumentException("Invalid rollout configuration");
   Directory.CreateDirectory(output);
   var policies=new PolicyNetwork[2];var hashes=new string[2];
   for(int s=0;s<2;s++)if(c.Models[s]!=null){
    string error;policies[s]=PolicyNetwork.TryLoad(c.Models[s],out error);
    if(policies[s]==null)throw new InvalidDataException(error);
    hashes[s]=SelfPlay.Hash(c.Models[s]);
   }
   var pool=c.Pool.Select(id=>c.Decks.ContainsKey(id)?DeckCode.Decode(cat,c.Decks[id],"classic-2014"):MatchAgents.Find(id).Deck(cat)).ToArray();
   var fixedDeck=c.EnemyDeck==null?null:MatchAgents.Find(c.EnemyDeck).Deck(cat);
   var schedule=new SimulationRandom(c.Seed);
   int seed=0,a=0,b=0;
   for(int g=0;g<c.Games;g++){
    if(g%2==0){seed=schedule.Next(int.MaxValue);a=schedule.Next(pool.Length);b=schedule.Next(pool.Length);}
    string path=Path.Combine(output,g.ToString("D5")+".json");
    if(File.Exists(path))continue;
    try{
     int first=g%2;
     var decks=new[]{pool[a],fixedDeck??pool[b]};
     var game=new MatchEngine(cat,decks[0],decks[1],seed,startingSeat:first);game.ClockNowMs=()=>0;
     var rng=new[]{new SimulationRandom(seed^398192),new SimulationRandom(seed^819163)};
     var search=policies.Select((p,s)=>new ExtremeSearch(policyNetwork:c.Modes[s]=="puct"?p:null)).ToArray();
     var bots=c.Modes.Select(m=>MatchAgents.Find(m)).Select(d=>d==null?null:d.Create()).ToArray();
     var rows=new List<object>();var replacements=new int[2][];
     var budget=new SearchBudget{MaxIterations=c.Iterations,MaxActions=12,MaxTurns=3,MaxTreeDepth=8};
     double[] ms=new double[2],inference=new double[2];int[] fallback=new int[2];
     for(int step=0;step<900&&!game.Finished;step++){
      bool opening=step<2;int s=opening?step:game.Active;
      var v=AgentBridge.Observe(game,s,false,true);
      var moves=opening?null:SearchPolicy.Actions(v);
      var masks=opening?Enumerable.Range(0,1<<v.Hand.Length).ToArray():null;
      var features=opening?masks.Select(m=>PolicyNetwork.EncodeAction("mulligan",-1,-1,-1,-1,null,null,null,null,Slots(v,m))).ToArray():
       moves.Select((m,i)=>PolicyNetwork.EncodeAction(v,m,i)).ToArray();
      if(features.Length==0)throw new InvalidOperationException("No legal action");
      var x=ValueNetwork.Encode(v);
      int pick=0;double[] probs=null;string mode=c.Modes[s];
      var watch=Stopwatch.StartNew();
      if(features.Length==1){mode="forced";probs=new[]{1.0};}
      else if(mode=="policy"){
       var logits=policies[s].Logits(x,features);
       double max=logits.Max();
       probs=logits.Select(l=>Math.Exp((l-max)/c.Temperature)).ToArray();double sum=probs.Sum();
       if(!(sum>0)||double.IsInfinity(sum))throw new InvalidDataException("Invalid distribution");
       int draw=rng[s].Next(int.MaxValue),lower=0;
       double cumulative=0;
       pick=-1;
       for(int i=0;i<probs.Length;i++){
        cumulative+=probs[i]/sum;
        int upper=i==probs.Length-1?int.MaxValue:(int)Math.Min(int.MaxValue,Math.Ceiling(cumulative*int.MaxValue));
        probs[i]=(upper-lower)/(double)int.MaxValue;
        if(draw>=lower&&draw<upper)pick=i;
        lower=upper;
       }
       inference[s]+=watch.Elapsed.TotalMilliseconds;
      }else if(opening){
       var ids=bots[s]==null?search[s].ChooseMulligan(game,s,decks[s],null,rng[s].Next(int.MaxValue),4).ReplaceIds:bots[s].Mulligan(v);
       pick=masks.Single(m=>Slots(v,m).Select(i=>v.Hand[i].Id).OrderBy(i=>i).SequenceEqual(ids.OrderBy(i=>i)));
      }else if(bots[s]!=null){
       var move=bots[s].Choose(v);
       pick=Array.FindIndex(moves,m=>m.ActionKey==SearchPolicy.ActionKey(v,move));
      }else{
       var result=search[s].Choose(game,s,decks[s],null,budget,rng[s].Next(int.MaxValue));
       inference[s]+=result.Policy==null?0:result.Policy.Milliseconds;
       if(result.FallbackReason!=null){mode="fallback";fallback[s]++;}
       pick=Array.FindIndex(moves,m=>m.ActionKey==result.BestMove.ActionKey);
      }
      ms[s]+=watch.Elapsed.TotalMilliseconds;
      if(pick<0)throw new InvalidOperationException("Action absent from legal candidates");
      bool eligible=mode=="policy";
      rows.Add(new{seat=s,turn=v.Turn,acting_model_hash=hashes[s],opponent_model_hash=hashes[1-s],behavior=mode,
       temperature=c.Temperature,eligible,x,features,pick,probs,logprob=probs==null?(double?)null:Math.Log(probs[pick]),
       kind=opening?"mulligan":moves[pick].Move==null?"decision":moves[pick].Move.Kind,mana=v.Self.Mana});
      if(opening){
       replacements[s]=Slots(v,pick).Select(i=>v.Hand[i].Id).ToArray();
       if(s==1){Require(game.Mulligan(0,replacements[0]));Require(game.Mulligan(1,replacements[1]));}
      }else Require(moves[pick].Execute(game,s));
     }
     if(!game.Finished)throw new InvalidOperationException("Decision cap");
     JsonData.Write(path,new{schema=1,features=ValueNetwork.Schema,actions=PolicyNetwork.Schema,seed,first,winner=game.Winner,turns=game.Turn,
      decks=new[]{c.Pool[a],c.EnemyDeck??c.Pool[b]},classes=decks.Select(d=>d.ClassId).ToArray(),models=hashes,modes=c.Modes,ms,inference,fallback,rows});
    }catch(Exception e){File.WriteAllText(path+".error",e.ToString());throw;}
   }
   return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
 static int[] Slots(AgentObservation v,int mask){return Enumerable.Range(0,v.Hand.Length).Where(i=>(mask&(1<<i))!=0).ToArray();}
}
