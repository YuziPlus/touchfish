using System;
using System.Diagnostics;
using System.Linq;
using Touchfish;

public static partial class RuleTests {
 static void Simulation(){
  foreach(int seed in new[]{0,1,-1,42,104729,int.MinValue,int.MaxValue}){
   var expected=new Random(seed);
   var actual=new SimulationRandom(seed);
   for(int i=0;i<1024;i++){
    int limit=i%7==0?0:i%5==0?int.MaxValue:i;
    Check(actual.Next(limit)==expected.Next(limit),"Seeded desktop RNG compatibility");
   }
  }

  var first=matchRules.TrainingDeck("MAGE");
  var second=matchRules.TrainingDeck("ROGUE");
  for(int i=0;i<1000;i++){
   var game=new MatchEngine(catalog,first,second,i,startingSeat:i%2);
   if(i%4!=0){
    game.Mulligan(0,new[]{game.Players[0].Hand[0].Id});
    game.Mulligan(1,new[]{game.Players[1].Hand[0].Id});
    for(int n=0;n<i%7;n++)game.EndTurn();
    var unit=game.Summon(i%2,"VAN_CS2_182");
    game.Buff(new MatchTarget(unit.Owner,unit.Id),i%4+1,2);
   }
   var before=SimulationState(game);
   var snapshot=SimulationSnapshot.Capture(game);
   var a=snapshot.Fork();
   var b=snapshot.Fork();
   Check(JsonData.Serializer().Serialize(a.Observe(0))==JsonData.Serializer().Serialize(b.Observe(0)),"Forks start with equal observations "+i);
   var view=a.Observe(a.ActiveSeat);
   if(view.MulliganComplete){
    var move=view.Moves.Last(m=>m.Kind=="end");
    Check(a.Apply(a.ActiveSeat,move).Success&&a.Turn!=b.Turn,"Branch action isolated "+i);
   }else{
    Check(a.Mulligan(0,new[]{a.Observe(0).Hand[0].Id}).Success&&a.Observe(0).Hand[0].Id!=b.Observe(0).Hand[0].Id,"Branch mulligan isolated "+i);
   }
   Check(SimulationState(game)==before,"Source unchanged "+i);
  }

  SimulationOpening();
  SimulationParity();
  if(Environment.GetEnvironmentVariable("TOUCHFISH_SIMULATION_BENCHMARK")=="1")SimulationBenchmark();
 }

 static string SimulationState(MatchEngine game){
  return JsonData.Serializer().Serialize(new{
   game.FirstSeat,game.Active,game.Turn,game.Winner,game.CardsPlayed,game.DeathsThisTurn,game.CardPoolId,
   game.Players,game.Graveyard,Secrets=new[]{game.SecretCards(0),game.SecretCards(1)},game.PrivateDeckPreview
  });
 }

 static void SimulationOpening(){
  var live=new MatchEngine(catalog,matchRules.TrainingDeck("MAGE"),matchRules.TrainingDeck("SHAMAN"),829,startingSeat:1);
  var copy=live.CopyForSimulation();
  for(int seat=0;seat<2;seat++){
   var selection=live.Players[seat].Hand.Select(h=>h.Id).Take(2).ToArray();
   Check(live.Mulligan(seat,selection).Success&&copy.Mulligan(seat,selection).Success&&SimulationState(live)==SimulationState(copy),"Mulligan, shuffling, opening draw and Coin parity");
  }
 }

