using System;
using System.Linq;
using System.Threading;
using Touchfish;

public static partial class RuleTests {
 static MatchEngine ReuseGame(DeckDocument own,DeckDocument enemy){
  var game=BeliefGame(own,enemy,123);
  game.Players[0].Mana=0;
  game.Players[0].Health=4;
  game.Players[1].Health=4;
  var large=game.Summon(0,"VAN_CS2_182");
  var small=game.Summon(0,"VAN_CS2_231");
  large.SummonTurn=small.SummonTurn=0;
  var blocker=game.Summon(1,"VAN_CS2_231");
  blocker.Taunt=true;
  var reply=game.Summon(1,"VAN_CS2_182");
  reply.SummonTurn=0;
  return game;
 }

 static SearchBudget ReuseBudget(){
  return new SearchBudget{MaxIterations=160,MaxTreeDepth=8,MaxActions=9,MaxTurns=2,MaxNodes=200,
   ProgressiveWidening=false};
 }

 static void CheckPublicReuseTransitions(DeckDocument own,DeckDocument enemy){
  var charge=BeliefGame(own,enemy,310);
  charge.Players[0].Health=4;
  charge.Players[1].Health=3;
  charge.Players[0].Mana=3;
  charge.GiveNaxxCard(0,"VAN_CS2_124");
  charge.Summon(1,"VAN_CS2_182").SummonTurn=0;
  var budget=ReuseBudget();
  var session=new PersistentSearchSession();
  var play=session.Choose(charge,0,own,enemy,budget,71);
  Check(play.BestMove.Move.Kind=="play"&&play.BestMove.Move.Key=="CS2_124"&&session.Execute(charge,play.BestMove).Success,
   "Search plays plain Charge minion");
  var attack=session.Choose(charge,0,own,enemy,budget,72);
  Check(attack.Reuse.InheritedVisits>0&&attack.BestMove.Move.Kind=="attack","Plain play promotes a branch with a new live entity ID");

  var ping=ReuseGame(own,enemy);
  ping.Players[0].Board.RemoveAt(1);
  ping.Players[0].Mana=2;
  var pings=new PersistentSearchSession(SearchReuseMode.TargetEffectiveVisits);
  var power=pings.Choose(ping,0,own,enemy,budget,69);
  Check(power.BestMove.Move.Kind=="power"&&pings.Execute(ping,power.BestMove).Success,"Search uses deterministic hero power to clear blocker");
  var lethal=pings.Choose(ping,0,own,enemy,budget,70);
  Check(lethal.Reuse.InheritedVisits>0&&lethal.BestMove.Move.Kind=="attack","Hero power reuses eligible attack continuation");

  var missiles=BeliefGame(own,enemy,733);
  missiles.Players[0].Mana=1;
  missiles.Players[1].Health=3;
  missiles.Summon(1,"VAN_CS2_182");
  missiles.GiveNaxxCard(0,"VAN_EX1_277");
  var randomSession=new PersistentSearchSession();
  var result=randomSession.Choose(missiles,0,own,enemy,budget,68);
  if(result.BestMove.Move.Kind=="play"){
   Check(randomSession.Execute(missiles,result.BestMove).Success,"Random spell executes with real RNG");
   var after=randomSession.Choose(missiles,0,own,enemy,budget,69);
   Check(after.Reuse.InheritedVisits==0&&after.Reuse.InvalidationReason!=null,"Uncertified random outcome invalidates old statistics");
  }else throw new Exception("Random spell fixture selected an unexpected action.");
 }

