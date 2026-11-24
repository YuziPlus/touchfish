using System;
using System.Collections.Generic;
using System.Linq;
using Touchfish;

public static partial class RuleTests {
 sealed class DepthScene {
  public int GameSeed,Turn,Step;
  public AgentObservation View;
  public DeterminizationBuilder Builder;
  public DeckBelief Belief;
 }

 static List<DepthScene> ZooDepthScenes(){
  var scenes=new List<DepthScene>();
  var deck=MatchAgents.Find("zoo").Deck(catalog);
  var phases=new[]{new[]{3,0},new[]{4,1},new[]{7,0},new[]{8,1}};
  for(int seed=972101;seed<972160&&scenes.Count<20;seed++){
   var candidates=new List<DepthScene>();
   var engine=new MatchEngine(catalog,deck,deck,seed,startingSeat:seed%2);
   var agents=new[]{new ZooWarlockAgent(),new ZooWarlockAgent()};
   var selections=agents.Select((agent,seat)=>agent.Mulligan(AgentBridge.Observe(engine,seat))).ToArray();
   for(int seat=0;seat<2;seat++)if(!engine.Mulligan(seat,selections[seat]).Success)throw new Exception("Fixture mulligan failed.");
   int turn=engine.Turn,step=0;
   while(!engine.Finished&&engine.Turn<=8){
    if(engine.Turn!=turn){turn=engine.Turn;step=0;}
    int actor=engine.Active;
    if(phases.Any(p=>p[0]==turn&&p[1]==step)){
     var view=AgentBridge.Observe(engine,actor,false,true);
     var builder=DeterminizationBuilder.Capture(engine,actor);
     var belief=DeckBelief.Create(builder.Information,deck,deck);
     if(SearchPolicy.Actions(view).Length>1&&belief.Ready)
      candidates.Add(new DepthScene{GameSeed=seed,Turn=turn,Step=step,View=view,Builder=builder,Belief=belief});
    }
    var move=agents[actor].Choose(AgentBridge.Observe(engine,actor));
    if(!AgentBridge.Execute(engine,actor,move).Success)throw new Exception("Fixture action failed.");
    step++;
   }
   if(candidates.Count==phases.Length)scenes.AddRange(candidates);
  }
  if(scenes.Count!=20)throw new Exception("Failed to collect 20 qualifying Zoo positions.");
  return scenes;
 }

 static SearchBudget DepthBudget(int limit,bool telemetry){
  return new SearchBudget{MaxIterations=200,MaxSamplingAttempts=200,MaxActions=16,MaxTurns=6,
   MaxNodes=2000,MaxTreeDepth=limit,CollectDepthTelemetry=telemetry};
 }

 static void RunZooDepthProbe(){
  var scenes=ZooDepthScenes();
  int[] depths={8,12,32,64};
  const int seed=790083;
  var warmup=scenes[0];
  var plain=searcher.Search(warmup.View,warmup.Builder,warmup.Belief,DepthBudget(8,false),seed);
  var measured=searcher.Search(warmup.View,warmup.Builder,warmup.Belief,DepthBudget(8,true),seed);
  if(SearchKey(plain)!=SearchKey(measured))throw new Exception("Telemetry changed search behavior.");
  for(int index=0;index<scenes.Count;index++){
   var scene=scenes[index];
   for(int slot=0;slot<depths.Length;slot++){
    int depth=depths[(slot+index)%depths.Length];
    var result=searcher.Search(scene.View,scene.Builder,scene.Belief,DepthBudget(depth,true),seed);
    var t=result.DepthTelemetry;
    if(result.Iterations!=200||result.SamplingFailures!=0||result.RejectedSimulationActions!=0||
     t.DepthHistogram.Sum()!=result.Iterations||t.NodesPerDepth.Sum()!=result.NodesCreated||
     t.DepthHistogram.Select((n,d)=>n*d).Sum()!=t.TreeActions||t.TreeActions!=result.TotalTreeDepth||
     t.TreeActions+t.RolloutActions!=result.SimulationActions||t.NodesPerDepth.Skip(17).Sum()!=0)
     throw new Exception("Invalid depth accounting for "+scene.GameSeed+" turn "+scene.Turn+" depth "+depth);
    Console.WriteLine(JsonData.Serializer().Serialize(new{
     Scene=index+1,scene.GameSeed,scene.Turn,scene.Step,scene.View.Seat,RootActions=scene.View.Moves.Length,
     DepthLimit=depth,Seed=seed,result.Iterations,result.CompletedRollouts,result.NodesCreated,result.ElapsedMilliseconds,
     Recommended=result.BestMove.ActionKey,t.AtDepthLimit,t.DepthLimitWithRollout,t.DepthHistogram,t.NodesPerDepth,t.TreeActions,t.RolloutActions
    }));
   }
  }
 }
}