 static void SimulationParity(){
  var live=MatchFixture("MAGE","ROGUE");
  var board=live.Summon(0,"VAN_CS2_182");
  live.Buff(new MatchTarget(0,board.Id),2,1);
  live.Summon(1,"VAN_EX1_029");
  live.Summon(1,"VAN_CS2_231");
  live.Players[0].Deck.Clear();
  live.Players[0].Deck.Add("VAN_CS2_182");
  live.PrivateDeckPreview[0]=new[]{"VAN_CS2_182"};
  var scheduled=new HandCard{Id=999,CardId="VAN_EX1_137",Generated=true};
  live.ScheduleComboReturn(0,scheduled);
  int secret=Hand(live,"EX1_287");
  int missiles=Hand(live,"EX1_277");
  int pyro=Hand(live,"EX1_279");
  Check(live.Play(0,secret).Success,"Fixture arms Counterspell");
  live.Players[0].Mana=10;
  live.ShuffleBlackrockCopy(0,"VAN_EX1_029");
  var snapshot=SimulationSnapshot.Capture(live);
  var copy=live.CopyForSimulation();
  Check(SimulationState(copy)==SimulationState(live),"Exact state includes private decks, enchantments and graveyard");
  copy.Players[0].Deck.Reverse();
  copy.Players[0].Hand[0].CostAdjustment=-9;
  copy.Players[0].Board[0].Enchantments.Items[0].Attack=50;
  copy.PrivateDeckPreview[0][0]="different";
  copy.DestroySecrets(0);
  Check(live.Players[0].Hand[0].CostAdjustment==0&&board.BuffAttack==2&&live.PrivateDeckPreview[0][0]=="VAN_CS2_182"&&live.SecretCards(0).Length==1,"Nested mutable state has no reference aliases");
  Check(!ReferenceEquals(copy.Card("VAN_CS2_182"),live.Card("VAN_CS2_182"))&&!ReferenceEquals(copy.Rules.Triggers[0],live.Rules.Triggers[0]),"Catalog records and trigger definitions detached from live match");
  var sim=live.CopyForSimulation();
  var same=live.CopyForSimulation();
  var moves=new[]{
   new AgentMove{Kind="play",Source=missiles},new AgentMove{Kind="end"},
   new AgentMove{Kind="play",Source=Hand(live,"CS2_029",1),Target=new MatchTarget(0)},
   new AgentMove{Kind="end"},new AgentMove{Kind="play",Source=pyro,Target=new MatchTarget(1)}
  };
  sim.Players[1].Hand.Add(live.Players[1].Hand.Last().CopyForSimulation());
  same.Players[1].Hand.Add(live.Players[1].Hand.Last().CopyForSimulation());
  live.Players[1].MaxMana=sim.Players[1].MaxMana=same.Players[1].MaxMana=10;
  foreach(var move in moves){
   int seat=live.Active;
   Check(AgentBridge.Execute(live,seat,move).Success&&AgentBridge.Execute(sim,seat,move).Success&&AgentBridge.Execute(same,seat,move).Success,"Formal and simulated move: "+move.Kind+" "+move.Source);
   Check(SimulationState(live)==SimulationState(sim)&&SimulationState(sim)==SimulationState(same),"State parity after random spell, secrets, turns or Pyroblast");
  }
  Check(sim.Players[0].Hand.Any(h=>h.CardId=="VAN_EX1_137"&&h.Generated),"Delayed Combo return preserved");
  Check(sim.VisualEvents.Count==0&&!sim.AnimationPlaying&&live.VisualEvents.Count>0,"Headless skips presentation");

  var a=snapshot.Fork(85);
  var b=snapshot.Fork(85);
  var randomMove=new AgentMove{Kind="play",Source=missiles};
  Check(a.Apply(0,randomMove).Success&&b.Apply(0,randomMove).Success&&JsonData.Serializer().Serialize(a.Observe(0))==JsonData.Serializer().Serialize(b.Observe(0)),"Explicit seed reproducible");
  var detached=a.Observe(0);
  detached.Self.Health=-1;
  Check(a.Observe(0).Self.Health>0,"Observation is detached");

  var burn=MatchFixture("MAGE");
  for(int i=0;i<10;i++)Hand(burn,"CS2_182");
  burn.Players[0].Deck.Clear();
  burn.Players[0].Deck.Add("VAN_CS2_029");
  Check(burn.Play(0,burn.Players[0].Hand[0].Id).Success,"Fixture makes board space");
  Hand(burn,"CS2_182");
  var burnFork=burn.CopyForSimulation();
  burn.Draw(0,2);
  burnFork.Draw(0,2);
  Check(SimulationState(burn)==SimulationState(burnFork)&&burnFork.Players[0].Fatigue==1&&burnFork.Graveyard.Any(e=>e.Reason==GraveyardReason.Burned)&&burnFork.VisualEvents.Count==0,"Burn and fatigue parity without visuals");

  var theft=MatchFixture("MAGE","MAGE");
  theft.Active=1;
  Check(theft.Play(1,Hand(theft,"EX1_287",1)).Success,"Fixture arms opposing secret");
  var theftFork=theft.CopyForSimulation();
  theft.StealGvgSecret(0);
  theftFork.StealGvgSecret(0);
  Check(SimulationState(theft)==SimulationState(theftFork)&&theftFork.SecretCards(0).Length==1&&theftFork.VisualEvents.Count==0,"Secret transfer parity without private visuals");

  var timer=MatchFixture("MAGE");
  long time=1000;
  timer.ClockNowMs=()=>time;
  timer.Summon(0,"VAN_EX1_560");
  timer.SyncTurnClock();
  time+=1250;
  var timed=SimulationSnapshot.Capture(timer).Fork();
  Check(timed.TurnClockRemainingMs==13750&&!timed.AdvanceTime(13749)&&timed.AdvanceTime(1)&&timed.ActiveSeat==1&&timed.TurnClockRemainingMs==15000&&timer.Active==0,"Copied logical timer ends turn exactly once");
  Check(!timed.Fork().AdvanceTime(14999),"Timer copied into independent fork");

  var death=MatchFixture("MAGE");
  var leper=death.Summon(1,"VAN_EX1_029");
  int lethal=Hand(death,"CS2_029");
  var deathFork=death.CopyForSimulation();
  var kill=new AgentMove{Kind="play",Source=lethal,Target=new MatchTarget(1,leper.Id)};
  Check(AgentBridge.Execute(death,0,kill).Success&&AgentBridge.Execute(deathFork,0,kill).Success&&deathFork.Players[0].Health==28&&deathFork.Graveyard.Any(e=>e.Reason==GraveyardReason.MinionDied)&&SimulationState(death)==SimulationState(deathFork),"Deathrattle damage and graveyard parity");

  var hero=MatchFixture("WARLOCK");
  hero.ReplaceHero(0,HeroReplacementRules.JaraxxusId);
  var heroFork=hero.CopyForSimulation();
  Check(hero.HeroPower(0).Success&&heroFork.HeroPower(0).Success&&SimulationState(hero)==SimulationState(heroFork),"Replacement hero and special power parity");

  var animation=MatchFixture("MAGE");
  animation.SetAnimationEffects(EffectLevel.High);
  int animated=Hand(animation,"EX1_279");
  var immediate=SimulationSnapshot.Capture(animation).Fork();
  var blast=new AgentMove{Kind="play",Source=animated,Target=new MatchTarget(1)};
  Check(immediate.Apply(0,blast).Success&&immediate.Observe(0).Enemy.Health==20&&animation.Players[1].Health==30,"Simulated Pyroblast impacts immediately");
  Check(AgentBridge.Execute(animation,0,blast).Success&&animation.SpellImpactPending,"Formal Pyroblast defers damage");
  SimulationCaptureRejected(animation,"Pending animation resolution cannot be captured");
  var remote=LanProtocol.ReadView(catalog,LanProtocol.View(timer,1,"simulation",1));
  SimulationCaptureRejected(remote,"Masked LAN replicas cannot be simulated as authoritative state");
 }

