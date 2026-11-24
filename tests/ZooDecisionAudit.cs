using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Touchfish;

public static partial class RuleTests {
 sealed class AuditPosition {
  internal int Seat,Step,Turn;
  internal AgentObservation View;
  internal SearchAction Actual;
  internal DeterminizationBuilder Builder;
  internal DeckBelief Belief;
 }

 static void AuditLine(object value){Console.WriteLine(JsonData.Serializer().Serialize(value));}
 static object AuditPublic(MatchEngine game){
  return new{game.Turn,game.Active,game.FirstSeat,game.Winner,game.CardsPlayed,game.DeathsThisTurn,
   Players=game.Players.Select((p,seat)=>new{
    Seat=seat,PublicState=PublicPlayerState.From(p),p.Health,p.Armor,p.Mana,p.MaxMana,p.LockedMana,p.Overload,p.PowerUsed,p.Frozen,
    p.HandCount,DeckCount=p.Deck.Count,p.Fatigue,p.SecretCount,p.WeaponId,p.WeaponAttack,p.WeaponDurability,
    Board=p.Board.Select(u=>new{Unit=u,Name=game.Card(u.CardId).Name,Attack=game.AttackValue(u)}).ToArray()
   }).ToArray()};
 }
 static object AuditPrivate(MatchEngine game,AgentObservation view){
  return new{view.Seat,Hand=view.Hand.Select(h=>new{
   h.Id,h.CardId,h.Key,h.Type,h.Cost,h.BaseCost,Name=game.Card(h.CardId).Name,Text=game.Card(h.CardId).Text,
   game.Card(h.CardId).Attack,game.Card(h.CardId).Health
  }).ToArray(),view.OwnSecrets};
 }
 static void RunZooDecisionAudit(string[] args){
  const int matchSeed=982311,seedStream=184719,openingSeed=638291;
  var deck=MatchAgents.Find("zoo").Deck(catalog);
  var game=new MatchEngine(catalog,deck,deck,matchSeed,startingSeat:new SimulationRandom(matchSeed).Next(2));
  var zoo=new ZooWarlockAgent();
  var random=new SimulationRandom(seedStream);
  var budget=new SearchBudget{MaxIterations=100,MaxSamplingAttempts=100,MaxActions=12,MaxTurns=3,
   MaxTreeDepth=8,MaxNodes=2000,CollectDepthTelemetry=true};
  var positions=new List<AuditPosition>();
  var requested=args.Length>2?File.ReadAllLines(args[2]).Where(line=>!String.IsNullOrWhiteSpace(line)).Select(line=>line.Split('\t')).ToArray():new string[0][];
  AuditLine(new{Kind="config",MatchSeed=matchSeed,SeedStream=seedStream,OpeningSeed=openingSeed,game.FirstSeat,Budget=budget,
   Deck=deck.Cards,Names=deck.Cards.Keys.ToDictionary(id=>id,id=>catalog.Card(id).Name)});
  var opening=searcher.ChooseMulligan(game,0,deck,deck,openingSeed,32);
  var replace=new[]{opening.ReplaceIds,zoo.Mulligan(AgentBridge.Observe(game,1))};
  for(int seat=0;seat<2;seat++){
   var hand=AuditPrivate(game,AgentBridge.Observe(game,seat,false,false));
   int n=game.Players[seat].Hand.Count;
   if(!game.Mulligan(seat,replace[seat]).Success)throw new Exception("Audit mulligan rejected.");
   AuditLine(new{Kind="mulligan",Seat=seat,Before=hand,Replaced=replace[seat],After=game.Players[seat].Hand.Take(n).Select(h=>new{
    h.Id,h.CardId,Name=game.Card(h.CardId).Name,Cost=game.Card(h.CardId).Cost}).ToArray(),
    Worlds=seat==0?opening.Worlds:0,Fallback=seat==0?opening.FallbackReason:null,
    Candidates=seat==0?opening.Candidates.OrderByDescending(c=>c.MeanValue).ToArray():null});
  }
  for(int step=1;step<=900&&!game.Finished;step++){
   int seat=game.Active;
   var view=AgentBridge.Observe(game,seat,false,true);
   var actions=SearchPolicy.Actions(view);
   // Serialize before applying the action. Public board objects are engine-owned.
   var before=JsonData.Serializer().Serialize(AuditPublic(game));
   var hand=AuditPrivate(game,view);
   int historyStart=game.ObserveHistory(seat).Length;
   int lastVisual=game.VisualEvents.Count==0?0:game.VisualEvents.Last().Sequence;
   int? seed=null;
   SearchResult search=null;
   SearchAction chosen;
   var position=new AuditPosition{Seat=seat,Step=step,Turn=game.Turn,View=view};
   if(seat==0){
    seed=random.Next(int.MaxValue);
    search=searcher.Choose(game,seat,deck,deck,budget,seed.Value);
    chosen=search.BestMove;
    position.Actual=chosen;
    try{
     position.Builder=DeterminizationBuilder.Capture(game,seat);
     position.Belief=DeckBelief.Create(position.Builder.Information,deck,deck);
    }catch(InvalidOperationException){}
    positions.Add(position);
   }else{
    var move=zoo.Choose(AgentBridge.Observe(game,seat));
    chosen=move==null?null:actions.FirstOrDefault(a=>a.ActionKey==SearchPolicy.ActionKey(view,move));
   }
   if(chosen==null)throw new Exception("No legal live audit action at step "+step);
   if(!chosen.Execute(game,seat).Success)throw new Exception("Live audit action rejected at step "+step);
   var depth=search==null?null:search.DepthTelemetry;
   var stats=search==null?null:search.RootActionStatistics.OrderByDescending(s=>s.Visits).ThenByDescending(s=>s.MeanValue)
    .Take(5).Select(s=>new{Stats=s,Move=actions.First(a=>a.ActionKey==s.ActionKey).Move}).ToArray();
   AuditLine(new{Kind="action",Step=step,Seat=seat,Turn=view.Turn,SearchSeed=seed,BeforePublic=JsonData.Serializer().DeserializeObject(before),
    ActingSeatPrivate=hand,LegalCount=actions.Length,LegalKinds=actions.GroupBy(a=>a.Move==null?"decision":a.Move.Kind)
    .ToDictionary(g=>g.Key,g=>g.Count()),Legal=actions.Select(a=>new{a.ActionKey,a.Move}).ToArray(),
    Chosen=chosen.ActionKey,chosen.Move,AfterPublic=AuditPublic(game),
    PublicEvents=game.ObserveHistory(seat).Skip(historyStart).Where(e=>e.VisibleTo<0).ToArray(),
    PublicVisuals=game.VisualEvents.Where(e=>e.Sequence>lastVisual&&e.RecipientSeat<0).ToArray(),
    Search=search==null?null:new{search.Iterations,search.SamplingAttempts,search.SamplingFailures,search.SamplingFailureReasons,
     search.StopReason,search.FallbackReason,search.CompletedRollouts,search.TruncatedRollouts,search.RestrictedDecisions,
     search.RejectedSimulationActions,search.NodesCreated,search.ElapsedMilliseconds,search.TotalTreeDepth,
     depth.AtDepthLimit,depth.DepthLimitWithRollout,depth.DepthHistogram,Top=stats}
   });
  }
  if(!game.Finished)throw new Exception("Audit game did not end.");
  AuditLine(new{Kind="result",game.Winner,game.Turn});
  foreach(var pos in positions){
   var samples=pos.Belief==null||!pos.Belief.Ready?null:new BeliefSampler().Sample(pos.Belief,778007+pos.Step);
   if(samples==null||!samples.Success)continue;
   var initial=pos.Builder.Build(samples);
   var lethal=AuditAttackLethal(initial,pos.Seat,6);
   if(lethal!=null)AuditLine(new{Kind="attack-lethal-probe",pos.Step,Line=lethal,Scope="One P2 sample, attacks only"});
  }
  foreach(var request in requested){
   int step=int.Parse(request[0],System.Globalization.CultureInfo.InvariantCulture);
   var pos=positions.Single(p=>p.Step==step);
   RunAuditCounterfactual(pos,request[1]);
  }
 }

