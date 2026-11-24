using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Touchfish;

public static partial class RuleTests {
 static readonly BeliefSampler worldSampler=new BeliefSampler();

 static MatchEngine BeliefGame(DeckDocument own,DeckDocument enemy,int seed,CardCatalog source=null,int firstSeat=0){
  var game=new MatchEngine(source??catalog,own,enemy,seed,startingSeat:firstSeat);
  game.Mulligan(0,null);
  game.Mulligan(1,null);
  return game;
 }
 static string WorldKey(SampleResult sample){
  return JsonData.Serializer().Serialize(new{sample.Diagnostic,Deck0=sample.Deck(0),Deck1=sample.Deck(1),Secrets=sample.SecretCards(1),Hand=sample.OpponentHand(),Initial=sample.InitialDeck(1)});
 }

 static void BeliefSampling(){
  BeliefFixedDecks();
  BeliefPrivacy();
  BeliefSecrets();
  BeliefMultipleSecretReveals();
  BeliefSources();
  BeliefDelayedAndShuffle();
  BeliefHandCostShifts();
  BeliefDistribution();
  BeliefDecision();
  if(Environment.GetEnvironmentVariable("TOUCHFISH_BELIEF_BENCHMARK")=="1")BeliefBenchmark();
 }

 static void BeliefFixedDecks(){
  foreach(var definition in MatchAgents.All){
   var enemy=definition.Deck(catalog);
   var own=matchRules.TrainingDeck("MAGE");
   var game=BeliefGame(own,enemy,619);
   string original=SimulationState(game);
   var builder=DeterminizationBuilder.Capture(game,0);
   var belief=DeckBelief.Create(builder.Information,own,enemy);
   Check(belief.Ready&&belief.EnemyDeckKnown,"Known fixed prior "+definition.Id);
   for(int seed=0;seed<1000;seed++){
    var sample=worldSampler.Sample(belief,seed);
    string trace="";
    try{
     if(!sample.Success)throw new Exception(sample.Diagnostic);
     var expected=enemy.Cards.OrderBy(p=>p.Key,StringComparer.Ordinal).SelectMany(p=>Enumerable.Repeat(p.Key,p.Value));
     var observed=sample.Deck(1).Concat(sample.OpponentHand().Select(c=>c.CardId)).Where(id=>id!="GAME_COIN").OrderBy(id=>id,StringComparer.Ordinal);
     Check(observed.SequenceEqual(expected)&&sample.Deck(1).Length==builder.Information.DeckCount(1)&&
      sample.OpponentHand().Length==builder.Information.HandCount(1),"Conservation and counts "+definition.Id);
     var world=builder.Build(sample);
     var fork=world.Fork();
     int seat=fork.ActiveSeat;
     trace="end("+seat+")";
     Check(fork.Apply(seat,new AgentMove{Kind="end"}).Success&&world.Turn==1&&fork.Observe(1).Hand.Length==builder.Information.HandCount(1)+1,
      "World copies and draws normally "+definition.Id);
     if(seed<6){
      for(int step=0;step<100&&!fork.Finished;step++){
       int active=fork.ActiveSeat;
       trace+=" end("+active+")";
       if(!fork.Apply(active,new AgentMove{Kind="end"}).Success)throw new Exception("End turn failed.");
      }
      Check(fork.Finished,"Hypothetical game reaches fatigue terminal");
     }
    }catch(Exception ex){
     Directory.CreateDirectory("work");
     File.WriteAllText("work/belief-failure.txt","prior="+definition.Id+" seed="+seed+" turn="+game.Turn+
      "\ntrace="+trace+"\nsample="+(sample.Success?WorldKey(sample):sample.Diagnostic));
     throw new Exception("Belief fixture failed: "+definition.Id+" seed "+seed,ex);
    }
   }
   Check(SimulationState(game)==original,"World generation never modifies live match");
  }
 }

 static DeckDocument AddBeliefCards(string profession,params string[] ids){
  var deck=matchRules.TrainingDeck(profession);
  foreach(string id in ids){
   if(deck.Cards.ContainsKey(id))continue;
   string remove=deck.Cards.Keys.First(key=>!ids.Contains(key));
   if(--deck.Cards[remove]==0)deck.Cards.Remove(remove);
   deck.Cards.Add(id,1);
  }
  DeckRules.Validate(catalog,deck,true);
  return deck;
 }

