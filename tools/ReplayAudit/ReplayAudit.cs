using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Touchfish;
class ReplayAudit {
 class Entry {public string Name{get;set;} public string Code{get;set;} public string PoolId{get;set;}}
 static readonly System.Web.Script.Serialization.JavaScriptSerializer Json=JsonData.Serializer();
 static void Main(string[] a){
  var cat=CardCatalog.Load(a[0]);var frames=TestReplay.Load(a[1]);
  var header=Json.Deserialize<ReplayHeader>(File.ReadLines(a[1]).First());
  var saved=JsonData.Read<Entry[]>(Path.Combine(a[0],"decks","library.json"));
  var decks=new[]{"克总萨","火球法"}.Select(n=>saved.Single(x=>x.Name==n)).Select(e=>DeckRules.ForCurrentPlay(cat,DeckCode.Decode(cat,e.Code,e.PoolId))).ToArray();
  if(decks.Any(d=>d.PoolId!=header.Pool))throw new Exception("Pool mismatch");
  var first=frames[0];
  Func<int,GameModeDefinition,MatchEngine> make=(seed,mode)=>{
   var g=new MatchEngine(cat,decks[0],decks[1],seed,mode:mode,startingSeat:first.FirstSeat,
    firstName:first.Players[0].Public.DisplayName,secondName:first.Players[1].Public.DisplayName);
   g.ClockNowMs=()=>0;return g;
  };
  int estimate=a.Length>3?int.Parse(a[3]):unchecked(Environment.TickCount-(int)(DateTime.UtcNow-header.StartedUtc).TotalMilliseconds);
  int[] found=Enumerable.Range(-10000,20001).AsParallel().WithDegreeOfParallelism(6).Where(offset=>{
   var g=make(unchecked(estimate+offset),GameModes.Get(header.Mode));
   return Enumerable.Range(0,2).All(s=>g.Players[s].Hand.Select(h=>h.CardId).SequenceEqual(first.Players[s].Hand.Select(h=>h.CardId)));
  }).Select(offset=>unchecked(estimate+offset)).ToArray();
  Console.WriteLine("Seed center {0}; opening matches {1}",estimate,String.Join(",",found));
  var verified=new List<int>();
  foreach(int seed in found){
   var g=make(seed,GameModes.Get(header.Mode));
   try{Verify(g,frames[0],0);int log=g.Log.Count;foreach(var f in frames.Skip(1)){Apply(g,f.Action);Verify(g,f,log);log=g.Log.Count;}verified.Add(seed);}
   catch(Exception e){Console.WriteLine("Rejected "+seed+" "+e.Message);}
  }
  if(verified.Count!=1)throw new Exception("No unique fully verified replay seed in the tested range.");
  int chosen=verified[0];
  var standard=make(chosen,GameModes.Classic);
  int cursor=0;
  foreach(var f in frames){if(f.Action!=null)Apply(standard,f.Action);Verify(standard,f,cursor);cursor=standard.Log.Count;}
  JsonData.Write(Path.Combine(a[2],"verified.json"),new{seed=chosen,estimate,openingCandidates=found,frames=frames.Count,header.Mode,header.Pool,winner=standard.Winner});
  var baseline=Environment.GetEnvironmentVariable("TOUCHFISH_REPLAY_BASELINE");
  if(!String.IsNullOrEmpty(baseline))
   foreach(var row in JsonData.Read<Dictionary<string,object>[]>(baseline))
    if(row["large"]!=null)baselinePoints.Add((int)row["Index"]);
  if(Environment.GetEnvironmentVariable("TOUCHFISH_REPLAY_BRANCH_ONLY")!="1")
   Analyze(make(chosen,GameModes.Classic),make(chosen,GameModes.Get(header.Mode)),decks,frames,a[2]);
  if(Environment.GetEnvironmentVariable("TOUCHFISH_REPLAY_SKIP_BRANCHES")!="1")
   Branches(()=>make(chosen,GameModes.Classic),decks,frames,a[2]);
 }
 static void Apply(MatchEngine g,ReplayAction a){
  ActionResult r;
  switch(a.Kind){
   case "mulligan":r=g.Mulligan(a.Seat,a.SelectedHandIds);break;
   case "decision":r=g.SubmitDecision(a.Seat,a.DecisionId,a.OptionId);break;
   case "take":r=g.TestTakeCard(a.Seat,a.CardId);break;
   case "emote":r=g.Emote(a.Seat,a.Choice);break;
   default:r=AgentBridge.Execute(g,a.Seat,new AgentMove{Kind=a.Kind,Source=a.Source,Target=a.Target,Choice=a.Choice,Position=a.Position});break;
  }
  if(!r.Success)throw new Exception(a.Kind+": "+r.Message);
 }
 static void Verify(MatchEngine g,ReplayFrame f,int log){
  Action<bool,string> check=(ok,what)=>{if(!ok)throw new Exception("Frame "+f.Index+": "+what);};
  check(g.FirstSeat==f.FirstSeat&&g.Active==f.Active&&g.Turn==f.Turn&&g.Winner==f.Winner,"turn/winner");
  check(g.Log.Skip(log).SequenceEqual(f.Log),"public log");
  check(Json.Serialize(AgentBridge.Observe(g,g.Active,false,true).Moves)==Json.Serialize(f.LegalMoves),"legal moves");
  for(int s=0;s<2;s++){
   var p=g.Players[s];var q=f.Players[s];
   check(Json.Serialize(PublicPlayerState.From(p))==Json.Serialize(q.Public),"public hero "+s);
   check(p.Deck.Count==q.DeckCount&&g.SecretCards(s).SequenceEqual(q.OwnSecrets),"deck count/secrets "+s);
   check(p.Hand.Select(h=>Tuple.Create(h.Id,h.CardId,g.HandCost(s,h),h.Generated)).SequenceEqual(q.Hand.Select(h=>Tuple.Create(h.Id,h.CardId,h.Cost,h.Generated))),"hand "+s);
   check(Json.Serialize(p.Board.Select(u=>new ReplayUnit{Id=u.Id,CardId=u.CardId,Name=g.Card(u.CardId).Name,Attack=g.AttackValue(u),Health=u.Health,Taunt=u.Taunt,Shield=u.Shield,Stealth=u.Stealth,Frozen=u.Frozen,Silenced=u.Silenced,Windfury=u.Windfury,Charge=u.Charge,Poison=u.Poison,CanAttack=g.CanAttack(s,u)==null}).ToArray())==Json.Serialize(q.Board),"board "+s);
   check(g.ObserveDecision(s)==null&&q.Decision==null,"unexpected dynamic decision");
  }
 }
 static object Judge(MatchEngine g,DeckDocument own,AgentObservation v,string key,int budget,int seed){
  var actions=SearchPolicy.Actions(v);
  var r=new ExtremeSearch().Choose(g,v.Seat,own,null,new SearchBudget{MaxIterations=budget},seed);
  var sorted=actions.Join(r.RootActionStatistics??new RootActionStatistics[0],a=>a.ActionKey,t=>t.ActionKey,(a,t)=>new{a,t})
   .OrderByDescending(x=>x.t.Visits).ThenByDescending(x=>x.t.MeanValue).ThenByDescending(x=>SearchPolicy.Priority(v,x.a)).ToArray();
  int rank=Array.FindIndex(sorted,x=>x.a.ActionKey==key);
  var stat=rank<0?null:sorted[rank].t;
  return new{budget,best=r.BestMove==null?null:r.BestMove.ActionKey,humanRank=stat==null||stat.Visits==0?(int?)null:rank+1,
   visits=stat==null?0:stat.Visits,value=stat==null||stat.Visits==0?(double?)null:stat.MeanValue,
   r.Iterations,r.FallbackReason,r.StopReason,r.SamplingFailures,r.SamplingFailureReasons,r.RejectedSimulationActions,root=r.RootActionStatistics};
 }
 static void Analyze(MatchEngine g,MatchEngine strict,DeckDocument[] decks,List<ReplayFrame> frames,string output){
  var search=new ExtremeSearch();var rows=new List<object>();var openings=new OpeningMulliganResult[2];
  var openingLarge=new OpeningMulliganResult[2];
  for(int s=0;s<2;s++){
   openings[s]=search.ChooseMulligan(g,s,decks[s],null,46221+s,samples:16);
   var recorded=frames.First(f=>f.Action!=null&&f.Action.Kind=="mulligan"&&f.Action.Seat==s).Action.SelectedHandIds??new int[0];
   if(!recorded.OrderBy(i=>i).SequenceEqual(openings[s].ReplaceIds.OrderBy(i=>i)))
    openingLarge[s]=search.ChooseMulligan(g,s,decks[s],null,46221+s,samples:160);
  }
  foreach(var f in frames.Skip(1)){
   var a=f.Action;var v=AgentBridge.Observe(g,a.Seat,false,true);
   string human=null;object small=null,large=null,original=null;
   if(a.Kind=="mulligan"){
    human=String.Join(",",a.SelectedHandIds??new int[0]);
    small=openings[a.Seat];
    if(!openings[a.Seat].ReplaceIds.OrderBy(i=>i).SequenceEqual((a.SelectedHandIds??new int[0]).OrderBy(i=>i)))
     large=openingLarge[a.Seat];
    if(f.Index==2)original="Opening choices for both players were captured before either confirmation.";
   }else{
    var moves=SearchPolicy.Actions(v);
    var match=moves.FirstOrDefault(m=>m.Move!=null&&m.Move.Kind==a.Kind&&m.Move.Source==a.Source&&m.Move.Choice==a.Choice&&
      (a.Position<0||m.Move.Position==a.Position)&&Json.Serialize(m.Move.Target)==Json.Serialize(a.Target));
    if(match==null)throw new Exception("Cannot map recorded action "+f.Index);
    human=match.ActionKey;
    small=Judge(g,decks[a.Seat],v,human,16,740001+f.Index);
    string best=(string)small.GetType().GetProperty("best").GetValue(small,null);
    if(best!=human||f.Index>=31&&f.Index<=53||AuditBaselinePoint(f.Index))large=Judge(g,decks[a.Seat],v,human,160,740001+f.Index);
    original=Judge(strict,decks[a.Seat],AgentBridge.Observe(strict,a.Seat,false,true),human,16,740001+f.Index);
   }
   rows.Add(new{f.Index,Turn=v.Turn,Seat=a.Seat,Kind=a.Kind,human,small,large,original,f.Log,hand=v.Hand,
    unsupported=g.ObserveHistory(a.Seat).Where(e=>e.Kind==HistoryKind.Unmodeled).Select(e=>new{e.Sequence,e.Seat,e.Origin}),
    before=v.Self.Board.Select(u=>u.CardId),after=f.Players[a.Seat].Board.Select(u=>u.CardId)});
   Apply(g,a);Apply(strict,a);
  }
  JsonData.Write(Path.Combine(output,"decisions.json"),rows);
 }
 static HashSet<int> baselinePoints=new HashSet<int>();
 static bool AuditBaselinePoint(int index){return baselinePoints.Contains(index);}
 static void Branches(Func<MatchEngine> make,DeckDocument[] decks,List<ReplayFrame> frames,string output){
  var runs=new List<object>();
  var configured=Environment.GetEnvironmentVariable("TOUCHFISH_REPLAY_POINTS");
  var points=String.IsNullOrEmpty(configured)?new[]{31,37,38}:configured.Split(',').Select(int.Parse).ToArray();
  var jobs=from point in points from seed in new[]{91827,91828,91829} select new{point,seed};
  System.Threading.Tasks.Parallel.ForEach(jobs,new System.Threading.Tasks.ParallelOptions{MaxDegreeOfParallelism=3},job=>{
   int point=job.point,seed=job.seed;
   var game=make();
   foreach(var f in frames.Skip(1).Take(point-1))Apply(game,f.Action);
   var search=new ExtremeSearch();
   var trace=new List<object>();
   var rng=new SimulationRandom(seed);
   int fallbacks=0,failures=0,maxCopies=0;
   string error=null;
   for(int i=0;i<500&&!game.Finished;i++){
    int s=game.Active;
    var v=AgentBridge.Observe(game,s,false,true);
    SearchAction move;
    string fallback=null;
    if(s==0){
     var r=search.Choose(game,s,decks[s],null,new SearchBudget{MaxIterations=160},rng.Next(int.MaxValue));
     move=r.BestMove;fallback=r.FallbackReason;if(fallback!=null)fallbacks++;failures+=r.SamplingFailures+r.RejectedSimulationActions;
    }else move=SearchPolicy.Choose(v,RolloutStyle.Heuristic,rng.Next(int.MaxValue));
    if(move==null){error="No legal move";break;}
    int cursor=game.Log.Count,turn=game.Turn;
    var executed=move.Execute(game,s);
    if(!executed.Success){error=executed.Message;break;}
    maxCopies=Math.Max(maxCopies,game.Players[0].Board.Count(u=>game.Card(u.CardId).BaseId=="FP1_013"));
    trace.Add(new{seat=s,turn,action=move.ActionKey,fallback,log=game.Log.Skip(cursor).ToArray(),
     kel=game.Players[0].Board.Count(u=>game.Card(u.CardId).BaseId=="FP1_013")});
   }
   lock(runs){
    runs.Add(new{point,seed,game.Winner,game.Turn,maxCopies,fallbacks,failures,error,trace});
    JsonData.Write(Path.Combine(output,"branches.json"),runs);
   }
  });
 }
}