 static string[] AuditAttackLethal(FastSimulation world,int seat,int depth){
  if(world.Finished)return world.Winner==seat?new string[0]:null;
  if(world.ActiveSeat!=seat||depth==0)return null;
  var view=world.SearchView(seat,true);
  if(view.Self.Board.Sum(u=>u.AttacksRemaining*Math.Max(0,u.Attack))+Math.Max(0,view.Self.HeroAttack)<view.Enemy.Health+view.Enemy.Armor)return null;
  foreach(var action in SearchPolicy.Actions(view).Where(a=>a.Move!=null&&a.Move.Kind=="attack").OrderByDescending(a=>a.Move.Target.Hero)){
   var fork=world.Fork();
   if(!action.Execute(fork,seat).Success)continue;
   var line=AuditAttackLethal(fork,seat,depth-1);
   if(line!=null)return new[]{action.ActionKey}.Concat(line).ToArray();
  }
  return null;
 }

 static void RunAuditCounterfactual(AuditPosition pos,string alternativeKey){
  var alternative=SearchPolicy.Actions(pos.View).Single(a=>a.ActionKey==alternativeKey);
  if(pos.Belief==null||!pos.Belief.Ready){
   AuditLine(new{Kind="counterfactual-unavailable",pos.Step,Reason=pos.Belief==null?"capture":pos.Belief.Diagnostic});return;
  }
  var sampler=new BeliefSampler();
  var old=new ZooWarlockAgent();
  for(int n=0;n<32;n++){
   int seed=651031+pos.Step*100+n;
   var sample=sampler.Sample(pos.Belief,seed);
   if(!sample.Success){AuditLine(new{Kind="counterfactual-sampling-failure",pos.Step,Seed=seed,sample.Diagnostic});continue;}
   var baseline=pos.Builder.Build(sample);
   var a=AuditBranch(baseline.Fork(),pos,old,pos.Actual);
   var b=AuditBranch(baseline.Fork(),pos,old,alternative);
   AuditLine(new{Kind="counterfactual",pos.Step,Seed=seed,Actual=pos.Actual.ActionKey,Alternative=alternativeKey,
    ActualResult=a,AlternativeResult=b});
  }
 }

