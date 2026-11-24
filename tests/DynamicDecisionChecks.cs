using System;
using System.Linq;
using Touchfish;

public static partial class RuleTests {
 static void DynamicDecisions(){
  var fixture=new DynamicDecisionFixture(catalog);
  DecisionLifecycle(fixture);
  DecisionOrdering(fixture);
  DecisionForks(fixture);
  if(Environment.GetEnvironmentVariable("TOUCHFISH_SIMULATION_BENCHMARK")=="1")DecisionBenchmark(fixture);
 }

 static string DecisionKey(DecisionObservation choice){
  return choice.Id+" "+String.Join(",",choice.Options.Select(option=>option.Id));
 }

 static void DecisionLifecycle(DynamicDecisionFixture fixture){
  var game=fixture.NewGame(15);
  game.Players[1].Hand.Add(new HandCard{CardId="VAN_CS2_024",Id=8999});
  var wyrm=game.Summon(0,"VAN_NEW1_012");
  var pyro=game.Summon(0,"VAN_NEW1_020");
  Check(game.Play(0,9000).Success&&game.HasPendingDecision,"Dynamic spell starts");
  var first=game.ObserveDecision(0);
  Check(first.Options.Length==3&&first.Options.Select(o=>o.Id).Distinct().Count()==3&&first.Options.All(o=>o.Cost.HasValue),"Distinct legal candidates with card metadata");
  Check(game.Players[0].Mana==8&&game.Players[0].Hand.Count==0&&game.CardsPlayed==1&&wyrm.BuffAttack==1&&pyro.Health==2,"Pay, remove and SpellCast occur once; SpellResolved waits");

  var before=SimulationState(game);
  var clock=game.TurnClockRemainingMs;
  int logs=game.Log.Count,visuals=game.VisualEvents.Count;
  Check(!game.SubmitDecision(1,first.Id,first.Options[0].Id).Success&&!game.SubmitDecision(0,first.Id+1,first.Options[0].Id).Success&&
   !game.SubmitDecision(0,first.Id,"missing").Success,"Wrong seat, stale ID and unknown option rejected");
  Check(!game.EndTurn().Success&&!game.Play(0,9000).Success&&!game.HeroPower(0,new MatchTarget(1)).Success&&
   !game.Attack(0,wyrm.Id,new MatchTarget(1)).Success&&!game.Mulligan(0,null).Success&&!game.TestTakeCard(0,"VAN_CS2_182").Success,
   "Waiting blocks ordinary actions");
  Check(before==SimulationState(game)&&logs==game.Log.Count&&visuals==game.VisualEvents.Count&&clock==game.TurnClockRemainingMs&&DecisionKey(first)==DecisionKey(game.ObserveDecision(0)),
   "Rejected submissions are atomic");

  var enemy=AgentBridge.Observe(game,1);
  var publicView=JsonData.Serializer().Serialize(LanProtocol.View(game,1,"fixture",0));
  string enemyView=JsonData.Serializer().Serialize(enemy);
  string visualsJson=JsonData.Serializer().Serialize(game.VisualEvents);
  Check(first.Options.All(o=>!enemyView.Contains(o.Id.Substring(5))&&!visualsJson.Contains(o.Title)&&!publicView.Contains(o.Title)),
   "Public observations and visuals omit actual candidate identities");
  Check(enemy.Decision==null&&!enemy.CanAct&&enemy.Moves.Length==0&&first.Options.All(o=>!publicView.Contains(o.Id)&&!game.Log.Any(l=>l.Contains(o.Title))),
   "Opponent, public log and LAN view cannot read private candidates");
  var owner=AgentBridge.Observe(game,0);
  Check(owner.Decision!=null&&!owner.CanAct&&owner.Moves.Length==0,"Owner receives decisions instead of ordinary moves");
  string key=DecisionKey(first);
  string ownerView=JsonData.Serializer().Serialize(owner);
  game.Players[1].Deck.Reverse();
  game.Players[1].Hand[0].CardId="VAN_CS2_028";
  Check(key==DecisionKey(game.ObserveDecision(0))&&ownerView==JsonData.Serializer().Serialize(AgentBridge.Observe(game,0)),
   "Opponent hidden changes do not affect the owning seat's observation");
  owner.Decision.Options[0]=null;
  Check(game.ObserveDecision(0).Options[0]!=null,"Observation array is detached");

  Check(game.SubmitDecision(0,first.Id,first.Options[0].Id).Success,"First choice accepted");
  var second=game.ObserveDecision(0);
  Check(second.Id!=first.Id&&second.Options.Length==3&&pyro.Health==2&&game.Players[0].Hand.Single().Generated,"Second request waits before tail");
  Check(!game.SubmitDecision(0,first.Id,first.Options[0].Id).Success,"Earlier decision cannot satisfy next request");
  Check(game.SubmitDecision(0,second.Id,second.Options[1].Id).Success&&!game.HasPendingDecision,"Second choice completes spell");
  Check(game.Players[0].Hand.Select(h=>"pick:"+h.CardId).SequenceEqual(new[]{first.Options[0].Id,second.Options[1].Id})&&
   game.Players[0].Mana==8&&game.CardsPlayed==1&&wyrm.BuffAttack==1&&pyro.Health==1&&
   game.Graveyard.Count(e=>e.CardId==DynamicDecisionFixture.Double)==1,"Only selected cards added; triggers and payment once");
  Check(!game.SubmitDecision(0,second.Id,second.Options[1].Id).Success,"Completed request cannot replay");

  var full=fixture.NewGame(16);
  for(int i=0;i<9;i++)full.Players[0].Hand.Add(new HandCard{Id=9100+i,CardId="VAN_CS2_231"});
  full.Play(0,9000);
  for(int step=0;step<2;step++){
   var decision=full.ObserveDecision(0);
   Check(full.SubmitDecision(0,decision.Id,decision.Options[0].Id).Success,"Full-hand choice resolves");
  }
  Check(full.Players[0].Hand.Count==10&&full.Players[0].Hand.Count(h=>h.Generated)==1&&
   full.Graveyard.Count(e=>e.Reason==GraveyardReason.Burned)==1,"Second generated card follows existing burn rules");

  var lethal=fixture.NewGame(17,DynamicDecisionFixture.Lethal);
  lethal.Play(0,9000);
  var finish=lethal.ObserveDecision(0);
  Check(lethal.SubmitDecision(0,finish.Id,"finish").Success&&lethal.Winner==0&&!lethal.HasPendingDecision&&lethal.Players[0].Hand.Count==0,
   "Lethal ends resolution without generating another choice");
 }

