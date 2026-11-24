using System;
using System.Linq;
using System.Threading;
using Touchfish;

public static partial class RuleTests {
 static readonly ExtremeSearch searcher=new ExtremeSearch();
 static void ExtremeSearchChecks(){
  SearchOpeningMulligan();
  SearchTactics();
  SearchPrivacyAndBounds();
  SearchDynamicAndRandom();
  if(Environment.GetEnvironmentVariable("TOUCHFISH_SEARCH_BENCHMARK")=="1")ExtremeSearchBenchmarks();
 }

 static void SearchOpeningMulligan(){
  var own=SearchSlowDeck("MAGE");
  var enemy=SearchSlowDeck("WARRIOR");
  for(int seat=0;seat<2;seat++){
   var game=new MatchEngine(catalog,own,enemy,528,startingSeat:0);
   var first=seat==0?own:enemy;
   var second=seat==0?enemy:own;
   string before=SimulationState(game);
   var rng=game.CopyForSimulation();
   var result=searcher.ChooseMulligan(game,seat,first,second,211,samples:4);
   int count=seat==0?8:16;
   Check(result.Worlds==4&&result.SamplingFailures==0&&result.Candidates.Length==count&&result.FallbackReason==null&&
    result.Candidates.Select(c=>String.Join(",",c.ReplaceIds)).Distinct().Count()==count&&
    result.ReplaceIds.All(id=>game.Players[seat].Hand.Any(h=>h.Id==id)),
    "Opening search enumerates each legal subset with real engine forks");
   Check(before==SimulationState(game)&&game.RandomIndex(100000)==rng.RandomIndex(100000),"Opening search does not mutate live cards or RNG");
   game.Players[seat].Deck.Reverse();
   game.Players[1-seat].Deck.Reverse();
   game.Players[1-seat].Hand[0].CardId="VAN_CS2_231";
   var changed=searcher.ChooseMulligan(game,seat,first,second,211,samples:4);
   Check(JsonData.Serializer().Serialize(result)==JsonData.Serializer().Serialize(changed),"Opening search ignores true hidden cards and orders");
   Check(game.Mulligan(1-seat,null).Success&&searcher.ChooseMulligan(game,seat,first,second,211,samples:4).FallbackReason!=null,
    "Already-confirmed opponent uses explicit generic fallback");
  }
  var zoo=MatchAgents.Find("zoo").Deck(catalog);
  bool replaced=false;
  for(int seed=0;seed<6;seed++){
   var game=new MatchEngine(catalog,zoo,zoo,seed);
   var result=searcher.ChooseMulligan(game,0,zoo,null,313,samples:12);
   Check(result.Worlds>0&&result.FallbackReason==null,"Opening search accepts unknown opponent construction prior");
   replaced|=result.ReplaceIds.Length>0;
  }
  Check(replaced,"Curve search actually replaces cards in playable opening fixtures");
 }

