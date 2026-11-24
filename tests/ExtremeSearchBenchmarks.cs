using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Touchfish;

public static partial class RuleTests {
 static void ExtremeSearchBenchmarks(){
  if(Environment.GetEnvironmentVariable("TOUCHFISH_SEARCH_SKIP_PERF")!="1"){
   foreach(var definition in MatchAgents.All)SearchMeasureBudget(definition,100,3);
   var mirror=MatchAgents.Find("miracle");
   foreach(int budget in new[]{500,2000,5000})SearchMeasureBudget(mirror,budget,3);
  }
  int games=SearchEnvInt("TOUCHFISH_SEARCH_GAMES",4);
  if(games==0)return;
  if(games<2||games%2!=0)throw new ArgumentException("The match count must be even and positive.");
  string rounds=Environment.GetEnvironmentVariable("TOUCHFISH_SEARCH_MATCH_BUDGETS")??"24";
  foreach(int budget in rounds.Split(',').Select(s=>Int32.Parse(s,CultureInfo.InvariantCulture))){
   if(budget<1)throw new ArgumentException("Match iterations must be positive.");
   foreach(string opponent in new[]{"random","heuristic","zoo","freeze","miracle"})
    SearchMatchPair(opponent,games,budget);
  }
 }

 static int SearchEnvInt(string key,int fallback){
  string value=Environment.GetEnvironmentVariable(key);
  return value==null?fallback:Int32.Parse(value,CultureInfo.InvariantCulture);
 }

