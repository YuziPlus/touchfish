using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Touchfish;

internal static class ValueArena {
 static int Main(string[] args){
  try{
   var catalog=CardCatalog.Load(args[0]);
   var network=ValueNetwork.Load(args[1]);
   string output=args[2];Directory.CreateDirectory(output);
   string opponent=args[3];
   int begin=int.Parse(args[4]),count=int.Parse(args[5]),iterations=int.Parse(args[6]);
   string[] modes=args[7].Split(',');
   var own=ZooWarlockDeck.Create(catalog);
   var rival=MatchAgents.Find(opponent);
   var enemy=args.Length>8?DeckCode.Decode(catalog,args[8],"classic-2014"):rival.Deck(catalog);
   if(args.Length>9)own=DeckCode.Decode(catalog,args[9],"classic-2014");
   foreach(string name in modes){
    bool guided=name=="Puct"||name=="PuctNeural";
    var mode=guided?(name=="PuctNeural"?LeafValueMode.Neural:LeafValueMode.Heuristic):(LeafValueMode)Enum.Parse(typeof(LeafValueMode),name);
    string policyError;
    var policy=guided?PolicyNetwork.TryLoad(Environment.GetEnvironmentVariable("TOUCHFISH_POLICY_MODEL"),out policyError):null;
    if(guided&&policy==null)throw new InvalidDataException("Policy benchmark requires a compatible model.");
    var search=new ExtremeSearch(valueNetwork:network,valueMode:mode,policyNetwork:policy);
    for(int i=begin;i<begin+count;i++){
     string file=Path.Combine(output,name+"-"+i.ToString("D3")+".json");
     if(File.Exists(file))continue;
     int seed=20261014+7919*i,first=(i%2)^((i/2)%2);
     var game=new MatchEngine(catalog,own,enemy,seed,startingSeat:first);
     var ai=rival==null?null:rival.Create();
     var fixedSearch=new ExtremeSearch();
     var fixedRng=new SimulationRandom(seed ^ 0x527299);
     var opening=search.ChooseMulligan(game,0,own,enemy,seed+619039,samples:32);
     var otherOpening=ai==null?fixedSearch.ChooseMulligan(game,1,enemy,own,fixedRng.Next(int.MaxValue),samples:8).ReplaceIds:ai.Mulligan(AgentBridge.Observe(game,1));
     Require(game.Mulligan(0,opening.ReplaceIds));Require(game.Mulligan(1,otherOpening));
     // Preserve the previously recorded per-shard search RNG for the 100-game ice-mage experiment.
     int offset=i<24?0:24+(i-24)/4*4;
     var rng=new SimulationRandom(20261014+7919*offset+50000+i-offset);
     var budget=new SearchBudget{MaxIterations=iterations,MaxSamplingAttempts=iterations,MaxActions=12,MaxTreeDepth=8,MaxTurns=3,
      CollectDepthTelemetry=true,TimeLimitMilliseconds=int.Parse(Environment.GetEnvironmentVariable("TOUCHFISH_VALUE_ARENA_MS")??"0")};
     var trace=new System.Collections.Generic.List<object>();
     var actions=new System.Collections.Generic.List<string>();
     int decisions=0,fallbacks=0,failures=0,terminal=0,truncated=0,calls=0;
     double milliseconds=0,inference=0;
     for(int step=0;!game.Finished&&step<900;step++){
      if(game.Active==1&&ai==null){
       var move=fixedSearch.Choose(game,1,enemy,own,budget,fixedRng.Next(int.MaxValue)).BestMove;
       Require(move.Execute(game,1));
      }else if(game.Active==1){
       var move=ai.Choose(AgentBridge.Observe(game,1));
       if(move==null)throw new InvalidOperationException("Fixed opponent returned no action.");
       Require(AgentBridge.Execute(game,1,move));
      }else{
       var r=search.Choose(game,0,own,enemy,budget,rng.Next(int.MaxValue));
       if(r.BestMove==null)throw new InvalidOperationException("No search action.");
       var v=r.LeafValue;
       decisions++;failures+=r.SamplingFailures;milliseconds+=r.ElapsedMilliseconds;
       terminal+=r.CompletedRollouts;truncated+=r.TruncatedRollouts;
       if(r.FallbackReason!=null)fallbacks++;
       if(v!=null){calls+=v.Calls;inference+=v.Milliseconds;if(v.Failures!=0)throw new InvalidOperationException("Bad prediction.");}
       actions.Add(r.BestMove.ActionKey);
       trace.Add(new{step,game.Turn,r.Iterations,r.FallbackReason,r.ElapsedMilliseconds,r.CompletedRollouts,r.TruncatedRollouts,leaf=v,
        policy=r.Policy,r.NodesCreated,depth=r.DepthTelemetry==null?null:r.DepthTelemetry.DepthHistogram,
        expansion=r.DepthTelemetry==null?null:r.DepthTelemetry.NodesPerDepth,
        root=r.RootActionStatistics==null?null:r.RootActionStatistics.Select(a=>new{a.ActionKey,a.Visits,a.AvailabilityCount}).ToArray()});
       Require(r.BestMove.Execute(game,0));
      }
     }
     if(!game.Finished)throw new InvalidOperationException("Game cap reached.");
     JsonData.Write(file,new{i,seed,first,game.Winner,game.Turn,decisions,fallbacks,failures,terminal,truncated,calls,milliseconds,inference,actions,trace});
     Console.WriteLine("{0} {1} {2} winner={3} calls={4}",opponent,name,i,game.Winner,calls);
    }
   }
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static void Require(ActionResult result){if(!result.Success)throw new InvalidOperationException(result.Message);}
}