 static DeckDocument SearchSlowDeck(string profession){
  var pool=catalog.Pool("classic-2014");
  var deck=new DeckDocument{PoolId=pool.Id,ClassId=profession};
  foreach(var card in catalog.Deckable(pool).Where(c=>c.Cost>=6&&matchRules.Supports(c)).OrderBy(c=>c.Id,StringComparer.Ordinal)){
   while(DeckRules.CanAdd(catalog,deck,card)==null){
    int count;deck.Cards.TryGetValue(card.Id,out count);deck.Cards[card.Id]=count+1;
   }
   if(deck.Count==30)break;
  }
  DeckRules.Validate(catalog,deck,true);
  return deck;
 }
 static SearchBudget TacticalBudget(int iterations=120){
  return new SearchBudget{MaxIterations=iterations,MaxSamplingAttempts=iterations,MaxTreeDepth=8,MaxActions=9,MaxTurns=2,
   ProgressiveWidening=false};
 }
 static SearchResult SearchGame(MatchEngine game,DeckDocument own,DeckDocument enemy,int seed=81,SearchBudget budget=null){
  return searcher.Choose(game,game.Active,own,enemy,budget??TacticalBudget(),seed);
 }
 static void SearchTactics(){
  var own=SearchSlowDeck("MAGE");
  var enemy=SearchSlowDeck("WARRIOR");
  var game=BeliefGame(own,enemy,56);
  game.Players[0].Mana=0;
  var single=SearchGame(game,own,enemy);
  Check(single.BestMove.Move.Kind=="end"&&single.StopReason=="single-action"&&single.SamplingAttempts==0,"Single legal root move returns directly");

  var one=game.Summon(0,"VAN_CS2_182");
  one.SummonTurn=0;
  game.Players[1].Health=4;
  var lethal=SearchGame(game,own,enemy);
  Check(lethal.BestMove.Move.Kind=="attack"&&lethal.BestMove.Move.Target.Hero&&lethal.RootActionStatistics.Any(stat=>stat.TerminalWinCount>0),
   "Immediate deterministic lethal discovered");

  var two=game.Summon(0,"VAN_CS2_231");
  two.SummonTurn=0;
  var taunt=game.Summon(1,"VAN_CS2_231");
  taunt.Taunt=true;
  var ordered=SearchGame(game,own,enemy,76,TacticalBudget(220));
  Check(ordered.BestMove.Move.Kind=="attack"&&ordered.BestMove.Move.Source==two.Id&&ordered.BestMove.Move.Target.UnitId==taunt.Id&&
   ordered.RootActionStatistics.Single(s=>s.ActionKey==ordered.BestMove.ActionKey).TerminalWinCount>0,
   "Clear taunt with small attacker before lethal; same-turn credit");

  var combo=BeliefGame(own,enemy,57);
  combo.Players[0].Mana=3;
  combo.Players[1].Health=6;
  combo.GiveNaxxCard(0,"GAME_COIN");
  combo.GiveNaxxCard(0,"VAN_CS2_029");
  var mana=SearchGame(combo,own,enemy,178,TacticalBudget(260));
  Check(mana.BestMove.Move.Kind=="play"&&mana.BestMove.Move.Key=="GAME_COIN"&&
   mana.RootActionStatistics.Single(s=>s.ActionKey==mana.BestMove.ActionKey).TerminalWinCount>0,
   "Search finds Coin before Fireball rather than a weaker order");

  var placement=BeliefGame(own,enemy,151);
  placement.Players[0].Mana=2;
  placement.Players[1].Health=4;
  var left=placement.Summon(0,"VAN_CS2_231");
  var middle=placement.Summon(0,"VAN_CS2_231");
  var right=placement.Summon(0,"VAN_CS2_231");
  left.SummonTurn=right.SummonTurn=0;
  middle.Frozen=true;
  placement.GiveNaxxCard(0,"VAN_EX1_162");
  // Ready bodies occupy the two rightmost positions so only the middle-right slot buffs both.
  placement.Players[0].Board.Remove(middle);
  placement.Players[0].Board.Insert(0,middle);
  var position=SearchGame(placement,own,enemy,118,TacticalBudget(350));
  Check(position.BestMove.Move.Kind=="play"&&position.BestMove.Move.Position==2&&
   position.RootActionStatistics.Single(stat=>stat.ActionKey==position.BestMove.ActionKey).TerminalWinCount>0,
   "Distinct placement keys preserve the only lethal adjacency");

  Check(ExtremeSearch.TerminalValue(-1,0)==0.5&&ExtremeSearch.TerminalValue(-1,1)==0.5&&
   ExtremeSearch.TerminalValue(1,0)==0&&ExtremeSearch.TerminalValue(1,1)==1,"Terminal utility is root-relative and draw is half");
  var reply=BeliefGame(own,enemy,56);
  reply.Players[0].Mana=2;
  reply.Players[0].Health=4;
  var threat=reply.Summon(1,"VAN_CS2_182");
  threat.SummonTurn=0;
  var response=SearchGame(reply,own,enemy);
  Check(response.RootActionStatistics.Single(stat=>stat.ActionKey.StartsWith("end|")).TerminalLossCount>0,
   "Opponent lethal is propagated as a root loss");
  Check(new[]{lethal,ordered,mana,position,response}.All(result=>result.RejectedSimulationActions==0&&
   result.RootActionStatistics.All(stat=>stat.AvailabilityCount>=stat.Visits&&
    stat.Visits==stat.TerminalWinCount+stat.TerminalLossCount+stat.TerminalDrawCount+stat.EstimatedCount)),
   "Only legal moves execute and availability is separate from visits");
 }