 static object AuditBranch(FastSimulation world,AuditPosition pos,ZooWarlockAgent old,SearchAction first){
  if(!first.Execute(world,pos.Seat).Success)throw new Exception("Counterfactual first action rejected.");
  var after=world.SearchView(pos.Seat,false);
  int? immediate=world.Finished?(int?)world.Winner:null;
  int? beforeNextOwnTurn=null;
  var trace=new List<string>();
  bool reachedOpponent=false;
  var sameTurnLethal=AuditAttackLethal(world,pos.Seat,6);
  for(int i=0;i<120&&!world.Finished;i++){
   int seat=world.ActiveSeat;
   if(seat!=pos.Seat)reachedOpponent=true;
   else if(reachedOpponent&&!beforeNextOwnTurn.HasValue)beforeNextOwnTurn=-2;
   var view=world.Observe(seat);
   var move=old.Choose(view);
   if(move==null||!world.Apply(seat,move).Success)throw new Exception("Counterfactual policy action rejected.");
   if(trace.Count<18)trace.Add(SearchPolicy.ActionKey(view,move));
  }
  if(world.Finished&&!beforeNextOwnTurn.HasValue)beforeNextOwnTurn=world.Winner;
  return new{Winner=world.Winner,ImmediateWinner=immediate,BeforeNextOwnTurn=beforeNextOwnTurn,SameTurnAttackLethal=sameTurnLethal,
   ImmediateSelfHealth=after.Self.Health,ImmediateEnemyHealth=after.Enemy.Health,
   ImmediateSelfBoard=after.Self.Board,ImmediateEnemyBoard=after.Enemy.Board,Trace=trace};
 }
}