 static void DecisionOrdering(DynamicDecisionFixture fixture){
  var countered=fixture.NewGame(23);
  countered.RestoreOwnSecrets(1,new[]{"VAN_EX1_287"});
  countered.Players[1].SecretCount=1;
  Check(countered.Play(0,9000).Success&&!countered.HasPendingDecision&&countered.Players[1].SecretCount==0&&
   countered.Players[0].Mana==8&&countered.Players[0].Hand.Count==0,"Counterspell precedes dynamic effect");

  var game=fixture.NewGame(24,DynamicDecisionFixture.Single);
  game.Summon(0,"VAN_CS2_237");
  game.RestoreOwnSecrets(1,new[]{"VAN_EX1_294"});
  game.Players[1].SecretCount=1;
  Check(game.Play(0,9000).Success&&game.Players[0].Board.Count==2&&game.Players[0].Hand.Count==0&&game.Players[1].SecretCount==1,
   "Battlecry pauses after placement before queued summons and Mirror Entity");
  var copy=game.CopyForSimulation();
  var pending=game.ObserveDecision(0);
  Check(game.SubmitDecision(0,pending.Id,pending.Options[1].Id).Success&&copy.SubmitDecision(0,pending.Id,pending.Options[1].Id).Success&&
   SimulationState(game)==SimulationState(copy),"Pending summon notifications resolve with fork-owned references");
  Check(game.Players[0].Hand.Count==2&&game.Players[0].Hand[0].Generated&&!game.Players[0].Hand[1].Generated&&
   game.Players[1].Board.Single().CardId==DynamicDecisionFixture.Single,"Gain precedes Buzzard draw and Mirror Entity");

  var deaths=fixture.NewGame(25);
  deaths.Summon(0,"VAN_NEW1_020");
  deaths.Summon(1,"VAN_EX1_029");
  deaths.Play(0,9000);
  for(int step=0;step<2;step++){
   var choice=deaths.ObserveDecision(0);
   deaths.SubmitDecision(0,choice.Id,choice.Options[0].Id);
  }
  Check(deaths.Players[1].Board.Count==0&&deaths.Players[0].Health==28&&deaths.DeathsThisTurn==1,"Post-spell Pyromancer and deathrattle finish in order");

  var timed=fixture.NewGame(26);
  timed.ClockNowMs=()=>0;
  timed.Summon(0,"VAN_EX1_560");
  timed.Play(0,9000);
  var fork=SimulationSnapshot.Capture(timed).Fork();
  Check(!fork.AdvanceTime(15000)&&fork.HasPendingDecision&&fork.ActiveSeat==0,"Timeout cannot interrupt a suspended effect");
  for(int step=0;step<2;step++){
   var choice=fork.ObserveDecision(0);
   Check(fork.SubmitDecision(0,choice.Id,choice.Options[0].Id).Success,"Owner can resolve after timer expires");
  }
  Check(fork.AdvanceTime(0)&&fork.ActiveSeat==1,"Expired turn advances once resolution finishes");

  var unsupported=fixture.NewGame(27);
  unsupported.Rules.RegisterExtension("test:callback","NONE",context=>GC.KeepAlive(timed));
  unsupported.Play(0,9000);
  SimulationCaptureRejected(unsupported,"Runtime callbacks cannot enter paused simulation");
 }

