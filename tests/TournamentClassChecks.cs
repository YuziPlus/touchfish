using System;
using System.Linq;
using Touchfish;
public static partial class RuleTests {
 static MatchEngine TgtClassGame(string job="MAGE"){
  var g=TgtGame(job);g.CardPoolId=TournamentClassRules.PoolId;return g;
 }
 static void TournamentClasses(){
  var pool=catalog.Pool(TournamentClassRules.PoolId);
  var deckable=catalog.Deckable(pool);
  var cards=deckable.Where(c=>c.SetId==TournamentClassRules.SetId).ToArray();
  Check(deckable.Count==698&&catalog.ResolveDbf(pool,900001)==null,"Full pool contains 698 historical cards and excludes the placeholder DBF ID");
  Check(cards.Length==82&&cards.GroupBy(c=>c.CardClass).Count()==9&&cards.GroupBy(c=>c.CardClass).All(g=>g.Count()==(g.Key=="HUNTER"?10:9)),"All nine classes have their historical card counts");
  Check(!pool.Archived&&deckable.All(matchRules.Supports),"All 698 cards are playable in the active pool");
  Check(!matchRules.Supports(new CardRecord{Id="AT_future",EffectId="AT_future",SetId=TournamentClassRules.SetId,Type="MINION"}),"Unregistered tournament effects do not silently become playable");
  AvianaAura(cards.Single(c=>c.Id=="AT_045"));
  foreach(var c in cards){
   Check(matchRules.Supports(c),"Registered "+c.Id);
   var g=TgtClassGame(c.CardClass);
   var friend=g.Summon(0,"VAN_CS2_182");friend.Race="BEAST";
   var enemy=g.Summon(1,"VAN_EX1_319");
   var choices=g.Rules.Choices(c);string choice=choices.Length==0?null:choices[0].Key;
   string mask=g.Rules.Target(c,choice,g,0);
   var target=mask=="NONE"?null:mask.StartsWith("FRIENDLY")?BT(friend):BT(enemy);
   BrmPlay(g,c.Id,target,choice);
  }
  var game=TgtClassGame("MAGE");game.Summon(0,"AT_003");int h=game.Players[1].Health;
  Check(game.HeroPower(0,new MatchTarget(1)).Success&&game.Players[1].Health==h-2,"Fallen hero boosts power damage");
  game=TgtClassGame("MAGE");game.Summon(0,"AT_008");game.Players[0].Mana=10;
  for(int i=0;i<5;i++)Check(game.HeroPower(0,new MatchTarget(1)).Success,"Coldarra repeated power");
  game=TgtClassGame("WARLOCK");game.Summon(0,"AT_021");int fist=GvgHand(game,"AT_022");h=game.Players[1].Health;game.DiscardAll(0);game.Cleanup();
  Check(game.Players[1].Health==h-4&&game.AttackValue(game.Players[0].Board[0])==4,"Discard fist damages and tiny knight grows");
  game=TgtClassGame("WARLOCK");game.Summon(0,"AT_027");game.Players[0].Deck.Clear();game.Players[0].Deck.Add("VAN_CS2_182");game.HeroPower(0);
  Check(game.HandCost(0,game.Players[0].Hand.Last())==0,"Fizzlebang sets life-tap draw to zero");
  game=TgtClassGame("SHAMAN");game.Players[0].Deck.Clear();game.Players[0].Deck.Add("VAN_CS2_182");BrmPlay(game,"AT_054");game.Draw(0,1);
  var drawn=game.Players[0].Hand.Last();Check(drawn.AttackBonus==1&&drawn.HealthBonus==1,"Mistcaller buffs drawn minion");
  game.Players[0].Mana=10;game.Play(0,drawn.Id);var buffed=game.Players[0].Board.Last();Check(buffed.BuffAttack==1&&buffed.BuffHealth==1,"Hand growth reaches board");game.Silence(BT(buffed));Check(buffed.BuffAttack==0&&buffed.BuffHealth==0,"Silence removes Mistcaller growth");
  game=TgtClassGame("ROGUE");game.Players[1].Deck.Clear();game.Players[1].Deck.Add("AT_035t");game.Players[1].Deck.Add("VAN_CS2_182");game.Draw(1,1);
  Check(game.Players[0].Board.Any(u=>u.CardId=="AT_036t")&&game.Players[1].Hand.Any(c=>c.CardId=="VAN_CS2_182"),"Ambush summons for opponent and replaces draw");
  game=TgtClassGame("WARLOCK");var horse=game.Summon(0,"AT_019");horse.DamageTaken=99;game.Cleanup();
  Check(game.Players[0].Board.Any(u=>u.CardId=="AT_019"),"Original Dreadsteed immediately returns");
  TournamentClassHistoricalEffects();
 }
 static void TournamentClassHistoricalEffects(){
  var deck=DeckRules.ForCurrentPlay(catalog,matchRules.TrainingDeck("DRUID"));
  string previous=deck.Cards.Keys.First();deck.Cards[previous]--;deck.Cards.Add("AT_045",1);
  var decoded=DeckCode.Decode(catalog,DeckCode.Encode(catalog,deck),TournamentClassRules.PoolId);
  Check(decoded.Count==30&&decoded.Cards["AT_045"]==1,"Aviana round-trips with her historical ID");

  var game=TgtClassGame("PRIEST");BrmPlay(game,"AT_116");
  var warden=game.Players[0].Board.Single();Check(!warden.Taunt&&game.AttackValue(warden)==1,"Wyrmrest Agent stays 1/4 without a dragon");
  GvgHand(game,"AT_017");BrmPlay(game,"AT_116");warden=game.Players[0].Board.Last();
  Check(warden.Taunt&&game.AttackValue(warden)==2&&warden.Health==4,"Wyrmrest Agent checks a dragon in hand");

  game=TgtClassGame("WARRIOR");var ally=game.Summon(0,"VAN_CS2_182");var enemy=game.Summon(1,"VAN_CS2_182");
  game.Players[0].Deck.Insert(0,"AT_130");game.Draw(0,1);
  Check(ally.DamageTaken==1&&enemy.DamageTaken==0&&game.Players[0].Hand.Single().CardId=="AT_130","Sea Reaver damages only friendly minions when drawn and stays in hand");
  BrmPlay(game,"AT_130");Check(ally.DamageTaken==1,"Playing Sea Reaver does not repeat its draw trigger");
  while(game.Players[0].Hand.Count<game.Mode.HandLimit)GvgHand(game,"VAN_CS2_182");
  game.Players[0].Deck.Insert(0,"AT_130");game.Draw(0,1);Check(ally.DamageTaken==1,"Burning Sea Reaver does not trigger it");

  game=TgtClassGame("MAGE");enemy=game.Summon(1,"VAN_CS2_182");BrmPlay(game,"AT_005",BT(enemy));
  var boar=game.Players[1].Board.Single();var boarCard=game.Card(boar.CardId);
  Check(boar.CardId=="AT_005t"&&boarCard.DbfId==2543&&boarCard.Cost==3&&boarCard.CardClass=="NEUTRAL"&&game.AttackValue(boar)==4&&boar.Health==2&&game.HasCharge(boar),"Polymorph uses the historical 3-mana 4/2 Boar with Charge");

  game=TgtClassGame("HUNTER");BrmPlay(game,"AT_060");enemy=game.Summon(1,"VAN_CS2_182");game.EndTurn();
  Check(game.Attack(1,enemy.Id,new MatchTarget(0)).Success,"Enemy attacks the defending hero");
  var bear=game.Players[0].Board.Single();Check(bear.CardId=="VAN_CS2_125"&&bear.Taunt&&game.AttackValue(bear)==3&&bear.Health==3,"Bear Trap summons Ironfur Grizzly after the hit");

  game=TgtClassGame("PALADIN");game.Summon(0,"AT_076");game.Summon(0,"AT_090");game.HeroPower(0);
  var murloc=game.Players[0].Board.Last();Check(murloc.Race=="MURLOC"&&murloc.BuffAttack==1&&murloc.BuffHealth==1,"Class and neutral Inspire effects resolve together in entry order");
 }
}