 static MatchEngine FindBeliefGame(DeckDocument own,DeckDocument enemy,int seat,string cardId){
  for(int seed=0;seed<300;seed++){
   var game=BeliefGame(own,enemy,seed,firstSeat:seat);
   if(game.Players[seat].Hand.Any(h=>h.CardId==cardId)){game.Players[seat].Mana=10;return game;}
  }
  throw new InvalidOperationException("Fixture card not found.");
 }

 static void BeliefPrivacy(){
  var own=AddBeliefCards("MAGE");
  var enemy=AddBeliefCards("MAGE","VAN_EX1_287","VAN_EX1_295");
  var game=FindBeliefGame(own,enemy,1,"VAN_EX1_287");
  Check(game.Play(1,game.Players[1].Hand.First(h=>h.CardId=="VAN_EX1_287").Id).Success,"Fixture secret played");
  var one=DeterminizationBuilder.Capture(game,0);
  game.Players[1].Deck.Reverse();
  var hidden=game.Players[1].Hand.First();
  hidden.CardId="VAN_EX1_168";hidden.Id=90000;hidden.AttackBonus=21;hidden.CostAdjustment=-9;hidden.Generated=true;
  game.RestoreOwnSecrets(1,new[]{"VAN_EX1_295"});
  var two=DeterminizationBuilder.Capture(game,0);
  Check(JsonData.Serializer().Serialize(one.Information.History)==JsonData.Serializer().Serialize(two.Information.History)&&
   one.Information.History.Where(e=>e.Kind==HistoryKind.Played||e.Kind==HistoryKind.ArmSecret).All(e=>e.CardId==null),"History masks actual secret and hidden mutations");
  var belief1=DeckBelief.Create(one.Information,own,enemy);
  var belief2=DeckBelief.Create(two.Information,own,enemy);
  for(int seed=0;seed<100;seed++){
   var a=worldSampler.Sample(belief1,seed);
   var b=worldSampler.Sample(belief2,seed);
   Check(a.Success&&b.Success&&WorldKey(a)==WorldKey(b),"Same legal information yields identical hypothetical configuration");
   Check(JsonData.Serializer().Serialize(one.Build(a).Observe(1))==JsonData.Serializer().Serialize(two.Build(b).Observe(1)),
    "Hypothetical world uses canonical IDs and sampled private attributes");
  }
  var unknown=DeckBelief.Create(two.Information,own);
  var unknownSample=worldSampler.Sample(unknown,29);
  Check(unknown.Ready&&!unknown.EnemyDeckKnown&&unknownSample.Success,"Unknown prior uses legal alternatives");
  DeckRules.Validate(catalog,unknownSample.InitialDeck(1),true);
  var otherUnknown=worldSampler.Sample(DeckBelief.Create(one.Information,own),29);
  Check(otherUnknown.Success&&WorldKey(unknownSample)==WorldKey(otherUnknown),"Unknown prior and its sampled initial deck ignore live hidden information");
 }

 static void BeliefSecrets(){
  string[,] cards={{"MAGE","VAN_EX1_295","VAN_EX1_287","EX1_287"},{"HUNTER","VAN_EX1_554","VAN_EX1_610","EX1_610"},
   {"PALADIN","VAN_EX1_136","VAN_EX1_132","EX1_132"}};
  for(int i=0;i<3;i++){
   var own=AddBeliefCards("MAGE","VAN_CS2_029");
   var enemy=AddBeliefCards(cards[i,0],cards[i,1],cards[i,2]);
   var game=FindBeliefGame(own,enemy,1,cards[i,1]);
   game.Play(1,game.Players[1].Hand.First(h=>h.CardId==cards[i,1]).Id);
   game.EndTurn();
   if(i==0){game.GiveNaxxCard(0,"VAN_CS2_029");game.Players[0].Mana=10;game.Play(0,game.Players[0].Hand.Last().Id,new MatchTarget(1));}
   if(i==1){var unit=game.Summon(0,"VAN_CS2_182");unit.SummonTurn=0;game.Attack(0,unit.Id,new MatchTarget(1));}
   if(i==2)game.Damage(new MatchTarget(1),1);
   var builder=DeterminizationBuilder.Capture(game,0);
   var belief=DeckBelief.Create(builder.Information,own,enemy);
   Check(builder.Information.History.Any(e=>e.Kind==HistoryKind.NoSecretTrigger&&e.CardId==cards[i,3]),"Safe negative secret evidence "+cards[i,0]);
   for(int seed=0;seed<30;seed++){
    var sample=worldSampler.Sample(belief,seed);
    Check(sample.Success&&sample.SecretCards(1).Single()!=cards[i,2],"Secret alternatives respect public evidence "+cards[i,0]+" "+sample.Diagnostic);
   }
  }
 }

