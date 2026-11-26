using System;
using System.Linq;
using Touchfish;
public static partial class RuleTests {
 static MatchEngine TgtGame(string profession="MAGE"){
  var g=BrmGame(profession);g.CardPoolId=TournamentRules.SetId;return g;
 }
 static void Tournament(){
  var cards=catalog.Deckable(catalog.Pool(TournamentRules.SetId));
  Check(cards.Count==616&&cards.Count(c=>c.SetId==TournamentRules.SetId)==50,"50 tournament neutrals in a pool of 616 cards");
  foreach(var id in TournamentRules.Ids){
   var g=TgtGame();
   var friendly=g.Summon(0,"VAN_EX1_556");friendly.Race="MECHANICAL";
   var enemy=g.Summon(1,"VAN_EX1_319");
   string mask=g.Rules.Target(g.Card(id),null,g,0);
   MatchTarget target=mask=="FRIENDLY_MINION"||mask=="FRIENDLY_MECH"?BT(friendly):mask=="NONE"?null:BT(enemy);
   BrmPlay(g,id,target);
   Check(g.Players[0].Board.Any(u=>u.CardId==id),"Every neutral enters play: "+id);
  }
  var game=TgtGame();var squire=game.Summon(0,"AT_082");game.Summon(0,"AT_080");
  Check(game.HeroPower(0,new MatchTarget(1)).Success&&squire.BaseAttack+squire.BuffAttack==2,"Inspire first use");
  Check(game.HeroPower(0,new MatchTarget(1)).Success&&squire.BaseAttack+squire.BuffAttack==3,"Commander second use");
  Check(!game.HeroPower(0,new MatchTarget(1)).Success&&game.Players[0].PowersThisGame==2,"Commander cannot use third time");
  Check(game.HandCost(0,new HandCard{CardId="AT_120"})==8,"Giant tracks total hero powers");
  game=TgtGame();game.Summon(0,"AT_085");BrmPlay(game,"AT_115");
  Check(game.HeroPowerCost(0)==0&&game.HeroPower(0,new MatchTarget(1)).Success&&game.HeroPowerCost(0)==1,"Discount consumed, maiden remains");
  game=TgtGame();var guard=game.Summon(0,"AT_109");guard.SummonTurn=0;
  Check(game.CanAttack(0,guard)!=null,"Watchman starts disabled");game.HeroPower(0,new MatchTarget(1));
  Check(game.CanAttack(0,guard)==null,"Watchman enabled by inspire");game.EndTurn();game.EndTurn();
  Check(game.CanAttack(0,guard)!=null,"Watchman permission expires");
  game=TgtGame();var dragonhawk=game.Summon(0,"AT_083");game.HeroPower(0,new MatchTarget(1));
  Check(game.HasWindfury(dragonhawk),"Temporary windfury");game.EndTurn();
  Check(!game.HasWindfury(dragonhawk),"Temporary windfury expires");
  game=TgtGame();var bolf=game.Summon(0,"AT_124");int health=game.Players[0].Health;game.Damage(new MatchTarget(0),5);
  Check(game.Players[0].Health==health&&bolf.DamageTaken==5,"Bolf redirects damage");
  game.Silence(BT(bolf));game.Damage(new MatchTarget(0),2);Check(game.Players[0].Health==health-2,"Silence stops redirection");
  TournamentJoustReveals();
  foreach(string profession in new[]{"MAGE","PRIEST","WARRIOR","WARLOCK","HUNTER","PALADIN","ROGUE","DRUID","SHAMAN"}){
   game=TgtGame(profession);BrmPlay(game,"AT_132");
   Check(game.Players[0].HeroAbilityId=="tgt:upgraded-"+profession,"Upgrade "+profession);
   var result=game.HeroPower(0,profession=="MAGE"||profession=="PRIEST"?new MatchTarget(1):null,profession=="SHAMAN"?"VAN_CS2_050":null);
   Check(result.Success,"Upgraded ability executes "+profession);
  }

  game=TgtGame();game.Summon(0,"AT_099");game.Summon(0,"AT_100");game.Summon(0,"AT_113");
  Check(game.HeroPower(0,new MatchTarget(1)).Success&&game.Players[0].Board.Any(u=>u.CardId=="AT_099t")&&game.Players[0].Board.Any(u=>u.CardId=="VAN_CS2_101t")&&game.Players[0].Hand.Any(h=>h.CardId=="AT_113t"),"Inspire summons and generated hand cards");
  game=TgtGame();var returned=game.Summon(0,"AT_110");game.HeroPower(0,new MatchTarget(1));
  Check(!game.Players[0].Board.Contains(returned)&&game.Players[0].Hand.Any(h=>h.CardId=="AT_110"),"Inspire returns controller minion");
  game=TgtGame();var buff=game.Summon(0,"AT_119");game.HeroPower(0,new MatchTarget(1));Check(game.AttackValue(buff)==6&&buff.MaxHealth==6,"Raider gets permanent stats");
  game=TgtGame();var fio=game.Summon(0,"AT_129");TournamentRules.SpellTargeted(game,0,BT(fio));Check(fio.Shield,"Friendly targeted spell grants shield");game.Silence(BT(fio));TournamentRules.SpellTargeted(game,0,BT(fio));Check(!fio.Shield,"Silenced twin does not trigger");
  game=TgtGame();game.Summon(0,"AT_080");game.HeroPower(0,new MatchTarget(1));game.Players[0].Board.Clear();Check(!game.HeroPower(0,new MatchTarget(1)).Success,"Losing commander removes extra use immediately");
  game=TgtGame();game.Players[0].HeroAbilityId="tgt:upgraded-SHAMAN";Check(!game.HeroPower(0,choice:"invalid").Success&&game.Players[0].Mana==10,"Invalid totem does not spend mana");
  Check(game.HeroPower(0,choice:"VAN_CS2_050").Success,"Chosen totem resolves");game.Players[0].PowerUsed=false;game.Players[0].PowersThisTurn=0;
  Check(game.HeroPower(0,choice:"VAN_CS2_050").Success&&game.Players[0].Board.Count==2,"Upgraded shaman can summon duplicate totems");

  game=TgtGame();game.Summon(0,"FP1_031");game.Players[0].Deck.Clear();game.Players[0].Deck.Add("AT_125");game.Players[1].Deck.Clear();game.Players[1].Deck.Add("AT_082");
  var knight=game.Summon(0,"AT_128");knight.DamageTaken=99;game.Cleanup();
  Check(game.Players[0].Hand.Count(h=>h.CardId=="AT_128")==2,"Baron doubles successful knight deathrattle");
  game=TgtGame();var maw=game.Summon(0,"AT_123");var body=game.Summon(1,"AT_101");GvgHand(game,"AT_017");maw.DamageTaken=99;game.Cleanup();
  Check(body.DamageTaken==3,"Chillmaw checks held dragon at death");
  game=TgtGame();BrmPlay(game,"AT_086");game.EndTurn();Check(game.HeroPowerCost(1)==7,"Saboteur applies next turn");game.EndTurn();game.EndTurn();Check(game.HeroPowerCost(1)==2,"Saboteur expires");
  game=TgtGame();game.Players[1].HeroAbilityId=HeroReplacementRules.InfernoId;BrmPlay(game,"AT_098");
  Check(game.Players[0].HeroAbilityId==HeroReplacementRules.InfernoId,"Copy special hero ability");
  game=TgtGame();var ice=game.Summon(0,"AT_125");Check(!game.Attack(0,ice.Id,new MatchTarget(1)).Success,"Icehowl cannot hit hero");
  game=TgtGame();var grave=game.Summon(0,"AT_128");game.Silence(BT(grave));grave.DamageTaken=99;game.Cleanup();
  Check(!game.Players[0].Hand.Any(c=>c.CardId=="AT_128"),"Silenced knight stays dead");
 }
 static void TournamentJoustReveals(){
  var game=TgtGame("HUNTER");
  var own=new[]{"VAN_CS2_029","AT_125","VAN_CS2_024","AT_125"};
  var enemy=new[]{"VAN_CS2_023","AT_082","VAN_CS2_027"};
  game.Players[0].Deck.Clear();game.Players[0].Deck.AddRange(own);
  game.Players[1].Deck.Clear();game.Players[1].Deck.AddRange(enemy);
  BrmPlay(game,"AT_133");
  var reveal=game.VisualEvents.Single(item=>item.Kind=="joust");
  Check(reveal.JoustCards.SequenceEqual(new[]{"AT_125","AT_082"})&&reveal.JoustWinner==0,
   "Gadgetzan Jouster reveals only the compared minions and marks the higher printed cost");
  Check(game.Players[0].Deck.SequenceEqual(own)&&game.Players[1].Deck.SequenceEqual(enemy),
   "Joust reveals in place and preserves both deck orders exactly");
  for(int seat=0;seat<2;seat++){
   var view=LanProtocol.View(game,seat,"joust",1);
   var remote=LanProtocol.ReadView(catalog,view);
   var shared=remote.VisualEvents.Single(item=>item.Kind=="joust");
   Check(shared.JoustCards.SequenceEqual(reveal.JoustCards)&&shared.JoustWinner==0
    &&remote.Players[1-seat].Deck.All(id=>id=="HIDDEN"),
    "Both LAN views get the comparison without the other deck or the revealed indices");
  }
  Check(!TournamentRules.Joust(game,1)&&game.VisualEvents.Last().JoustWinner==0,
   "A higher opposing card wins the comparison but does not activate the initiating card");
  game.Players[1].Deck[1]="AT_125";
  Check(!TournamentRules.Joust(game,0)&&game.VisualEvents.Last().JoustWinner==-1,"Ties have no winner");
  game.Players[1].Deck[1]="AT_082";
  game.Players[0].Deck[1]="VAN_CS2_026";
  var remaining=game.Players[0].Deck.Take(3).ToArray();
  BrmPlay(game,"AT_058");
  Check(game.Players[0].Hand.Count(card=>card.CardId=="AT_125")==1
   &&game.Players[0].Deck.SequenceEqual(remaining)&&game.Players[1].Deck.SequenceEqual(enemy),
   "Winning Elekk separately draws a minion and keeps the remaining cards in order");
  game.Players[0].Deck.Add("AT_125");
  game.Players[1].Deck.Clear();game.Players[1].Deck.Add("VAN_CS2_023");
  Check(TournamentRules.Joust(game,0)&&game.VisualEvents.Last().JoustCards[1]==null,
   "A deck with only spells shows no minion");
 }
 static void AvianaAura(CardRecord card){
  string auraId=card.Id;
  Check(matchRules.Supports(card)&&card.DbfId==2796&&card.CardClass=="DRUID"&&card.Rarity=="LEGENDARY"&&card.Cost==9&&card.Attack==5&&card.Health==5,"Aviana uses the historical identity and stats");
  var game=TgtClassGame("DRUID");
  int handId=GvgHand(game,"VAN_CS2_182");
  var hand=game.Players[0].Hand.Single(h=>h.Id==handId);
  hand.CostAdjustment=2;
  var zero=new HandCard{CardId="VAN_CS2_231"};
  var spell=new HandCard{CardId="VAN_CS2_029"};
  var weapon=new HandCard{CardId="VAN_CS2_106"};

  Check(game.HandCost(0,hand)==6,"Hand modifier applies before aura enters");
  BrmPlay(game,auraId);
  Check(game.Players[0].Mana==1,"The first aura source costs nine");
  var aura=game.Players[0].Board.Single(u=>u.CardId==auraId);
  Check(game.HandCost(0,hand)==1&&game.HandCost(0,zero)==1&&game.HandCost(1,hand)==6,"Only controller's minions are set to one, including zero-cost minions");
  Check(game.HandCost(0,spell)==game.Card(spell.CardId).Cost&&game.HandCost(0,weapon)==game.Card(weapon.CardId).Cost,"Spells and weapons keep their usual costs");
  Check(AgentBridge.Observe(game,0).Moves.Any(m=>m.Kind=="play"&&m.Source==hand.Id&&m.Cost==1),"Legal action quotes one mana");
  Check(game.Play(0,hand.Id).Success&&game.Players[0].Mana==0,"Actually pays one mana to play a minion");

  var second=game.Summon(0,auraId);
  game.Silence(BT(aura));
  Check(game.HandCost(0,hand)==1,"Another live copy keeps the aura active");
  second.DamageTaken=second.MaxHealth;
  game.Cleanup();
  Check(game.HandCost(0,hand)==6&&game.HandCost(0,zero)==0,"Removal of the last active source restores computed costs");
 }
}
