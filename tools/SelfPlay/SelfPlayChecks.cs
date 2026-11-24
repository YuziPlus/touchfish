using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Touchfish;

internal static class SelfPlayChecks {
 static void Check(bool condition,string message){
  if(!condition)throw new InvalidOperationException(message);
 }

 internal static void Run(CardCatalog catalog){
  var zoo=ZooWarlockDeck.Create(catalog);
  var freeze=FreezeMageDeck.Create(catalog);
  var game=new MatchEngine(catalog,zoo,freeze,732);
  game.Mulligan(0,null);game.Mulligan(1,null);
  var view=AgentBridge.Observe(game,0,false,true);
  var original=JsonData.Serializer().Serialize(SelfPlay.Observation(game,view,zoo.Cards));
  game.Players[1].Deck.Reverse();
  game.Players[1].Hand[0].CardId="VAN_EX1_289";
  game.Players[1].Hand[0].CostAdjustment=-2;
  view=AgentBridge.Observe(game,0,false,true);
  Check(original==JsonData.Serializer().Serialize(SelfPlay.Observation(game,view,zoo.Cards)),"Opponent hidden data leaked into training observation.");

  game.ArmSecret(1,new HandCard{Id=game.NewEffectInstanceId(),CardId="VAN_EX1_289"});
  var attacker=game.Summon(0,"VAN_CS2_182");
  attacker.SummonTurn=0;
  var attackView=AgentBridge.Observe(game,0,false,true);
  var attack=SearchPolicy.Actions(attackView).Single(action=>action.Move.Kind=="attack"&&action.Move.Target.Hero);
  Check(attack.Execute(game,0).Success,"Secret test attack rejected.");
  var history=game.ObserveHistory(0);
  Check(history.Single(e=>e.Kind==HistoryKind.ArmSecret).CardId==null&&
   history.Single(e=>e.Kind==HistoryKind.RevealSecret).CardId=="VAN_EX1_289","Secret identity was not limited to its public reveal.");

  var fixture=new DynamicDecisionFixture(catalog);
  var dynamic=fixture.OpenForBelief(319,freeze,freeze);
  var pending=AgentBridge.Observe(dynamic,0,false,true);
  Check(AgentBridge.Observe(dynamic,1,false,true).Decision==null,"Opponent can see a private choice.");
  var options=SearchPolicy.Actions(pending);
  Check(options.Length==3,"Dynamic candidates missing.");
  for(int i=0;i<options.Length;i++){
   var candidate=SelfPlay.Candidate(pending,options[i],i,new Dictionary<string,RootActionStatistics>());
   Check(candidate.option_index==i&&candidate.choice==pending.Decision.Options[i].Id&&candidate.visits==null,"Invalid recorded dynamic action.");
  }
  Check(options[0].Execute(dynamic,0).Success&&dynamic.HasPendingDecision,"Dynamic continuation rejected.");
  Check(SearchPolicy.Actions(AgentBridge.Observe(dynamic,0,false,true))[0].Execute(dynamic,0).Success&&!dynamic.HasPendingDecision,"Second dynamic continuation rejected.");
  var copy=fixture.OpenForBelief(319,freeze,freeze);
  Check(pending.Decision.Options.Select(x=>x.Id).SequenceEqual(copy.ObserveDecision(0).Options.Select(x=>x.Id)),"Random option sampling is not reproducible.");

  var schedule=SelfPlay.Schedule(new SelfPlayConfig{Games=2,Seed=54,FirstSeat="paired",Decks=new[]{"zoo","freeze"}});
  Check(schedule[0].Seed==schedule[1].Seed&&schedule[0].FirstSeat!=schedule[1].FirstSeat,"Pairing changed seed or failed to exchange initiative.");
  Console.WriteLine("PASS self-play: visibility, secret trigger, dynamic legality and continuation, seeded random candidates, paired initiative");
 }
}