 static void BeliefMultipleSecretReveals(){
  const string barrier="VAN_EX1_289", counter="VAN_EX1_287", block="VAN_EX1_295";
  var own=AddBeliefCards("MAGE","VAN_CS2_029");
  var enemy=AddBeliefCards("MAGE",barrier,counter,block);
  var game=BeliefGame(own,enemy,619,firstSeat:1);
  // Move known fixture cards into hand using the normal draw history.
  foreach(string id in new[]{barrier,counter,block}){
   var player=game.Players[1];
   if(!player.Hand.Any(h=>h.CardId==id)){
    Check(player.Deck.Remove(id),"Fixture secret in deck");
    player.Deck.Insert(0,id);
    game.Draw(1,1);
   }
   player.Mana=10;
   Check(game.Play(1,player.Hand.First(h=>h.CardId==id).Id).Success,"Arm fixture secret anonymously");
  }
  game.EndTurn();
  var attacker=game.Summon(0,"VAN_CS2_182");
  attacker.SummonTurn=0;
  Check(game.Attack(0,attacker.Id,new MatchTarget(1)).Success,"Attack reveals Ice Barrier with three secrets armed");
  var after=DeterminizationBuilder.Capture(game,0);
  game.RestoreOwnSecrets(1,new[]{block,counter});
  game.Players[1].Deck.Reverse();
  var changed=DeterminizationBuilder.Capture(game,0);
  var a=DeckBelief.Create(after.Information,own,enemy);
  var b=DeckBelief.Create(changed.Information,own,enemy);
  for(int seed=0;seed<30;seed++){
   var sample=worldSampler.Sample(a,seed);
   var alternate=worldSampler.Sample(b,seed);
   Check(sample.Success&&alternate.Success&&WorldKey(sample)==WorldKey(alternate),"Remaining secret sampling ignores live identities and order");
   Check(sample.SecretCards(1).OrderBy(id=>id).SequenceEqual(new[]{counter,block})&&
    !sample.Deck(1).Concat(sample.OpponentHand().Select(h=>h.CardId)).Contains(barrier),"Remaining secrets are distinct and spent Barrier is accounted for");
  }
  game.GiveNaxxCard(0,"VAN_CS2_029");
  game.Players[0].Mana=10;
  Check(game.Play(0,game.Players[0].Hand.Last().Id,new MatchTarget(1)).Success,"Next action reveals Counterspell");
  var history=DeterminizationBuilder.Capture(game,0).Information;
  var final=worldSampler.Sample(DeckBelief.Create(history,own,enemy),78);
  Check(final.Success&&final.SecretCards(1).SequenceEqual(new[]{block})&&
   history.History.Where(e=>e.Kind==HistoryKind.RevealSecret).Select(e=>e.CardId).SequenceEqual(new[]{barrier,counter}),
   "Consecutive public reveals preserve the one remaining secret");
  game.DestroySecrets(1);
  Check(!DeckBelief.Create(DeterminizationBuilder.Capture(game,0).Information,own,enemy).Ready,"Unmodeled secret destruction still falls back");
 }

