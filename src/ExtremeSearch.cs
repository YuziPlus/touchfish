using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Touchfish {
public enum RolloutStyle { Random, Heuristic }

public sealed class SearchBudget {
 public int MaxIterations=300,MaxTreeDepth=12,MaxActions=28,MaxTurns=6,MaxNodes=2000;
 public int TimeLimitMilliseconds,MaxSamplingAttempts,MaxConsecutiveFailures=12;
 public double Exploration=0.7,WideningConstant=2,WideningExponent=0.5;
 public bool ProgressiveWidening=true;
 internal bool CollectDepthTelemetry=false;
 public RolloutStyle Rollout=RolloutStyle.Heuristic;
 internal SearchBudget CheckedCopy(){
  if(MaxIterations<1||MaxTreeDepth<1||MaxActions<1||MaxTurns<1||MaxNodes<1||TimeLimitMilliseconds<0||
     MaxSamplingAttempts<0||MaxConsecutiveFailures<1||Double.IsNaN(Exploration)||Double.IsInfinity(Exploration)||Exploration<0||
     Double.IsNaN(WideningConstant)||Double.IsInfinity(WideningConstant)||WideningConstant<=0||
     Double.IsNaN(WideningExponent)||WideningExponent<=0||WideningExponent>1||!Enum.IsDefined(typeof(RolloutStyle),Rollout))
   throw new ArgumentException("Invalid search budget.");
  var copy=(SearchBudget)MemberwiseClone();
  if(copy.MaxSamplingAttempts==0)copy.MaxSamplingAttempts=(int)Math.Min(int.MaxValue,(long)MaxIterations+MaxConsecutiveFailures);
  return copy;
 }
}

public sealed class SearchAction {
 public readonly string ActionKey,OptionId;
 public readonly int DecisionId;
 readonly AgentMove move;
 internal SearchAction(string key,AgentMove move,int decisionId=0,string optionId=null){
  ActionKey=key;this.move=Copy(move);DecisionId=decisionId;OptionId=optionId;
 }
 public AgentMove Move{get{return Copy(move);}}
 internal AgentMove SimulatedMove{get{return move;}}
 static AgentMove Copy(AgentMove source){
  return source==null?null:new AgentMove{Kind=source.Kind,Key=source.Key,Choice=source.Choice,Source=source.Source,
   Cost=source.Cost,Position=source.Position,Damage=source.Damage,
   Target=source.Target==null?null:new MatchTarget(source.Target.Seat,source.Target.UnitId)};
 }
 internal ActionResult Execute(FastSimulation world,int seat){
  return OptionId==null?world.Apply(seat,move):world.SubmitDecision(seat,DecisionId,OptionId);
 }
 public ActionResult Execute(MatchEngine live,int seat){
  return OptionId==null?AgentBridge.Execute(live,seat,move):AgentBridge.SubmitDecision(live,seat,DecisionId,OptionId);
 }
}

public sealed class RootActionStatistics {
 public string ActionKey;
 public int Visits,AvailabilityCount,TerminalWinCount,TerminalLossCount,TerminalDrawCount,EstimatedCount;
 public double MeanValue;
 public double? EstimatedValue;
}

internal sealed class SearchDepthTelemetry {
 public readonly int[] DepthHistogram,NodesPerDepth;
 public int AtDepthLimit,DepthLimitWithRollout,TreeActions,RolloutActions;
 internal SearchDepthTelemetry(int maximum){
  DepthHistogram=new int[maximum+1];
  NodesPerDepth=new int[maximum+1];
 }
}

public sealed class SearchResult {
 public SearchAction BestMove;
 public int Iterations,NodesCreated,EdgesCreated,SimulationActions,CompletedRollouts,TruncatedRollouts;
 public int SamplingAttempts,SamplingFailures,SuccessfulSamples,RestrictedDecisions,RejectedSimulationActions,TotalTreeDepth;
 public bool Cancelled;
 public LeafValueMetrics LeafValue;
 public PolicyMetrics Policy;
 public string StopReason,FallbackReason;
 public double ElapsedMilliseconds;
 public long EstimatedTreeBytes;
 public SearchReuseStatistics Reuse;
 internal SearchDepthTelemetry DepthTelemetry;
 internal bool UnsafeReuseIteration;
 public Dictionary<string,int> SamplingFailureReasons=new Dictionary<string,int>(StringComparer.Ordinal);
 public RootActionStatistics[] RootActionStatistics;
}

internal sealed class SearchEdge {
 internal int Visits,Available,Wins,Losses,Draws,Estimates;
 internal double Value,EstimateValue;
 internal Dictionary<string,SearchNode> Outcomes;
 internal void Record(double reward,int? winner,int rootSeat){
  Visits++;Value+=reward;
  if(!winner.HasValue){Estimates++;EstimateValue+=reward;}
  else if(winner.Value<0)Draws++;
  else if(winner.Value==rootSeat)Wins++;
  else Losses++;
 }
 internal RootActionStatistics Describe(string key){
  return new RootActionStatistics{ActionKey=key,Visits=Visits,AvailabilityCount=Available,TerminalWinCount=Wins,
   TerminalLossCount=Losses,TerminalDrawCount=Draws,EstimatedCount=Estimates,MeanValue=Visits==0?0:Value/Visits,
   EstimatedValue=Estimates==0?(double?)null:EstimateValue/Estimates};
 }
}
internal sealed class SearchNode {
 internal string PolicyKey;
 internal float[] PolicyObservation;
 internal double[] PolicyPrior;
 internal readonly int Actor;
 internal readonly Dictionary<string,SearchEdge> Edges=new Dictionary<string,SearchEdge>(StringComparer.Ordinal);
 internal int Visits,NonTerminalVisits;
 internal string CompatibilityKey;
 internal bool CompatibilityConflict;
 internal SearchNode(int actor){Actor=actor;}
}

public sealed class ExtremeSearch {
 readonly IBeliefSampler sampler;
 readonly ValueNetwork valueNetwork;
 readonly LeafValueMode valueMode;
 readonly PolicyNetwork policyNetwork;
 public OpeningMulliganResult ChooseMulligan(MatchEngine live,int seat,DeckDocument own,DeckDocument opponent,int seed,
  int samples=32,CancellationToken cancellation=default(CancellationToken),IDeckPrior prior=null){
  if(live==null)throw new ArgumentNullException("live");
  if(seat<0||seat>1)throw new ArgumentOutOfRangeException("seat");
  if(samples<1||samples>1024)throw new ArgumentOutOfRangeException("samples");
  if(live.Players[seat].MulliganDone)throw new InvalidOperationException("该席位已经确认换牌。");
  var view=AgentBridge.Observe(live,seat,false,false);
  if(cancellation.IsCancellationRequested)return OpeningMulligan.Fallback(view,"换牌搜索已取消。");
  DeterminizationBuilder builder;
  try{builder=DeterminizationBuilder.CaptureOpening(live,seat);}
  catch(InvalidOperationException ex){return OpeningMulligan.Fallback(view,ex.Message);}
  var belief=DeckBelief.Create(builder.Information,own,opponent,prior);
  return OpeningMulligan.Search(view,builder,belief,sampler,seed,samples,cancellation);
 }
 public ExtremeSearch(IBeliefSampler sampler=null,ValueNetwork valueNetwork=null,LeafValueMode valueMode=LeafValueMode.Heuristic,PolicyNetwork policyNetwork=null){
  this.sampler=sampler??new BeliefSampler();this.valueNetwork=valueNetwork;this.valueMode=valueMode;
  this.policyNetwork=policyNetwork;
 }

 // Trusted match adapter. The search loop below only sees root observations and sampled simulations.
 public SearchResult Choose(MatchEngine live,int seat,DeckDocument own,DeckDocument opponent,SearchBudget budget,int seed,
  CancellationToken cancellation=default(CancellationToken),IDeckPrior prior=null,string[] knownOwnTop=null){
  return ChoosePersistent(live,seat,own,opponent,budget,seed,cancellation,prior,knownOwnTop,null);
 }

 internal SearchResult ChoosePersistent(MatchEngine live,int seat,DeckDocument own,DeckDocument opponent,SearchBudget budget,int seed,
  CancellationToken cancellation,IDeckPrior prior,string[] knownOwnTop,PersistentSearchSession session){
  var clock=Stopwatch.StartNew();
  var view=AgentBridge.Observe(live,seat,false,true);
  DeterminizationBuilder builder=null;
  DeckBelief belief=null;
  string reason=null;
  if((session!=null||SearchPolicy.Actions(view).Length>1)&&!cancellation.IsCancellationRequested){
   try{
    builder=DeterminizationBuilder.Capture(live,seat,knownOwnTop);
    belief=DeckBelief.Create(builder.Information,own,opponent,prior);
   }catch(InvalidOperationException ex){reason=ex.Message;}
  }
  return SearchCore(view,builder,belief,budget,seed,cancellation,reason,clock,cancellation.IsCancellationRequested?null:session,live);
 }

 public SearchResult Search(AgentObservation root,DeterminizationBuilder builder,DeckBelief belief,SearchBudget budget,int seed,
  CancellationToken cancellation=default(CancellationToken)){
  return SearchCore(root,builder,belief,budget,seed,cancellation,null,Stopwatch.StartNew());
 }

 SearchResult SearchCore(AgentObservation root,DeterminizationBuilder builder,DeckBelief belief,SearchBudget options,int seed,
  CancellationToken token,string captureError,Stopwatch clock,PersistentSearchSession session=null,MatchEngine live=null){
  if(root==null||options==null)throw new ArgumentNullException("root/options");
  var budget=options.CheckedCopy();
  var result=new SearchResult();
  if(budget.CollectDepthTelemetry)result.DepthTelemetry=new SearchDepthTelemetry(Math.Min(budget.MaxActions,budget.MaxTreeDepth));
  var rootActions=SearchPolicy.Actions(root);
  SearchNode rootNode=null;
  int inheritedNodes=0;
  long maxBytes=long.MaxValue;
  if(session!=null){
   result.Reuse=new SearchReuseStatistics();
   rootNode=session.Prepare(live,root,belief,budget,result.Reuse);
   inheritedNodes=result.Reuse.RetainedNodes;
   result.EstimatedTreeBytes=result.Reuse.RetainedBytes;
   maxBytes=session.MaxBytes;
  }
  if(rootNode==null){
   rootNode=new SearchNode(root.Seat);
   result.NodesCreated=1;
   if(result.DepthTelemetry!=null)result.DepthTelemetry.NodesPerDepth[0]++;
   result.EstimatedTreeBytes=128;
  }
  if(rootActions.Where(action=>!rootNode.Edges.ContainsKey(action.ActionKey)).Sum(action=>SearchTreeReuse.EdgeBytes(action.ActionKey))
    >maxBytes-result.EstimatedTreeBytes){
   result.StopReason=result.FallbackReason="tree-memory-limit";
   result.BestMove=rootActions.OrderByDescending(action=>SearchPolicy.Priority(root,action)).FirstOrDefault();
   result.RootActionStatistics=new RootActionStatistics[0];
   result.Reuse.InvalidatedNodes+=inheritedNodes;
   result.Reuse.InheritedVisits=result.Reuse.ReusedNodes=0;
   result.Reuse.Reused=false;
   result.Reuse.InvalidationReason="tree-memory-limit";
   result.EstimatedTreeBytes=0;
   session.Finish(null,result.BestMove,result.Reuse);
   result.ElapsedMilliseconds=clock.Elapsed.TotalMilliseconds;
   return result;
  }
  foreach(var action in rootActions)Edge(rootNode,action.ActionKey,result);
  int newBudget=session==null?budget.MaxIterations:session.NewBudget(budget.MaxIterations,result.Reuse.InheritedVisits);
  int streamSeed=session==null?seed:session.ContinuationSeed(seed,result.Reuse.InheritedVisits);
  var rng=new SimulationRandom(streamSeed);
  string reason=captureError;
  if(rootActions.Length==0)result.StopReason="no-legal-action";
  else if(token.IsCancellationRequested){result.Cancelled=true;result.StopReason="cancelled";}
  else if(rootActions.Length==1)result.StopReason="single-action";
  else if(builder==null||belief==null||!ReferenceEquals(builder.Information,belief.Information)||builder.Information.Seat!=root.Seat)
   reason=reason??"缺少匹配的信念状态。";
  else if(!belief.Ready){
   reason=belief.Diagnostic;
  }else{
   string rootInformation=SearchPolicy.InformationKey(root);
   var rootMap=rootActions.ToDictionary(action=>action.ActionKey,StringComparer.Ordinal);
   int consecutive=0;
   while(result.Iterations<newBudget&&result.SamplingAttempts<budget.MaxSamplingAttempts){
    if(Interrupted(budget,clock,token,result))break;
    result.SamplingAttempts++;
    var sample=sampler.Sample(belief,rng.Next(int.MaxValue));
    if(sample==null||!sample.Success||!ReferenceEquals(sample.Information,belief.Information)){
     RecordFailure(result,sample==null?"采样器未返回结果。":sample.Diagnostic??"采样结果与当前信念不匹配。");
     if(++consecutive>=budget.MaxConsecutiveFailures){reason="连续采样失败达到上限。";break;}
     continue;
    }
    var world=builder.Build(sample);
    var sampledRoot=world.SearchView(root.Seat,true);
    if(SearchPolicy.InformationKey(sampledRoot)!=rootInformation){
     RecordFailure(result,"假想世界与根观察不匹配。");
     if(++consecutive>=budget.MaxConsecutiveFailures){reason="连续采样失败达到上限。";break;}
     continue;
    }
    consecutive=0;
    result.SuccessfulSamples++;
    var policyRandom=new SimulationRandom(rng.Next(int.MaxValue));
    Iterate(world,sampledRoot,rootNode,rootMap,root.Seat,budget,clock,token,policyRandom,result,inheritedNodes,maxBytes);
   }
  }
  if(result.StopReason==null)result.StopReason=reason!=null?"fallback":
   newBudget==0?"effective-budget":result.Iterations>=newBudget?"iteration-limit":result.SamplingAttempts>=budget.MaxSamplingAttempts?"sampling-limit":"no-samples";
  var best=rootActions.OrderByDescending(action=>rootNode.Edges[action.ActionKey].Visits)
   .ThenByDescending(action=>{var e=rootNode.Edges[action.ActionKey];return e.Visits==0?0:e.Value/e.Visits;})
   .ThenByDescending(action=>SearchPolicy.Priority(root,action)).FirstOrDefault();
  if(best!=null&&rootActions.Length>1&&rootNode.Edges[best.ActionKey].Visits==0)
   result.FallbackReason=reason??result.StopReason;
  result.BestMove=best;
  result.RootActionStatistics=rootActions.Select(action=>rootNode.Edges[action.ActionKey].Describe(action.ActionKey)).ToArray();
  if(session!=null){
   result.Reuse.NewIterations=result.Iterations;
   result.Reuse.EffectiveVisits=rootNode.Visits;
   result.Reuse.PeakTreeBytes=result.EstimatedTreeBytes;
   if(result.UnsafeReuseIteration){
    result.Reuse.InvalidatedNodes+=inheritedNodes+result.NodesCreated;
    result.Reuse.InvalidationReason="incomplete-iteration";
   }
   session.Finish(result.UnsafeReuseIteration?null:rootNode,result.BestMove,result.Reuse);
  }
  result.ElapsedMilliseconds=clock.Elapsed.TotalMilliseconds;
  return result;
 }

 static void RecordFailure(SearchResult result,string reason){
  result.SamplingFailures++;
  int count;result.SamplingFailureReasons.TryGetValue(reason,out count);
  result.SamplingFailureReasons[reason]=count+1;
 }
 static bool Interrupted(SearchBudget budget,Stopwatch clock,CancellationToken token,SearchResult result){
  if(token.IsCancellationRequested){result.Cancelled=true;result.StopReason="cancelled";return true;}
  if(budget.TimeLimitMilliseconds>0&&clock.ElapsedMilliseconds>=budget.TimeLimitMilliseconds){
   result.StopReason="time-limit";return true;
  }
  return false;
 }
 static SearchEdge Edge(SearchNode node,string key,SearchResult result){
  SearchEdge edge;
  if(!node.Edges.TryGetValue(key,out edge)){
   edge=new SearchEdge();node.Edges.Add(key,edge);result.EdgesCreated++;result.EstimatedTreeBytes+=112+2L*key.Length;
  }
  return edge;
 }

 void Iterate(FastSimulation world,AgentObservation sampledRoot,SearchNode root,Dictionary<string,SearchAction> rootMap,int rootSeat,
  SearchBudget budget,Stopwatch clock,CancellationToken token,SimulationRandom random,SearchResult result,int inheritedNodes,long maxBytes){
  int initialTurn=world.Turn;
  var path=new List<SearchEdge>();
  var nodes=new List<SearchNode>();
  SearchNode node=root;
  int actions=0;
  while(!world.Finished&&actions<budget.MaxActions&&world.Turn-initialTurn<budget.MaxTurns){
   if(Interrupted(budget,clock,token,result)){
    if(path.Count>0)result.UnsafeReuseIteration=true;
    break;
   }
   int actor=world.ActiveSeat;
   // Current P1.5 decisions belong to the active seat. Other future owners
   // intentionally stop this search until a public routing contract exists.
   if(world.HasPendingDecision&&actor!=rootSeat){
    result.RestrictedDecisions++;break;
   }
   var view=actions==0?sampledRoot:world.SearchView(actor,true);
   if(world.HasPendingDecision&&view.Decision==null){result.RestrictedDecisions++;break;}
   var available=SearchPolicy.Actions(view);
   if(actions==0)available=available.Where(action=>rootMap.ContainsKey(action.ActionKey)).ToArray();
   if(available.Length==0)break;
   SearchAction chosen;
   SearchEdge chosenEdge=null;
   if(node!=null&&path.Count<budget.MaxTreeDepth){
    long additional=0;
    if(result.Reuse!=null){
     if(actor==rootSeat){
      if(path.Count>0&&!node.CompatibilityConflict){
       string compatibility=world.ReuseObservationKey(view,available);
       if(node.CompatibilityKey==null){
        long size=SearchTreeReuse.KeyBytes(compatibility);
        if(size<=maxBytes-result.EstimatedTreeBytes){
         node.CompatibilityKey=compatibility;result.EstimatedTreeBytes+=size;
        }else node.CompatibilityConflict=true;
       }else if(node.CompatibilityKey!=compatibility)node.CompatibilityConflict=true;
      }
      additional=available.Where(action=>!node.Edges.ContainsKey(action.ActionKey)).Sum(action=>SearchTreeReuse.EdgeBytes(action.ActionKey));
     }else if(!node.Edges.ContainsKey("opponent-response"))additional=SearchTreeReuse.EdgeBytes("opponent-response");
    }
    if(additional>maxBytes-result.EstimatedTreeBytes){
     node=null;
     chosen=SearchPolicy.Pick(view,available,budget.Rollout,random);
    }else if(actor==rootSeat){
     chosen=policyNetwork==null||policyNetwork.ObservationSchema!=ValueNetwork.Schema?Select(node,view,available,budget,random,result):SelectPuct(node,view,available,budget,random,result);
     chosenEdge=node.Edges[chosen.ActionKey];
    }else{
     // One public response edge. The policy never uses root-private information
     // or statistics conditioned on the root's hidden hand.
     chosen=SearchPolicy.Pick(view,available,budget.Rollout,random);
     chosenEdge=Edge(node,"opponent-response",result);
     chosenEdge.Available++;
    }
   }else chosen=SearchPolicy.Pick(view,available,budget.Rollout,random);

   var execution=chosen.Execute(world,actor);
   if(!execution.Success){result.RejectedSimulationActions++;result.UnsafeReuseIteration=true;break;}
   actions++;result.SimulationActions++;
   if(result.DepthTelemetry!=null){
    if(chosenEdge==null)result.DepthTelemetry.RolloutActions++;
    else result.DepthTelemetry.TreeActions++;
   }
   if(chosenEdge==null){node=null;continue;}
   nodes.Add(node);
   path.Add(chosenEdge);
   if(world.Finished||path.Count>=budget.MaxTreeDepth){node=null;continue;}
   var rootView=world.SearchView(rootSeat,false);
   string outcome=(actor==rootSeat?"":SearchPolicy.PublicResponse(view,chosen))+"|"+SearchPolicy.InformationKey(rootView);
   if(chosenEdge.Outcomes!=null&&chosenEdge.Outcomes.TryGetValue(outcome,out node))continue;
   node=null;
   if(inheritedNodes+result.NodesCreated<budget.MaxNodes&&
      160+2L*outcome.Length+(chosenEdge.Outcomes==null?104:0)<=maxBytes-result.EstimatedTreeBytes){
    if(chosenEdge.Outcomes==null){
     chosenEdge.Outcomes=new Dictionary<string,SearchNode>(StringComparer.Ordinal);
     result.EstimatedTreeBytes+=104;
    }
    node=new SearchNode(world.ActiveSeat);
    chosenEdge.Outcomes.Add(outcome,node);
    result.NodesCreated++;
    if(result.DepthTelemetry!=null)result.DepthTelemetry.NodesPerDepth[path.Count]++;
    result.EstimatedTreeBytes+=160+2L*outcome.Length;
    // Expand at most one new node per iteration.
    node=null;
   }
  }
  if(path.Count==0)return;
  int? winner=world.Finished?(int?)world.Winner:null;
  double reward=LeafReward(world,rootSeat,winner,result);
  if(winner.HasValue)result.CompletedRollouts++;else result.TruncatedRollouts++;
  result.Iterations++;
  result.TotalTreeDepth+=path.Count;
  if(result.DepthTelemetry!=null){
   var telemetry=result.DepthTelemetry;
   telemetry.DepthHistogram[path.Count]++;
   if(path.Count==budget.MaxTreeDepth){
    telemetry.AtDepthLimit++;
    if(actions>path.Count)telemetry.DepthLimitWithRollout++;
   }
  }
  foreach(var visited in nodes){visited.Visits++;if(!winner.HasValue)visited.NonTerminalVisits++;}
  foreach(var edge in path)edge.Record(reward,winner,rootSeat);
 }

 double LeafReward(FastSimulation world,int rootSeat,int? winner,SearchResult result){
  if(valueMode==LeafValueMode.Heuristic)
   return winner.HasValue?TerminalValue(winner.Value,rootSeat):SearchPolicy.Evaluate(world.SearchView(rootSeat,false));
  var metrics=result.LeafValue??(result.LeafValue=new LeafValueMetrics());
  if(winner.HasValue){metrics.Terminal++;return TerminalValue(winner.Value,rootSeat);}
  metrics.Truncated++;
  var view=world.SearchView(rootSeat,false);
  double heuristic=SearchPolicy.Evaluate(view);
  if(valueNetwork==null)return heuristic;
  long start=Stopwatch.GetTimestamp();
  metrics.Calls++;
  double prediction=valueNetwork.Predict(view);
  metrics.Milliseconds+=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
  if(double.IsNaN(prediction)||double.IsInfinity(prediction)||prediction<0||prediction>1){metrics.Failures++;return heuristic;}
  metrics.Sum+=prediction;
  metrics.Minimum=Math.Min(metrics.Minimum,prediction);
  metrics.Maximum=Math.Max(metrics.Maximum,prediction);
  return valueMode==LeafValueMode.Neural?prediction:heuristic;
 }
 internal static double TerminalValue(int winner,int rootSeat){return winner<0?0.5:winner==rootSeat?1:0;}

 SearchAction SelectPuct(SearchNode node,AgentObservation view,SearchAction[] options,SearchBudget budget,
  SimulationRandom random,SearchResult result){
  if(options.Length==1)return Select(node,view,options,budget,random,result);
  var metrics=result.Policy??(result.Policy=new PolicyMetrics());
  long start=Stopwatch.GetTimestamp();
  var encoded=ValueNetwork.Encode(view);
  // Cache only identical feature vectors and legal-action order within this node.
  var key=new System.Text.StringBuilder();
  foreach(var option in options)key.Append(option.ActionKey.Length).Append(':').Append(option.ActionKey);
  string signature=key.ToString();
  double[] prior;
  if(node.PolicyKey==signature&&encoded.SequenceEqual(node.PolicyObservation)){prior=node.PolicyPrior;metrics.CacheHits++;}
  else{
   metrics.Calls++;
   var candidates=options.Select((action,i)=>PolicyNetwork.EncodeAction(view,action,i)).ToArray();
   prior=PolicyNetwork.Probabilities(policyNetwork.Logits(encoded,candidates));
   if(prior!=null&&node.PolicyKey==null&&result.Reuse==null){
    node.PolicyKey=signature;node.PolicyPrior=prior;node.PolicyObservation=encoded;
   }
  }
  metrics.Milliseconds+=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
  if(prior==null){metrics.Failures++;return Select(node,view,options,budget,random,result);}
  int explored=0;
  foreach(var option in options){
   var edge=Edge(node,option.ActionKey,result);
   edge.Available++;
   if(edge.Visits>0)explored++;
  }
  int width=options.Length;
  if(budget.ProgressiveWidening)width=(int)Math.Min(width,Math.Max(1,Math.Ceiling(budget.WideningConstant*Math.Pow(node.Visits+1,budget.WideningExponent))));
  double best=Double.NegativeInfinity;
  int index=0;
  for(int i=0;i<options.Length;i++){
   var edge=node.Edges[options[i].ActionKey];
   if(edge.Visits==0&&explored>=width)continue;
   double q=edge.Visits==0?0.5:edge.Value/edge.Visits;
   // An action receives exploration credit only in worlds where it was available.
   double score=q+budget.Exploration*prior[i]*Math.Sqrt(edge.Available)/(1+edge.Visits);
   if(score>best){best=score;index=i;}
  }
  return options[index];
 }

 static SearchAction Select(SearchNode node,AgentObservation view,SearchAction[] options,SearchBudget budget,
  SimulationRandom random,SearchResult result){
  int explored=0;
  foreach(var action in options){
   var edge=Edge(node,action.ActionKey,result);
   edge.Available++;
   if(edge.Visits>0)explored++;
  }
  int width=options.Length;
  if(budget.ProgressiveWidening)width=(int)Math.Min(width,Math.Max(1,Math.Ceiling(budget.WideningConstant*Math.Pow(node.Visits+1,budget.WideningExponent))));
  bool admit=explored<width;
  double bestScore=Double.NegativeInfinity;
  SearchAction best=null;
  foreach(var action in options){
   var edge=node.Edges[action.ActionKey];
   if(edge.Visits==0&&!admit)continue;
   double score=edge.Visits==0?2+0.01*SearchPolicy.Priority(view,action)+random.Next(1000)*0.000001
    :edge.Value/edge.Visits+budget.Exploration*Math.Sqrt(Math.Log(edge.Available)/(1+edge.Visits));
   if(score>bestScore){bestScore=score;best=action;}
  }
  return best??options[0];
 }
}
}