 static void SearchTreeReuseChecks(){
  var own=SearchSlowDeck("MAGE");
  var enemy=SearchSlowDeck("WARRIOR");
  foreach(var mode in new[]{SearchReuseMode.FixedNewIterations,SearchReuseMode.TargetEffectiveVisits}){
   var budget=ReuseBudget();
   var game=ReuseGame(own,enemy);
   var session=new PersistentSearchSession(mode);
   var first=session.Choose(game,0,own,enemy,budget,81);
   Check(first.Iterations==budget.MaxIterations&&first.Reuse.InheritedVisits==0&&first.Reuse.RetainedNodes<=budget.MaxNodes,"Initial search is cold and bounded");
   Check(first.BestMove.Move.Kind=="attack"&&first.BestMove.Move.Source==game.Players[0].Board.Last().Id,"Search clears taunt with expendable attacker");
   long version=session.StateVersion;
   Check(!session.Execute(game,new SearchAction("bogus",new AgentMove{Kind="end"})).Success&&session.StateVersion==version,
    "Foreign action cannot advance current session");
   Check(session.Execute(game,first.BestMove).Success&&session.StateVersion==version+1&&!session.Execute(game,first.BestMove).Success,
    "Successful action advances exactly once");
   var second=session.Choose(game,0,own,enemy,budget,82);
   Check(second.Reuse.Reused&&second.Reuse.InheritedVisits>0&&second.Reuse.InvalidatedNodes>0&&
    second.Reuse.EffectiveVisits==second.Reuse.InheritedVisits+second.Iterations&&second.Reuse.NewIterations==second.Iterations,
    "Compatible terminal statistics promoted and unrelated branches dropped: "+second.Reuse.InvalidationReason);
   Check(second.Iterations==(mode==SearchReuseMode.FixedNewIterations?budget.MaxIterations:Math.Max(0,budget.MaxIterations-second.Reuse.InheritedVisits)),
    "Mode accounts separately for inherited and new iterations");
   Check(second.BestMove.Move.Kind=="attack"&&second.BestMove.Move.Target.Hero&&session.Execute(game,second.BestMove).Success&&game.Winner==0,
    "Promoted action maps to current entity IDs and completes lethal");
  }

  CheckPublicReuseTransitions(own,enemy);
  var identical=ReuseGame(own,enemy);
  var unchanged=new PersistentSearchSession(SearchReuseMode.TargetEffectiveVisits);
  var previous=unchanged.Choose(identical,0,own,enemy,ReuseBudget(),81);
  identical.Players[1].Hand.Reverse();
  identical.Players[1].Deck.Reverse();
  var next=unchanged.Choose(identical,0,own,enemy,ReuseBudget(),81);
  Check(next.Iterations==0&&next.Reuse.InheritedVisits==previous.Iterations&&next.BestMove.ActionKey==previous.BestMove.ActionKey,
   "Same observation and posterior continue without reading actual private deck or hand");
  using(var cts=new CancellationTokenSource()){
   cts.Cancel();
   Check(unchanged.Choose(identical,0,own,enemy,ReuseBudget(),83,cts.Token).Cancelled,"Cancelled attempt returns without committing session state");
  }
  Check(unchanged.Choose(identical,0,own,enemy,ReuseBudget(),83).Reuse.InheritedVisits==160,"Cancellation preserves validated root");

  var other=ReuseGame(own,enemy);
  var foreign=unchanged.Choose(other,0,own,enemy,ReuseBudget(),81);
  Check(foreign.Reuse.InheritedVisits==0&&foreign.Reuse.InvalidationReason=="match-or-seat-changed","Distinct game instances never share statistics");
  other.Draw(0,1);
  var drawn=unchanged.Choose(other,0,own,enemy,ReuseBudget(),81);
  Check(drawn.Reuse.InheritedVisits==0&&drawn.Reuse.InvalidationReason!=null,"New private draw invalidates observations and posterior");
  var unsupported=ReuseGame(own,enemy);
  unsupported.GiveNaxxCard(1,"VAN_CS2_029");
  Check(unchanged.Choose(unsupported,0,own,enemy,ReuseBudget(),81).FallbackReason!=null,"P2 failure retains legal fallback");
  var tiny=new PersistentSearchSession(maxTreeBytes:1024);
  var tight=tiny.Choose(ReuseGame(own,enemy),0,own,enemy,ReuseBudget(),81);
  Check(tight.Reuse.RetainedBytes<=1024&&tight.Reuse.PeakTreeBytes<=1024&&tight.BestMove!=null,"Estimated persistent tree memory cap is enforced");

  var shallow=new PersistentSearchSession();
  var shortBudget=ReuseBudget();
  shortBudget.MaxActions=2;
  var truncated=ReuseGame(own,enemy);
  var a=shallow.Choose(truncated,0,own,enemy,shortBudget,81);
  if(a.BestMove.Move.Kind=="attack"){
   shallow.Execute(truncated,a.BestMove);
   var b=shallow.Choose(truncated,0,own,enemy,shortBudget,82);
   Check(b.Reuse.InheritedVisits==0&&b.Reuse.InvalidationReason=="shorter-horizon-estimates","Shorter-horizon estimates never count as effective visits");
  }else throw new Exception("Short-horizon fixture did not choose attack.");

  var secretDeck=AddBeliefCards("MAGE","VAN_EX1_287","VAN_EX1_295");
  var secretGame=FindBeliefGame(own,secretDeck,1,"VAN_EX1_287");
  secretGame.Play(1,secretGame.Players[1].Hand.First(h=>h.CardId=="VAN_EX1_287").Id);
  secretGame.EndTurn();
  secretGame.Players[0].Mana=2;
  var secretSession=new PersistentSearchSession();
  var secretBefore=secretSession.Choose(secretGame,0,own,secretDeck,TacticalBudget(20),33);
  secretGame.RestoreOwnSecrets(1,new[]{"VAN_EX1_295"});
  secretGame.Players[1].Deck.Reverse();
  secretGame.Players[1].Hand[0].CardId="VAN_CS2_231";
  var secretAfter=secretSession.Choose(secretGame,0,own,secretDeck,TacticalBudget(20),33);
  Check(secretBefore.Iterations==20&&secretAfter.Reuse.InheritedVisits==0&&SearchKey(secretBefore)==SearchKey(secretAfter),
   "Hidden secret, hand and deck permutations cannot enter persistent statistics");

  var fixture=new DynamicDecisionFixture(catalog);
  var fixedDecision=fixture.OpenForTreeReuse(own,enemy);
  var fixedSession=new PersistentSearchSession(SearchReuseMode.TargetEffectiveVisits);
  var firstFixed=fixedSession.Choose(fixedDecision,0,own,enemy,ReuseBudget(),108);
  Check(fixedSession.Execute(fixedDecision,firstFixed.BestMove).Success,"Fixed candidate selection advances");
  var secondFixed=fixedSession.Choose(fixedDecision,0,own,enemy,ReuseBudget(),109);
  Check(secondFixed.Reuse.InheritedVisits>0&&secondFixed.BestMove.DecisionId==fixedDecision.ObserveDecision(0).Id,
   "Compatible consecutive decision inherits terminal statistics with fresh decision ID: "+secondFixed.Reuse.InvalidationReason);
  var deck=matchRules.TrainingDeck("MAGE");
  var rogue=matchRules.TrainingDeck("ROGUE");
  var pending=fixture.OpenForBelief(21,deck,rogue);
  var dynamicSession=new PersistentSearchSession(SearchReuseMode.TargetEffectiveVisits);
  var firstDecision=pending.ObserveDecision(0);
  var pendingSearch=dynamicSession.Choose(pending,0,deck,rogue,TacticalBudget(35),81);
  Check(pendingSearch.BestMove.DecisionId==firstDecision.Id&&DecisionKey(pending.ObserveDecision(0))==DecisionKey(firstDecision),
   "Session does not regenerate a live decision");
  Check(dynamicSession.Execute(pending,pendingSearch.BestMove).Success&&pending.ObserveDecision(0).Id!=firstDecision.Id,"Successful decision records the actual next candidate set");
  var nextChoice=dynamicSession.Choose(pending,0,deck,rogue,TacticalBudget(35),82);
  Check(nextChoice.BestMove.DecisionId==pending.ObserveDecision(0).Id&&dynamicSession.Execute(pending,nextChoice.BestMove).Success&&
   pending.CardsPlayed==1&&pending.Players[0].Mana==8&&!pending.HasPendingDecision,"Sequential choices use current IDs and pay only once");
  if(Environment.GetEnvironmentVariable("TOUCHFISH_REUSE_TACTICAL_BENCH")=="1")ReuseTacticalBenchmark(own,enemy);
 }