 static string SearchKey(SearchResult result){
  return JsonData.Serializer().Serialize(new{
   Action=result.BestMove==null?null:result.BestMove.ActionKey,result.RootActionStatistics,result.Iterations,
   result.SimulationActions,result.NodesCreated,result.SamplingAttempts,result.RestrictedDecisions,result.FallbackReason
  });
 }

 static void SearchPrivacyAndBounds(){
  var own=matchRules.TrainingDeck("MAGE");
  var enemy=AddBeliefCards("MAGE","VAN_EX1_287","VAN_EX1_295");
  var game=FindBeliefGame(own,enemy,1,"VAN_EX1_287");
  game.Play(1,game.Players[1].Hand.First(h=>h.CardId=="VAN_EX1_287").Id);
  game.EndTurn();
  game.Players[0].Mana=5;
  var budget=TacticalBudget(35);
  var rng=game.CopyForSimulation();
  string original=SimulationState(game);
  var before=SearchGame(game,own,enemy,33,budget);
  var repeated=SearchGame(game,own,enemy,33,budget);
  Check(before.Iterations==35&&SearchKey(before)==SearchKey(repeated)&&SimulationState(game)==original&&game.RandomIndex(100000)==rng.RandomIndex(100000),
   "Fixed-budget deterministic search leaves live state and RNG untouched");
  game.Players[1].Deck.Reverse();
  game.Players[1].Hand[0].CardId="VAN_CS2_231";
  game.RestoreOwnSecrets(1,new[]{"VAN_EX1_295"});
  var after=SearchGame(game,own,enemy,33,budget);
  Check(SearchKey(before)==SearchKey(after),"Enemy hand, secret and deck permutations do not change search");
  Check(SearchKey(before)!=SearchKey(SearchGame(game,own,enemy,34,budget)),"Changing search seed changes sampled behavior");
  var unknown=SearchGame(game,own,null,33,TacticalBudget(12));
  Check(unknown.SuccessfulSamples>0&&unknown.RejectedSimulationActions==0,"Search runs with P2 unknown-deck prior");
  var oneNode=TacticalBudget(20);oneNode.MaxNodes=1;
  var limited=SearchGame(game,own,enemy,33,oneNode);
  Check(limited.NodesCreated==1&&limited.Iterations==20&&limited.CompletedRollouts+limited.TruncatedRollouts==20,"Node cap preserves bounded rollouts");
  var timed=TacticalBudget(10000);timed.TimeLimitMilliseconds=15;
  var timeout=SearchGame(game,own,enemy,33,timed);
  Check(timeout.StopReason=="time-limit"&&timeout.ElapsedMilliseconds<1500&&timeout.BestMove!=null,"Time budget returns a legal action");
  using(var cts=new CancellationTokenSource()){
   cts.Cancel();
   var stopped=searcher.Choose(game,0,own,enemy,budget,33,cts.Token);
   Check(stopped.Cancelled&&stopped.Iterations==0&&stopped.BestMove!=null,"Cancellation returns legal fallback");
  }
  using(var cts=new CancellationTokenSource()){
   var cancelling=new ExtremeSearch(new InterruptingSearchSampler(cts,3));
   var stopped=cancelling.Choose(game,0,own,enemy,budget,33,cts.Token);
   Check(stopped.Cancelled&&stopped.Iterations==2&&stopped.BestMove!=null&&stopped.FallbackReason==null,
    "Cancellation during sampling retains completed root statistics");
  }
  var failing=new ExtremeSearch(new FailingSearchSampler());
  var failureBudget=TacticalBudget(10);failureBudget.MaxConsecutiveFailures=3;
  var failed=failing.Choose(game,0,own,enemy,failureBudget,13);
  Check(failed.SamplingAttempts==3&&failed.SamplingFailures==3&&failed.SuccessfulSamples==0&&failed.Iterations==0&&
   failed.SamplingFailureReasons["fixture"]==3&&failed.BestMove!=null&&failed.FallbackReason!=null,"Sampling failures stop at configured limit");
  var actor=1;
  var control=game.CopyForSimulation();
  control.Active=actor;
  control.Players[actor].Mana=5;
  var view=AgentBridge.Observe(control,actor,false,true);
  string action=SearchPolicy.Choose(view,RolloutStyle.Heuristic,100).ActionKey;
  control.Players[0].Hand.Reverse();
  control.Players[0].Deck.Reverse();
  var other=AgentBridge.Observe(control,actor,false,true);
  Check(SearchPolicy.InformationKey(view)==SearchPolicy.InformationKey(other)&&
   SearchPolicy.Choose(other,RolloutStyle.Heuristic,100).ActionKey==action&&other.RemainingDeck.Length==0,
   "Opponent policy sees its legal hand and public boards, without root-private information");
  game.GiveNaxxCard(1,"VAN_CS2_029");
  var fallback=SearchGame(game,own,enemy);
  Check(fallback.Iterations==0&&fallback.FallbackReason!=null&&fallback.BestMove!=null&&
   SearchPolicy.Actions(AgentBridge.Observe(game,0)).Any(a=>a.ActionKey==fallback.BestMove.ActionKey),"Unsupported belief yields explicit legal fallback");
 }