 static void SearchMeasureBudget(LocalAgentDefinition definition,int iterations,int runs){
  var deck=definition.Deck(catalog);
  var game=BeliefGame(deck,deck,315087);
  game.Players[0].Mana=3;
  game.Players[1].Health=4;
  var attacker=game.Summon(0,"VAN_CS2_182");attacker.SummonTurn=0;
  var budget=new SearchBudget{MaxIterations=iterations,MaxSamplingAttempts=iterations,MaxActions=14,MaxTurns=3,MaxNodes=6000};
  searcher.Choose(game,0,deck,deck,TacticalBudget(15),401);
  AppDomain.MonitoringIsEnabled=true;
  GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
  long bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
  int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
  long steps=0,nodes=0,depth=0,tree=0,visits=0,failures=0,restricted=0;
  int best=0;
  var clock=Stopwatch.StartNew();
  for(int n=0;n<runs;n++){
   var result=searcher.Choose(game,0,deck,deck,budget,811001+n);
   visits+=result.Iterations;steps+=result.SimulationActions;nodes+=result.NodesCreated;
   depth+=result.TotalTreeDepth;tree+=result.EstimatedTreeBytes;failures+=result.SamplingFailures;restricted+=result.RestrictedDecisions;
   if(result.BestMove.Move.Kind=="attack"&&result.BestMove.Move.Target.Hero)best++;
  }
  clock.Stop();
  bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-bytes;
  long peak=Process.GetCurrentProcess().PeakWorkingSet64;
  Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
   "SEARCH PERF deck={0} budget={1} runs={2} ms/decision={3:F2} iterations/s={4:F0} actions/iteration={5:F2} depth={6:F2} actions/s={7:F0} nodes/decision={8:F0} allocation={9:F0} tree-estimate={10:F0} gc={11}/{12}/{13} peak-process={14} lethal={15}/{2} failures={16} restricted={17}",
   definition.Id,iterations,runs,clock.Elapsed.TotalMilliseconds/runs,visits/clock.Elapsed.TotalSeconds,
   (double)steps/visits,(double)depth/visits,steps/clock.Elapsed.TotalSeconds,(double)nodes/runs,(double)bytes/runs,
   (double)tree/runs,GC.CollectionCount(0)-g0,GC.CollectionCount(1)-g1,GC.CollectionCount(2)-g2,peak,best,failures,restricted));
 }

 static string OpeningCosts(MatchEngine game,int seat,int count){
  return String.Join("|",game.Players[seat].Hand.Take(count).Select(h=>game.Card(h.CardId).Cost));
 }
 static void RunZooBudgetMatch(){
  SearchMatchPair("zoo",SearchEnvInt("TOUCHFISH_SEARCH_GAMES",2),SearchEnvInt("TOUCHFISH_SEARCH_MATCH_BUDGETS",100),true);
 }

 static void SearchMatchPair(string opponent,int games,int iterations,bool detailed=false){
  int win=0,loss=0,draw=0,capped=0,decisions=0,unsupported=0,errors=0;
  long treeDepth=0,visits=0,nodes=0,attempts=0,failures=0,simulationActions=0,opponentPending=0;
  var reasons=new System.Collections.Generic.Dictionary<string,int>(StringComparer.Ordinal);
  double milliseconds=0;
  int seedStart=SearchEnvInt("TOUCHFISH_SEARCH_SEED_START",732001);
  var budget=new SearchBudget{MaxIterations=iterations,MaxSamplingAttempts=iterations,MaxActions=SearchEnvInt("TOUCHFISH_SEARCH_MAX_ACTIONS",12),MaxTreeDepth=SearchEnvInt("TOUCHFISH_SEARCH_TREE_DEPTH",8),MaxTurns=SearchEnvInt("TOUCHFISH_SEARCH_MAX_TURNS",3)};
  string reuseMode=Environment.GetEnvironmentVariable("TOUCHFISH_ZOO_REUSE");
  if(reuseMode!=null&&reuseMode!="cold"&&reuseMode!="fixed"&&reuseMode!="target")
   throw new ArgumentException("Invalid reuse mode.");
  for(int i=0;i<games;i++){
   long beforeVisits=visits,beforeNodes=nodes,beforeAttempts=attempts,beforeFailures=failures,beforeActions=simulationActions;
   int beforeDecisions=decisions,beforeUnsupported=unsupported,beforeErrors=errors;
   double beforeMilliseconds=milliseconds;
   var definition=MatchAgents.Find(opponent);
   var deck=(definition??MatchAgents.All[i/2%MatchAgents.All.Length]).Deck(catalog);
   var engine=new MatchEngine(catalog,deck,deck,seedStart+i,startingSeat:i%2);
   var enemy=definition==null?null:definition.Create();
   var session=reuseMode=="fixed"?new PersistentSearchSession(SearchReuseMode.FixedNewIterations):
    reuseMode=="target"?new PersistentSearchSession(SearchReuseMode.TargetEffectiveVisits):null;
   int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
   string openingMode=Environment.GetEnvironmentVariable("TOUCHFISH_ZOO_OPENING")??"search";
   bool fair=openingMode=="fair";
   if(!fair&&openingMode!="search")throw new ArgumentException("Unknown opening mode.");
   var opening0=AgentBridge.Observe(engine,0);
   var opening1=AgentBridge.Observe(engine,1);
   int openingWorlds=SearchEnvInt("TOUCHFISH_OPENING_WORLDS",32);
   var openingClock=Stopwatch.StartNew();
   var decision=fair?null:searcher.ChooseMulligan(engine,0,deck,definition==null?null:deck,
    unchecked(seedStart+i+619039),samples:openingWorlds);
   openingClock.Stop();
   var ids0=fair?OpeningMulligan.Curve(opening0):decision.ReplaceIds;
   if(detailed&&decision!=null){
    Console.WriteLine("MULLIGAN {0},{1},{2},{3}",iterations,seedStart+i,decision.Worlds,decision.FallbackReason??"");
    Console.WriteLine(String.Format(CultureInfo.InvariantCulture,"OPENING SEARCH {0},{1},{2},{3:R},{4}",
     openingWorlds,seedStart+i,decision.Worlds,openingClock.Elapsed.TotalMilliseconds,String.Join("|",ids0)));
   }
   var ids1=fair?OpeningMulligan.Curve(opening1):enemy==null?new int[0]:enemy.Mulligan(opening1);
   if(!engine.Mulligan(0,ids0).Success)throw new InvalidOperationException("Extreme mulligan failed.");
   string kept0=OpeningCosts(engine,0,opening0.Hand.Length);
   if(!engine.Mulligan(1,ids1).Success)throw new InvalidOperationException("Opponent mulligan failed.");
   string kept1=OpeningCosts(engine,1,opening1.Hand.Length);
   if(detailed)Console.WriteLine("OPENING {0},{1},{2},{3},{4},{5},{6},{7}",iterations,seedStart+i,ids0.Length,ids1.Length,
    String.Join("|",opening0.Hand.Select(h=>h.BaseCost)),String.Join("|",opening1.Hand.Select(h=>h.BaseCost)),kept0,kept1);
   var random=new SimulationRandom(seedStart+50000+i);
   for(int action=0;action<900&&!engine.Finished;action++){
    int actor=engine.Active;
    ActionResult played;
    if(actor==0){
     var watch=reuseMode==null?null:Stopwatch.StartNew();
     int searchSeed=random.Next(int.MaxValue);
     var result=session==null?searcher.Choose(engine,0,deck,deck,budget,searchSeed):session.Choose(engine,0,deck,deck,budget,searchSeed);
     decisions++;milliseconds+=result.ElapsedMilliseconds;visits+=result.Iterations;
     nodes+=result.NodesCreated;treeDepth+=result.TotalTreeDepth;attempts+=result.SamplingAttempts;
     failures+=result.SamplingFailures;simulationActions+=result.SimulationActions;opponentPending+=result.RestrictedDecisions;
     if(result.FallbackReason!=null){
      unsupported++;
      int previous;reasons.TryGetValue(result.FallbackReason,out previous);
      reasons[result.FallbackReason]=previous+1;
     }
     if(result.BestMove==null)throw new InvalidOperationException("No legal move in live benchmark.");
     played=session==null?result.BestMove.Execute(engine,0):session.Execute(engine,result.BestMove);
     if(watch!=null){
      watch.Stop();
      var reuse=result.Reuse;
      Console.WriteLine(String.Format(CultureInfo.InvariantCulture,"REUSE DECISION {0},{1},{2:R},{3},{4},{5},{6},{7},{8:R},{9},{10},{11:R},{12},{13},{14}",
       seedStart+i,action,watch.Elapsed.TotalMilliseconds,result.Iterations,reuse==null?0:reuse.InheritedVisits,
       reuse==null?result.NodesCreated:reuse.RetainedNodes,reuse==null?result.EstimatedTreeBytes:reuse.PeakTreeBytes,
       reuse==null?0:reuse.InvalidatedNodes,reuse==null?0:reuse.ValidationMilliseconds,reuse==null?"":reuse.InvalidationReason??"",
       reuse!=null&&reuse.Reused?1:0,result.ElapsedMilliseconds,result.CompletedRollouts,result.TruncatedRollouts,result.RejectedSimulationActions));
     }
    }else if(enemy==null){
     var option=SearchPolicy.Choose(AgentBridge.Observe(engine,1,false,true),
      opponent=="random"?RolloutStyle.Random:RolloutStyle.Heuristic,random.Next(int.MaxValue));
     played=option.Execute(engine,1);
    }else{
     var view=AgentBridge.Observe(engine,1);
     var move=enemy.Choose(view);
     played=move==null?new ActionResult():AgentBridge.Execute(engine,1,move);
     if(!played.Success){errors++;played=engine.EndTurn();}
    }
    if(!played.Success)throw new InvalidOperationException("Live benchmark rejected move: "+opponent+" seed="+(seedStart+i)+" action="+action+" "+played.Message);
   }
   if(engine.Winner==0)win++;else if(engine.Winner==1)loss++;else if(engine.Winner==-1)draw++;else capped++;
   if(reuseMode!=null)Console.WriteLine("REUSE GC {0},{1},{2},{3},{4}",seedStart+i,GC.CollectionCount(0)-gc0,GC.CollectionCount(1)-gc1,GC.CollectionCount(2)-gc2,Process.GetCurrentProcess().PeakWorkingSet64);
   if(detailed)Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
    "ZOO {0},{1},{2},{3},{4},{5},{6:R},{7},{8},{9},{10},{11},{12},{13}",
    iterations,seedStart+i,engine.FirstSeat,engine.Winner,engine.Turn,decisions-beforeDecisions,milliseconds-beforeMilliseconds,
    visits-beforeVisits,nodes-beforeNodes,attempts-beforeAttempts,failures-beforeFailures,unsupported-beforeUnsupported,
    simulationActions-beforeActions,errors-beforeErrors));
   Console.WriteLine("SEARCH GAME opponent={0} seed={1} first={2} winner={3} turn={4}",opponent,seedStart+i,engine.FirstSeat,engine.Winner,engine.Turn);
  }
  int finished=win+loss+draw;
  double rate=finished==0?0:(double)win/finished;
  double low=0,high=1;
  if(finished>0){
   const double z=1.95996398454;
   double divisor=1+z*z/finished;
   double center=(rate+z*z/(2*finished))/divisor;
   double half=z*Math.Sqrt((rate*(1-rate)+z*z/(4*finished))/finished)/divisor;
   low=Math.Max(0,center-half);high=Math.Min(1,center+half);
  }
  Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
   "SEARCH MATCH opponent={0} budget={1} games={2} w/l/d/cap={3}/{4}/{5}/{6} win={7:P1} wilson95={8:P1}..{9:P1} decisions={10} ms={11:F2} iterations={12:F2} nodes={13:F2} depth={14:F2} failures={15}/{16} restricted={17}/{10} sim-actions={18} private-pending={19} old-rejected={20}",
   opponent,iterations,games,win,loss,draw,capped,rate,low,high,decisions,milliseconds/decisions,
   (double)visits/decisions,(double)nodes/decisions,visits==0?0:(double)treeDepth/visits,failures,attempts,unsupported,
   simulationActions,opponentPending,errors));
  foreach(var reason in reasons.OrderByDescending(pair=>pair.Value))Console.WriteLine("SEARCH RESTRICTION {0}: {1} ({2})",opponent,reason.Key,reason.Value);
 }
}
