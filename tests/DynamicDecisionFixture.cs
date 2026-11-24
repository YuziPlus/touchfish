using System;
using System.Linq;

namespace Touchfish {
internal sealed class DynamicDecisionFixture {
 internal const string Single="TEST_DYNAMIC_BEAST",Double="TEST_DYNAMIC_SPELL",Lethal="TEST_DYNAMIC_LETHAL";
 readonly CardCatalog catalog;
 readonly DeckDocument deck;
 internal readonly DecisionStep Discover;

 internal DynamicDecisionFixture(CardCatalog original){
  catalog=original.CopyForSimulation();
  Add(Single,"测试发现随从","MINION");
  Add(Double,"测试双重发现","SPELL");
  Add(Lethal,"测试终结决策","SPELL");
  deck=new MatchRules(catalog).TrainingDeck("MAGE");
  var candidates=new[]{"VAN_CS2_182","VAN_CS2_231","VAN_CS2_029","VAN_EX1_029","VAN_CS2_023","VAN_CS2_120"};
  Discover=new DecisionStep("发现一张牌",3,candidates.Select((id,index)=>new DecisionCandidate("pick:"+id,id,index+1)).ToArray());
 }

 void Add(string id,string name,string type){
  catalog.Cards.Add(id,new CardRecord{Id=id,BaseId=id,EffectId="test:"+id,Name=name,Type=type,CardClass="NEUTRAL",
   SetId="classic-2014",Cost=2,Attack=2,Health=2,Race=type=="MINION"?"BEAST":null,Text="隔离测试。",Mechanics=new string[0]});
 }

 internal MatchEngine OpponentCanOpenDecision(int seed,DeckDocument own,DeckDocument enemy){
  var game=new MatchEngine(catalog,own,enemy,seed);
  game.Mulligan(0,null);
  game.Mulligan(1,null);
  game.Rules.RegisterDynamic("test:"+Double,new DecisionProgram(Discover,Discover));
  game.Players[1].Hand.Add(new HandCard{Id=game.NewEffectInstanceId(),CardId=Double,Generated=true});
  game.NoteHistory(HistoryKind.Gain,1,Double);
  game.Players[0].Mana=2;
  game.Players[1].MaxMana=1;
  return game;
 }

 internal MatchEngine OpenForTreeReuse(DeckDocument own,DeckDocument enemy){
  var game=new MatchEngine(catalog,own,enemy,629);
  game.Mulligan(0,null);
  game.Mulligan(1,null);
  var step=new DecisionStep("固定候选",3,new DecisionCandidate("ogre","VAN_CS2_200"),
   new DecisionCandidate("champion","VAN_CS2_222"),new DecisionCandidate("core","VAN_CS2_201"));
  game.Rules.RegisterDynamic("test:"+Double,new DecisionProgram(step,step));
  game.Players[0].Health=game.Players[1].Health=4;
  game.Summon(0,"VAN_CS2_182").SummonTurn=0;
  game.Summon(1,"VAN_CS2_182").SummonTurn=0;
  game.Players[0].Mana=2;
  game.GiveNaxxCard(0,Double);
  if(!game.Play(0,game.Players[0].Hand.Last().Id).Success)throw new InvalidOperationException("Reuse fixture did not pause.");
  return game;
 }

 internal MatchEngine OpenForBelief(int seed,DeckDocument own,DeckDocument enemy){
  var game=new MatchEngine(catalog,own,enemy,seed);
  game.Mulligan(0,null);
  game.Mulligan(1,null);
  game.Rules.RegisterDynamic("test:"+Double,new DecisionProgram(Discover,Discover));
  game.GiveNaxxCard(0,Double);
  game.Players[0].Mana=10;
  if(!game.Play(0,game.Players[0].Hand.Last().Id).Success)throw new InvalidOperationException("Belief fixture did not pause.");
  return game;
 }

 internal MatchEngine NewGame(int seed,string id=Double,int seat=0){
  var game=new MatchEngine(catalog,deck,deck,seed,testMode:true,startingSeat:seat);
  game.Rules.RegisterDynamic("test:"+Single,new DecisionProgram(Discover));
  game.Rules.RegisterDynamic("test:"+Double,new DecisionProgram(Discover,Discover));
  game.Rules.RegisterDynamic("test:"+Lethal,new DecisionProgram(new DecisionStep("选择伤害",1,
   new DecisionCandidate("finish","结束对局","造成 30 点伤害",30)),Discover));
  game.MarkMulliganCompleteForTests();
  foreach(var player in game.Players){player.Hand.Clear();player.Mana=10;}
  game.Players[seat].Hand.Add(new HandCard{Id=9000,CardId=id});
  return game;
 }
}
}