 sealed class InterruptingSearchSampler : IBeliefSampler {
  readonly CancellationTokenSource cancel;
  readonly BeliefSampler inner=new BeliefSampler();
  int remaining;
  internal InterruptingSearchSampler(CancellationTokenSource cancel,int remaining){this.cancel=cancel;this.remaining=remaining;}
  public SampleResult Sample(DeckBelief belief,int seed){
   var sample=inner.Sample(belief,seed);
   if(--remaining==0)cancel.Cancel();
   return sample;
  }
 }
 sealed class FailingSearchSampler : IBeliefSampler {
  public SampleResult Sample(DeckBelief belief,int seed){return new SampleResult(belief.Information,"fixture");}
 }

 static void SearchDynamicAndRandom(){
  var fixture=new DynamicDecisionFixture(catalog);
  var own=matchRules.TrainingDeck("MAGE");
  var enemy=matchRules.TrainingDeck("ROGUE");
  var pending=fixture.OpenForBelief(21,own,enemy);
  var choice=pending.ObserveDecision(0);
  var result=SearchGame(pending,own,enemy,81,TacticalBudget(40));
  Check(result.RootActionStatistics.Length==3&&result.RootActionStatistics.All(s=>s.Visits>0)&&result.BestMove.DecisionId==choice.Id&&
   DecisionKey(pending.ObserveDecision(0))==DecisionKey(choice)&&pending.CardsPlayed==1&&pending.Players[0].Mana==8,
   "Decision options searched independently without touching paid live action");
  Check(result.BestMove.Execute(pending,0).Success&&pending.ObserveDecision(0).Id!=choice.Id,"Live decision resumes second request");
  var second=SearchGame(pending,own,enemy,82,TacticalBudget(20));
  Check(second.BestMove.Execute(pending,0).Success&&!pending.HasPendingDecision&&pending.CardsPlayed==1&&pending.Players[0].Mana==8,
   "Second search completes sequential decisions without replaying the card");

  var slowOwn=SearchSlowDeck("MAGE");
  var slowEnemy=SearchSlowDeck("WARRIOR");
  var privateEnemy=fixture.OpponentCanOpenDecision(60,slowOwn,slowEnemy);
  var restricted=SearchGame(privateEnemy,slowOwn,slowEnemy,419,TacticalBudget(30));
  Check(restricted.RestrictedDecisions>0&&restricted.TruncatedRollouts>=restricted.RestrictedDecisions&&
   !privateEnemy.HasPendingDecision,"Opponent-private simulated choices are counted and truncated");

  var randomGame=BeliefGame(own,enemy,63);
  randomGame.Players[0].Mana=1;
  randomGame.Players[1].Health=1;
  randomGame.Summon(1,"VAN_CS2_182");
  randomGame.GiveNaxxCard(0,"VAN_EX1_277");
  var randomBudget=TacticalBudget(600);
  randomBudget.MaxActions=1;
  var distribution=SearchGame(randomGame,own,enemy,988,randomBudget);
  var missiles=distribution.RootActionStatistics.First(s=>s.ActionKey.Contains("VAN_EX1_277")&&s.Visits>40);
  double fraction=(double)missiles.TerminalWinCount/missiles.Visits;
  Check(fraction>0.78&&fraction<0.95&&missiles.EstimatedCount>0,"Engine rolls all missile targets; lethal frequency near 7/8");
 }
}

