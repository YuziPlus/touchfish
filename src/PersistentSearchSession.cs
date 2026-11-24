using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Touchfish {
public enum SearchReuseMode { FixedNewIterations, TargetEffectiveVisits }

public sealed class SearchReuseStatistics {
 public int InheritedVisits,NewIterations,EffectiveVisits,ReusedNodes,InvalidatedNodes,RetainedNodes;
 public long RetainedBytes,PeakTreeBytes;
 public double ValidationMilliseconds;
 public string InvalidationReason;
 public bool Reused;
}

public sealed class PersistentSearchSession {
 readonly ExtremeSearch search;
 internal readonly long MaxBytes;
 readonly SearchReuseMode mode;
 object matchToken;
 SearchNode root;
 SearchAction lastAction;
 string observation,configuration,posterior,history;
 int seat,historyCount;
 long version;
 bool advanced;
 int prunedNodes;
 double transitionMilliseconds;
 string transitionReason;

 public string MatchId{get;private set;}
 public int Seat{get{return matchToken==null?-1:seat;}}
 public long StateVersion{get{return version;}}
 public int PublicHistoryVersion{get{return historyCount;}}
 public SearchAction LastExecutedAction{get;private set;}
 public int ReuseCount{get;private set;}
 public string LastInvalidationReason{get;private set;}

 public PersistentSearchSession(SearchReuseMode mode=SearchReuseMode.FixedNewIterations,long maxTreeBytes=32*1024*1024){
  if(maxTreeBytes<1024||!Enum.IsDefined(typeof(SearchReuseMode),mode))throw new ArgumentException("Invalid persistent search limits.");
  this.mode=mode;MaxBytes=maxTreeBytes;search=new ExtremeSearch();
 }

 public SearchResult Choose(MatchEngine game,int viewer,DeckDocument own,DeckDocument opponent,SearchBudget budget,int seed,
  CancellationToken cancellation=default(CancellationToken),IDeckPrior prior=null,string[] knownOwnTop=null){
  return search.ChoosePersistent(game,viewer,own,opponent,budget,seed,cancellation,prior,knownOwnTop,this);
 }

 // Receipt is created by executing the exact action issued by this session.
 // No external success flag or observation can advance an unrelated root.
 public ActionResult Execute(MatchEngine game,SearchAction action){
  if(game==null)throw new ArgumentNullException("game");
  if(action==null||!ReferenceEquals(action,lastAction)||!ReferenceEquals(matchToken,SearchTreeReuse.MatchToken(game))||game.SearchStateVersion!=version)
   return new ActionResult{Message="搜索操作已过期。"};
  var watch=Stopwatch.StartNew();
  var before=AgentBridge.Observe(game,seat,false,true);
  if(game.ReuseObservationKey(before)!=observation||!SearchPolicy.Actions(before).Any(candidate=>SameExecution(candidate,action)))
   return new ActionResult{Message="搜索观察已过期。"};
  string restriction=posterior==null?"uncertified-belief":game.ReuseTransitionRestriction(seat,action);
  var result=action.Execute(game,seat);
  if(!result.Success)return result;
  lastAction=null;
  LastExecutedAction=action;
  version=game.SearchStateVersion;
  var after=AgentBridge.Observe(game,seat,false,true);
  observation=game.ReuseObservationKey(after);
  int oldNodes;long oldBytes;SearchTreeReuse.Measure(root,out oldNodes,out oldBytes);
  SearchNode next=null;
  if(String.IsNullOrEmpty(restriction)){
   if(game.AnimationPlaying||game.Finished||after.ActiveSeat!=seat||after.Turn!=before.Turn)
    restriction="turn-or-resolution-boundary";
   else{
    SearchEdge edge;
    if(root==null||!root.Edges.TryGetValue(action.ActionKey,out edge)||edge.Outcomes==null||
     !edge.Outcomes.TryGetValue("|"+SearchPolicy.InformationKey(after),out next))restriction="unseen-outcome";
    else if(next.Actor!=seat||next.CompatibilityConflict||next.CompatibilityKey!=observation)restriction="incompatible-observation";
    else if(next.Visits==0)restriction="unvisited-outcome";
    else if(next.NonTerminalVisits!=0||next.Edges.Values.Sum(e=>e.Visits)!=next.Visits)restriction="shorter-horizon-estimates";
   }
  }
  if(!String.IsNullOrEmpty(restriction))next=null;
  root=next;advanced=true;transitionReason=restriction;
  int keptNodes;long keptBytes;SearchTreeReuse.Measure(root,out keptNodes,out keptBytes);
  prunedNodes+=oldNodes-keptNodes;
  transitionMilliseconds+=watch.Elapsed.TotalMilliseconds;
  return result;
 }

 static bool SameExecution(SearchAction candidate,SearchAction issued){
  if(candidate.ActionKey!=issued.ActionKey||candidate.DecisionId!=issued.DecisionId)return false;
  var a=candidate.SimulatedMove;
  var b=issued.SimulatedMove;
  if(a==null||b==null)return a==b;
  return a.Source==b.Source&&(a.Target==null||b.Target==null?a.Target==b.Target:
   a.Target.Seat==b.Target.Seat&&a.Target.UnitId==b.Target.UnitId);
 }

 internal SearchNode Prepare(MatchEngine game,AgentObservation view,DeckBelief belief,SearchBudget budget,SearchReuseStatistics stats){
  var clock=Stopwatch.StartNew();
  var token=SearchTreeReuse.MatchToken(game);
  bool same=ReferenceEquals(token,matchToken)&&seat==view.Seat;
  string newObservation=game.ReuseObservationKey(view);
  string newConfig=SearchTreeReuse.Configuration(budget);
  string newPosterior=SearchTreeReuse.BeliefKey(belief);
  var events=belief==null?game.ObserveHistory(view.Seat):belief.Information.Events;
  string newHistory=SearchTreeReuse.History(events,events.Length);
  string reason=transitionReason;
  if(!same)reason=matchToken==null?"new-session":"match-or-seat-changed";
  else if(version!=game.SearchStateVersion)reason="untracked-action";
  else if(configuration!=newConfig)reason="search-configuration-changed";
  else if(belief==null||!belief.Ready||newPosterior==null)reason="uncertified-belief";
  else if(newPosterior!=posterior)reason="belief-changed";
  else if(observation!=newObservation)reason="observation-changed";
  else if(historyCount>events.Length||history!=SearchTreeReuse.History(events,historyCount))reason="history-prefix-changed";
  else if(advanced&&events.Skip(historyCount).Any(e=>e.Kind!=HistoryKind.HandGrowth&&
   (e.Seat!=seat||(e.Kind!=HistoryKind.Played&&e.Kind!=HistoryKind.Gain))))reason="history-transition-changed";
  else if(!advanced&&newHistory!=history)reason="untracked-history";
  else if(root!=null)reason=null;

  int nodes;long bytes;SearchTreeReuse.Measure(root,out nodes,out bytes);
  if(reason!=null){
   prunedNodes+=nodes;root=null;nodes=0;bytes=0;
  }
  stats.InvalidationReason=reason;
  stats.InvalidatedNodes=prunedNodes;
  stats.Reused=root!=null&&root.Visits>0;
  stats.InheritedVisits=stats.Reused?root.Visits:0;
  stats.ReusedNodes=stats.Reused?nodes:0;
  stats.RetainedNodes=nodes;
  stats.RetainedBytes=bytes;
  stats.ValidationMilliseconds=transitionMilliseconds+clock.Elapsed.TotalMilliseconds;
  if(stats.Reused)ReuseCount++;
  LastInvalidationReason=reason;
  if(!same){LastExecutedAction=null;ReuseCount=0;}
  matchToken=token;MatchId=game.VisualSession;seat=view.Seat;version=game.SearchStateVersion;
  observation=newObservation;configuration=newConfig;posterior=newPosterior;history=newHistory;historyCount=events.Length;
  advanced=false;transitionReason=null;transitionMilliseconds=0;prunedNodes=0;lastAction=null;
  return root;
 }

 internal int ContinuationSeed(int seed,int inherited){
  if(inherited==0)return seed;
  // Keep cold-start reproducibility and separate the stream of each continuation.
  return unchecked(seed^((int)version*1000000007+inherited*104729+ReuseCount*1301081));
 }
 internal int NewBudget(int requested,int inherited){
  return mode==SearchReuseMode.TargetEffectiveVisits?Math.Max(0,requested-inherited):requested;
 }
 internal void Finish(SearchNode node,SearchAction action,SearchReuseStatistics stats){
  root=node;lastAction=action;
  int nodes;long bytes;SearchTreeReuse.Measure(root,out nodes,out bytes);
  stats.RetainedNodes=nodes;stats.RetainedBytes=bytes;
 }
 public void Reset(){
  root=null;matchToken=null;lastAction=null;LastExecutedAction=null;MatchId=null;
  observation=configuration=posterior=history=transitionReason=null;
  version=historyCount=prunedNodes=ReuseCount=0;advanced=false;transitionMilliseconds=0;LastInvalidationReason=null;
 }
}
}