 static void DecisionForks(DynamicDecisionFixture fixture){
  for(int seed=0;seed<1000;seed++){
   var source=fixture.NewGame(seed);
   Check(source.Play(0,9000).Success,"Seeded decision opens");
   var initial=source.ObserveDecision(0);
   var original=DecisionKey(initial);
   var snapshot=SimulationSnapshot.Capture(source);
   var replay=snapshot.Fork(811);
   var futureKeys=new string[3];
   for(int option=0;option<3;option++){
    var fork=snapshot.Fork(811);
    Check(DecisionKey(fork.ObserveDecision(0))==original&&fork.ObserveDecision(1)==null,"Fork preserves generated choices privately");
    var selected=initial.Options[option];
    Check(fork.SubmitDecision(0,initial.Id,selected.Id).Success&&"pick:"+fork.Observe(0).Hand.Single().CardId==selected.Id,"Fork applies only its selected result");
    var next=fork.ObserveDecision(0);
    futureKeys[option]=DecisionKey(next);
    Check(fork.SubmitDecision(0,next.Id,next.Options[0].Id).Success&&!fork.HasPendingDecision&&fork.Observe(0).Hand.Length==2,
     "Each candidate fork can complete the entire effect");
   }
   Check(replay.SubmitDecision(0,initial.Id,initial.Options[0].Id).Success&&futureKeys.All(k=>k==DecisionKey(replay.ObserveDecision(0))),
    "Second request consumes the same RNG sequence independent of first option");
   var continuation=snapshot.Fork();
   Check(continuation.SubmitDecision(0,initial.Id,initial.Options[0].Id).Success&&
    source.Players[0].Hand.Count==0&&source.Players[0].Mana==8&&DecisionKey(source.ObserveDecision(0))==original&&
    DecisionKey(snapshot.Fork().ObserveDecision(0))==original,"Source and snapshot stay unchanged across branches");
   Check(source.SubmitDecision(0,initial.Id,initial.Options[0].Id).Success&&
    DecisionKey(source.ObserveDecision(0))==DecisionKey(continuation.ObserveDecision(0)),"Unseeded fork preserves live RNG continuation");
  }
 }

 static void DecisionBenchmark(DynamicDecisionFixture fixture){
  var game=fixture.NewGame(435,DynamicDecisionFixture.Single);
  game.Summon(0,"VAN_CS2_237");
  game.Play(0,9000);
  var pending=game.ObserveDecision(0);
  var snapshot=SimulationSnapshot.Capture(game);
  var control=fixture.NewGame(435,DynamicDecisionFixture.Single);
  control.Summon(0,"VAN_CS2_237");
  control.Summon(0,DynamicDecisionFixture.Single,summonEvent:false);
  control.Players[0].Hand.Clear();
  control.Players[0].Mana=8;
  var settled=SimulationSnapshot.Capture(control);
  SimulationMeasure("settled fork (two minions)",30000,()=>GC.KeepAlive(settled.Fork()));
  SimulationMeasure("pending fork",30000,()=>GC.KeepAlive(snapshot.Fork()));
  SimulationMeasure("generate three candidates",50000,()=>GC.KeepAlive(fixture.Discover.Generate(game)));
  const int count=6000;
  FastSimulation[] forks=null;
  int index=0;
  SimulationMeasure("submit and resolve",count,()=>{
   if(!forks[index++].SubmitDecision(0,pending.Id,pending.Options[0].Id).Success)
    throw new InvalidOperationException("Benchmark decision failed");
  },()=>{
   forks=Enumerable.Range(0,count).Select(i=>snapshot.Fork()).ToArray();
   index=0;
  });
 }
}