 static void ReuseTacticalBenchmark(DeckDocument own,DeckDocument enemy){
  var budget=ReuseBudget();
  const int rounds=20;
  var times=new double[3];
  var iterations=new int[3];
  var visits=new int[3];
  var wins=new int[3];
  for(int trial=-1;trial<rounds;trial++){
   for(int slot=0;slot<3;slot++){
    int variant=(trial+slot+3)%3;
    var game=ReuseGame(own,enemy);
    var session=variant==0?null:new PersistentSearchSession(variant==1?SearchReuseMode.FixedNewIterations:SearchReuseMode.TargetEffectiveVisits);
    var first=session==null?SearchGame(game,own,enemy,81+trial,budget):session.Choose(game,0,own,enemy,budget,81+trial);
    if(!(session==null?first.BestMove.Execute(game,0):session.Execute(game,first.BestMove)).Success)throw new Exception("Tactical first move failed.");
    var watch=System.Diagnostics.Stopwatch.StartNew();
    var second=session==null?SearchGame(game,own,enemy,82+trial,budget):session.Choose(game,0,own,enemy,budget,82+trial);
    watch.Stop();
    if(!(session==null?second.BestMove.Execute(game,0):session.Execute(game,second.BestMove)).Success)throw new Exception("Tactical second move failed.");
    if(trial<0)continue;
    times[variant]+=watch.Elapsed.TotalMilliseconds;iterations[variant]+=second.Iterations;
    visits[variant]+=second.Reuse==null?0:second.Reuse.InheritedVisits;
    if(game.Winner==0)wins[variant]++;
   }
  }
  for(int variant=0;variant<3;variant++)Console.WriteLine(System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
   "REUSE TACTICAL mode={0} trials={1} new={2:F1} inherited={3:F1} mean-ms={4:F2} wins={5}",variant,rounds,
   (double)iterations[variant]/rounds,(double)visits[variant]/rounds,times[variant]/rounds,wins[variant]));
 }
}
