using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Touchfish;

internal static class PolicyProbe {
 static int Main(string[] args){
  var c=CardCatalog.Load(args[0]);
  var own=ZooWarlockDeck.Create(c);
  var enemy=FreezeMageDeck.Create(c);
  var fixtures=new List<object>();
  string diagnostic;
  var model=args.Length>2?PolicyNetwork.TryLoad(args[2],out diagnostic):null;
  for(int seed=3815;seed<3821;seed++){
   var game=new MatchEngine(c,own,enemy,seed);
   var zoo=new ZooWarlockAgent();var freeze=new FreezeMageAgent();
   game.Mulligan(0,zoo.Mulligan(AgentBridge.Observe(game,0)));
   game.Mulligan(1,freeze.Mulligan(AgentBridge.Observe(game,1)));
   var captured=new HashSet<int>();
   for(int step=0;step<200&&!game.Finished;step++){
    int seat=game.Active;
    var view=AgentBridge.Observe(game,seat);
    var actions=SearchPolicy.Actions(view);
    int phase=game.Turn<=6?0:game.Turn<=12?1:2;
    if(seat==0&&actions.Length>=3&&captured.Add(phase)){
     var search=new ExtremeSearch();
     var budgets=new[]{16,100,200};
     var results=budgets.Select(n=>search.Choose(game,0,own,enemy,new SearchBudget{MaxIterations=n,MaxActions=12,MaxTurns=3,MaxTreeDepth=8},seed+step)).ToArray();
     var sampleBuilder=DeterminizationBuilder.Capture(game,0);
     var belief=DeckBelief.Create(sampleBuilder.Information,own,enemy);
     var sampler=new BeliefSampler();
     var sampled=Enumerable.Range(0,8).Select(n=>sampler.Sample(belief,91000+seed+step+n)).Where(s=>s.Success).ToArray();
     var timed=model==null?null:new[]{new ExtremeSearch(),new ExtremeSearch(policyNetwork:model)}.Select(engine=>{
      var r=engine.Choose(game,0,own,enemy,new SearchBudget{MaxIterations=10000,TimeLimitMilliseconds=45,MaxActions=12,MaxTurns=3,MaxTreeDepth=8},seed+step);
      return new{r.Iterations,r.ElapsedMilliseconds,r.FallbackReason,best=r.BestMove.ActionKey};
     }).ToArray();
     var finalists=results.Select(r=>r.BestMove.ActionKey).Concat(timed==null?new string[0]:timed.Select(r=>r.best)).Distinct().ToArray();
     var counters=finalists.Select(key=>{
      var candidate=actions.Single(a=>a.ActionKey==key);
      int wins=0,losses=0,draws=0,legal=0,unfinished=0;
      foreach(var sample in sampled){
       var world=sampleBuilder.Build(sample);
       if(!candidate.Execute(world,0).Success)continue;
       legal++;
       for(int i=0;i<100&&!world.Finished;i++){
        int actor=world.ActiveSeat;
        var action=SearchPolicy.Choose(world.SearchView(actor,true),RolloutStyle.Heuristic,1041+i);
        if(action==null||!action.Execute(world,actor).Success)break;
       }
       if(!world.Finished)unfinished++;else if(world.Winner==0)wins++;else if(world.Winner==1)losses++;else draws++;
      }
      return new{key,legal,wins,losses,draws,unfinished};
     }).ToArray();
     fixtures.Add(new{seed,step,game.Turn,actions=actions.Length,results=results.Select((r,i)=>new{budget=budgets[i],r.Iterations,r.FallbackReason,best=r.BestMove.ActionKey,root=r.RootActionStatistics}),counters,timed});
    }
    var move=(seat==0?(IMatchAgent)zoo:freeze).Choose(view);
    if(move==null||!AgentBridge.Execute(game,seat,move).Success)break;
   }
  }
  JsonData.Write(args[1],fixtures);
  Console.WriteLine("Probed {0} observations",fixtures.Count);
  return 0;
 }
}