 static void BeliefSources(){
  var own=AddBeliefCards("MAGE","VAN_CS2_029");
  var enemy=AddBeliefCards("MAGE","VAN_EX1_287","VAN_EX1_029");
  var game=FindBeliefGame(own,enemy,1,"VAN_EX1_287");
  game.Play(1,game.Players[1].Hand.First(h=>h.CardId=="VAN_EX1_287").Id);
  game.EndTurn();
  game.GiveNaxxCard(0,"VAN_CS2_029");
  game.Players[0].Mana=10;
  int fire=game.Players[0].Hand.Last().Id;
  var builder=DeterminizationBuilder.Capture(game,0);
  var sample=worldSampler.Sample(DeckBelief.Create(builder.Information,own,enemy),144);
  Check(sample.Success,"Owner-known generated card and hidden secret accounted");
  var sim=builder.Build(sample);
  Check(sim.Apply(0,new AgentMove{Kind="play",Source=fire,Target=new MatchTarget(1)}).Success&&sim.Observe(0).Enemy.Health==30&&sim.Observe(0).Enemy.SecretCount==0,"Hypothetical Counterspell uses actual rules");
  game.Play(0,fire,new MatchTarget(1));
  var after=DeterminizationBuilder.Capture(game,0);
  Check(worldSampler.Sample(DeckBelief.Create(after.Information,own,enemy),121).Success,"Revealed secret and consumed generated spell reconcile");

  var leper=game.Summon(1,"VAN_EX1_029");
  game.GiveNaxxCard(0,"VAN_CS2_029");
  game.Players[0].Mana=10;
  var withMinion=DeterminizationBuilder.Capture(game,0);
  var world=withMinion.Build(worldSampler.Sample(DeckBelief.Create(withMinion.Information,own,enemy),92));
  Check(world.Apply(0,new AgentMove{Kind="play",Source=game.Players[0].Hand.Last().Id,Target=new MatchTarget(1,leper.Id)}).Success&&
   world.Observe(0).Self.Health==28,"Sample executes normal deathrattle");
  var attacker=game.Summon(0,"VAN_CS2_182");
  attacker.SummonTurn=0;
  var combatBuilder=DeterminizationBuilder.Capture(game,0);
  var combat=combatBuilder.Build(worldSampler.Sample(DeckBelief.Create(combatBuilder.Information,own,enemy),68));
  Check(combat.Apply(0,new AgentMove{Kind="attack",Source=attacker.Id,Target=new MatchTarget(1,leper.Id)}).Success&&
   combat.Apply(0,new AgentMove{Kind="power",Target=new MatchTarget(1)}).Success&&combat.Observe(0).Self.Health==28,
   "Hypothetical attack and hero skill execute with real rules");
  game.GiveNaxxCard(1,"VAN_CS2_029");
  var unsupported=DeterminizationBuilder.Capture(game,0);
  Check(!DeckBelief.Create(unsupported.Information,own,enemy).Ready,"Unknown generated opponent card returns diagnostic");
 }

 static void BeliefHandCostShifts(){
  const string kel="FP1_013", rebirth="BRM9554_FP1_025";
  var own=DeckRules.ForCurrentPlay(catalog,matchRules.TrainingDeck("SHAMAN"));
  var enemy=DeckRules.ForCurrentPlay(catalog,FreezeMageDeck.Create(catalog));
  foreach(string id in new[]{kel,rebirth}){
   if(own.Cards.ContainsKey(id))continue;
   string remove=own.Cards.Keys.First(k=>k!=kel&&k!=rebirth);
   if(--own.Cards[remove]==0)own.Cards.Remove(remove);
   own.Cards.Add(id,1);
  }
  var game=BeliefGame(own,enemy,308);
  foreach(string id in new[]{kel,rebirth}){
   var p=game.Players[0];
   if(p.Hand.Any(h=>h.CardId==id))continue;
   Check(p.Deck.Remove(id),"Fixture card available in shuffled deck");
   p.Deck.Insert(0,id);game.Draw(0,1);
  }
  game.GiveNaxxCard(0,"GAME_COIN",publicIdentity:true);
  game.GiveNaxxCard(0,"VAN_CS2_029",adjustment:2,publicIdentity:true);
  game.Summon(0,"BRM_028");
  Check(game.EndTurn().Success&&game.EndTurn().Success,"Native Emperor trigger resolved");
  game.Players[0].Mana=10;
  var hand=AgentBridge.Observe(game,0,false,true).Hand;
  Check(hand.Single(h=>h.CardId==kel).Cost==7&&hand.Single(h=>h.CardId==rebirth).Cost==1,"Kel'Thuzad and Reincarnate cost 7 and 1");
  Check(hand.Single(h=>h.CardId=="GAME_COIN").Cost==0&&hand.Single(h=>h.CardId=="VAN_CS2_029").Cost==5,"Zero floor and generated surcharge use the same order");
  game.Transform(new MatchTarget(0,game.Players[0].Board[0].Id),"VAN_CS2_tk1");
  var builder=DeterminizationBuilder.Capture(game,0);
  var sample=worldSampler.Sample(DeckBelief.Create(builder.Information,own,enemy),19);
  Check(sample.Success,"Discounted hand and public transform reconcile "+sample.Diagnostic);
  var live=AgentBridge.Observe(game,0,false,true);
  var simulated=builder.Build(sample).SearchView(0,true);
  Check(SearchPolicy.InformationKey(live)==SearchPolicy.InformationKey(simulated)&&
   SearchPolicy.Actions(live).Select(a=>a.ActionKey).SequenceEqual(SearchPolicy.Actions(simulated).Select(a=>a.ActionKey)),
   "Reconstruction preserves exact acting costs, transformed board and legal actions");
  var other=DeterminizationBuilder.Capture(game,1);
  var sampleOther=worldSampler.Sample(DeckBelief.Create(other.Information,enemy,own),71);
  game.Players[0].Hand.Reverse();game.Players[0].Deck.Reverse();
  game.Players[0].Hand[0].CostAdjustment+=7;
  var changed=DeterminizationBuilder.Capture(game,1);
  var sampleChanged=worldSampler.Sample(DeckBelief.Create(changed.Information,enemy,own),71);
  Check(sampleOther.Success&&sampleChanged.Success&&sampleOther.OpponentHand().Select(h=>Tuple.Create(h.CardId,h.CostAdjustment))
   .SequenceEqual(sampleChanged.OpponentHand().Select(h=>Tuple.Create(h.CardId,h.CostAdjustment))),"Sampled discounts ignore true enemy order");
 }