 static void SimulationCaptureRejected(MatchEngine game,string label){
  bool rejected=false;
  try{SimulationSnapshot.Capture(game);}catch(InvalidOperationException){rejected=true;}
  Check(rejected,label);
 }

 static void SimulationBenchmark(){
  var source=MatchFixture("MAGE","ROGUE");
  for(int seat=0;seat<2;seat++){
   for(int i=0;i<5;i++){
    var unit=source.Summon(seat,"VAN_CS2_182");
    source.Buff(new MatchTarget(seat,unit.Id),1,1);
    Hand(source,"CS2_029",seat);
   }
  }
  var snapshot=SimulationSnapshot.Capture(source);
  int hand=source.Players[0].Hand[0].Id;
  var fireball=new AgentMove{Kind="play",Source=hand,Target=new MatchTarget(1)};
  var end=new AgentMove{Kind="end"};
  SimulationMeasure("capture",300,()=>GC.KeepAlive(SimulationSnapshot.Capture(source)));
  SimulationMeasure("fork",30000,()=>GC.KeepAlive(snapshot.Fork()));
  SimulationMeasure("fork+3 actions",12000,()=>{
   var simulation=snapshot.Fork();
   if(!simulation.Apply(0,fireball).Success||!simulation.Apply(0,end).Success||!simulation.Apply(1,end).Success)
    throw new InvalidOperationException("Invalid benchmark actions");
  });
  var opening=SimulationSnapshot.Capture(new MatchEngine(catalog,matchRules.TrainingDeck("MAGE"),matchRules.TrainingDeck("ROGUE"),512));
  int games=0,actions=0;
  SimulationMeasure("complete games",160,()=>{
   var game=opening.Fork(2000+games++);
   if(!game.Mulligan(0,null).Success||!game.Mulligan(1,null).Success)
    throw new InvalidOperationException("Invalid benchmark mulligan");
   int steps=0;
   while(!game.Finished&&steps<500){
    int seat=game.ActiveSeat;
    var moves=game.Observe(seat).Moves;
    var move=moves.FirstOrDefault(m=>m.Kind=="attack"&&m.Target.Hero)
      ??moves.FirstOrDefault(m=>m.Kind=="play"&&m.Position>=0)
      ??moves.FirstOrDefault(m=>m.Kind=="attack")
      ??moves.FirstOrDefault(m=>m.Kind=="power"&&(m.Target==null||m.Target.Seat!=seat))
      ??end;
    if(!game.Apply(seat,move).Success)throw new InvalidOperationException("Invalid benchmark game action");
    steps++;
   }
   if(!game.Finished)throw new InvalidOperationException("Benchmark game did not finish");
   actions+=steps;
  });
  Console.WriteLine("BENCH complete games: {0:F1} mean actions per game, excluding mulligan",(double)actions/games);
 }

 static void SimulationMeasure(string name,int count,Action run,Action prepare=null){
  AppDomain.MonitoringIsEnabled=true;
  if(prepare!=null)prepare();
  for(int i=0;i<Math.Min(count,1000);i++)run();
  if(prepare!=null)prepare();
  GC.Collect();
  GC.WaitForPendingFinalizers();
  GC.Collect();
  int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
  long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
  var clock=Stopwatch.StartNew();
  for(int i=0;i<count;i++)run();
  clock.Stop();
  long bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated;
  Console.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture,
   "BENCH {0}: n={1} ms={2:F2} /s={3:F0} us/op={4:F2} bytes/op={5:F0} GC={6}/{7}/{8}",
   name,count,clock.Elapsed.TotalMilliseconds,count/clock.Elapsed.TotalSeconds,clock.Elapsed.TotalMilliseconds*1000/count,
   (double)bytes/count,GC.CollectionCount(0)-g0,GC.CollectionCount(1)-g1,GC.CollectionCount(2)-g2));
 }
}