 static void BeliefDelayedAndShuffle(){
  var own=AddBeliefCards("ROGUE","VAN_EX1_137");
  var enemy=AddBeliefCards("MAGE");
  var game=FindBeliefGame(own,enemy,0,"VAN_EX1_137");
  game.GiveNaxxCard(0,"GAME_COIN");
  game.Play(0,game.Players[0].Hand.Last().Id);
  Check(game.Play(0,game.Players[0].Hand.First(h=>h.CardId=="VAN_EX1_137").Id).Success,"Combo delay fixture");
  game.ShuffleGvgCard(1,"GVG_056t",true);
  var builder=DeterminizationBuilder.Capture(game,0,game.Players[0].Deck.Take(2).ToArray());
  var belief=DeckBelief.Create(builder.Information,own,enemy);
  var sample=worldSampler.Sample(belief,155);
  Check(sample.Success&&sample.Deck(1).Contains("GVG_056t")&&!sample.OpponentHand().Any(h=>h.CardId=="GVG_056t")&&
   sample.Deck(0).Take(2).SequenceEqual(game.Players[0].Deck.Take(2)),"Known insert stays in deck and authorized top order is preserved");
  var world=builder.Build(sample);
  var end=new AgentMove{Kind="end"};
  Check(world.Apply(0,end).Success&&world.Apply(1,end).Success&&world.Observe(0).Hand.Any(h=>h.CardId=="VAN_EX1_137"),
   "Hypothetical engine restores delayed return from public history");
 }

 static void BeliefDistribution(){
  var own=AddBeliefCards("MAGE");
  var enemy=AddBeliefCards("MAGE","VAN_EX1_295","VAN_EX1_287");
  var game=BeliefGame(own,enemy,20,firstSeat:1);
  var builder=DeterminizationBuilder.Capture(game,0);
  var belief=DeckBelief.Create(builder.Information,own,enemy);
  string target=enemy.Cards.First(p=>p.Value==2).Key;
  int hits=0;
  for(int seed=0;seed<10000;seed++){
   var sample=worldSampler.Sample(belief,seed);
   if(sample.OpponentHand().Any(h=>h.CardId==target))hits++;
  }
  // Enemy starts with four initial draws. P(at least one copy) = 1-C(28,4)/C(30,4).
  double expected=1.0-(26.0*25)/(30*29);
  Check(Math.Abs(hits/10000.0-expected)<0.02,"Hypergeometric distribution of blind opening hand");

  var other=AddBeliefCards("MAGE","VAN_EX1_289","VAN_EX1_287");
  var weighted=new LegalDeckPrior(new[]{enemy,other},new[]{1,3},0);
  var unknown=DeckBelief.Create(builder.Information,own,prior:weighted);
  int ice=0;
  for(int seed=0;seed<2000;seed++){
   var sample=worldSampler.Sample(unknown,seed);
   if(sample.Deck(1).Concat(sample.OpponentHand().Select(h=>h.CardId)).Contains("VAN_EX1_295"))ice++;
  }
  Check(Math.Abs(ice/2000.0-.25)<.04,"Configured archetype prior weights");

  var secretDeck=AddBeliefCards("MAGE","VAN_EX1_295","VAN_EX1_287");
  var extras=secretDeck.Cards.Keys.Where(id=>id!="VAN_EX1_295"&&id!="VAN_EX1_287"&&MatchEngine.Has(catalog.Card(id),"SECRET")).ToArray();
  foreach(var id in extras)secretDeck.Cards.Remove(id);
  secretDeck.Cards["VAN_EX1_295"]=2;
  var pool=catalog.Pool(secretDeck.PoolId);
  foreach(var card in catalog.Deckable(pool)){
   if(secretDeck.Count==30)break;
   if(MatchEngine.Has(card,"SECRET")||DeckRules.CanAdd(catalog,secretDeck,card)!=null)continue;
   secretDeck.Cards[card.Id]=secretDeck.Cards.ContainsKey(card.Id)?secretDeck.Cards[card.Id]+1:1;
  }
  if(secretDeck.Count>30){string remove=secretDeck.Cards.Keys.First(id=>!MatchEngine.Has(catalog.Card(id),"SECRET"));secretDeck.Cards[remove]--;if(secretDeck.Cards[remove]==0)secretDeck.Cards.Remove(remove);}
  var secretGame=FindBeliefGame(own,secretDeck,1,"VAN_EX1_295");
  secretGame.Play(1,secretGame.Players[1].Hand.First(h=>h.CardId=="VAN_EX1_295").Id);
  var secretInfo=DeterminizationBuilder.Capture(secretGame,0).Information;
  var secretBelief=DeckBelief.Create(secretInfo,own,secretDeck);
  int blocks=0;
  for(int seed=0;seed<3000;seed++){
   var sample=worldSampler.Sample(secretBelief,seed);
   if(sample.SecretCards(1).Single()=="VAN_EX1_295")blocks++;
  }
  Check(Math.Abs(blocks/3000.0-2.0/3)<.035,"Unrevealed secret multiplicity weight");
 }

 static void BeliefDecision(){
  var fixture=new DynamicDecisionFixture(catalog);
  var own=matchRules.TrainingDeck("MAGE");
  var enemy=matchRules.TrainingDeck("ROGUE");
  var game=fixture.OpenForBelief(37,own,enemy);
  var choice=game.ObserveDecision(0);
  var builder=DeterminizationBuilder.Capture(game,0);
  var belief=DeckBelief.Create(builder.Information,own,enemy);
  var sample=worldSampler.Sample(belief,29);
  Check(sample.Success,"Sample while dynamic resolution is paused");
  var world=builder.Build(sample);
  foreach(var option in choice.Options){
   var fork=world.Fork();
   Check(DecisionKey(fork.ObserveDecision(0))==DecisionKey(choice)&&fork.ObserveDecision(1)==null&&
    fork.SubmitDecision(0,choice.Id,option.Id).Success&&fork.ObserveDecision(0)!=null&&world.ObserveDecision(0).Id==choice.Id,
    "Owner-known candidates survive independent sampling forks");
  }
  bool rejected=false;
  try{DeterminizationBuilder.Capture(game,1);}catch(InvalidOperationException){rejected=true;}
  Check(rejected,"Other seat cannot sample private pending resolution");
 }

 static void BeliefBenchmark(){
  var own=matchRules.TrainingDeck("MAGE");
  var enemy=matchRules.TrainingDeck("ROGUE");
  var game=BeliefGame(own,enemy,313);
  var shortHistory=DeterminizationBuilder.Capture(game,0);
  for(int n=0;n<12;n++)game.EndTurn();
  var longHistory=DeterminizationBuilder.Capture(game,0);
  var belief=DeckBelief.Create(shortHistory.Information,own,enemy);
  var longBelief=DeckBelief.Create(longHistory.Information,own,enemy);
  var unknown=DeckBelief.Create(shortHistory.Information,own);
  int seed=0;
  SimulationMeasure("belief known build",3000,()=>GC.KeepAlive(DeckBelief.Create(shortHistory.Information,own,enemy)));
  SimulationMeasure("belief unknown build",3000,()=>GC.KeepAlive(DeckBelief.Create(shortHistory.Information,own)));
  SimulationMeasure("belief sample short "+shortHistory.Information.History.Length,10000,()=>GC.KeepAlive(worldSampler.Sample(belief,seed++)));
  SimulationMeasure("belief sample long "+longHistory.Information.History.Length,10000,()=>GC.KeepAlive(worldSampler.Sample(longBelief,seed++)));
  SimulationMeasure("belief unknown sample",5000,()=>GC.KeepAlive(worldSampler.Sample(unknown,seed++)));
  var sample=worldSampler.Sample(belief,633);
  SimulationMeasure("belief world build",10000,()=>GC.KeepAlive(shortHistory.Build(sample)));
  SimulationMeasure("belief world build and two actions",5000,()=>{
   var world=shortHistory.Build(sample);
   var end=new AgentMove{Kind="end"};
   if(!world.Apply(0,end).Success||!world.Apply(1,end).Success)throw new Exception("Benchmark world action failed.");
  });
 }
}

