using System;
using System.Linq;
using System.IO;

using Touchfish;
public static partial class RuleTests {
 static void HyenaLethalDeathrattleRegression(){
  var g=InteractionSeedFixture();g.Active=1;var abom=g.Summon(0,"VAN_EX1_097");abom.DamageTaken=2;g.Summon(0,"VAN_CS2_171");var hyena=g.Summon(0,"VAN_EX1_531");
  Check(g.Play(1,Hand(g,"EX1_251",1)).Success,"Forked Lightning resolves Abomination and beast death wave");
  Check(g.Players[0].Board.Count==0&&hyena.Health<=0,"Mortally wounded Hyena cannot revive from a later beast trigger");
  g=MatchFixture();var living=g.Summon(0,"VAN_EX1_531");var beast=g.Summon(0,"VAN_CS2_171");g.Damage(new MatchTarget(0,beast.Id),1);g.Cleanup();
  Check(g.AttackValue(living)==4&&living.Health==3,"Living Hyena still receives beast death buff");
 }
 static void HunterBeastTargets(){
  foreach(string key in new[]{"DS1_070","EX1_549"})for(int seat=0;seat<2;seat++){
   var g=MatchFixture("HUNTER","HUNTER");g.Active=seat;var friendly=g.Summon(seat,"VAN_DS1_175");var other=g.Summon(seat,"VAN_CS2_182");var enemy=g.Summon(1-seat,"VAN_DS1_175");int hand=Inject(g,key,seat);int mana=g.Players[seat].Mana,log=g.Log.Count,board=g.Players[seat].Board.Count,played=g.CardsPlayed;
   foreach(var target in new[]{new MatchTarget(seat,other.Id),new MatchTarget(1-seat,enemy.Id),new MatchTarget(seat),new MatchTarget(1-seat),new MatchTarget(seat,Int32.MaxValue)}){
    Check(!g.Play(seat,hand,target).Success,key+" rejects non-Beasts, enemy Beasts, heroes and stale targets for seat "+seat);
    Check(g.Players[seat].Mana==mana&&g.Players[seat].Hand.Any(h=>h.Id==hand)&&g.Players[seat].Board.Count==board&&g.Log.Count==log&&g.CardsPlayed==played&&other.BuffAttack==0&&enemy.BuffAttack==0,key+" invalid target leaves resources and board unchanged");
   }
   g.Silence(new MatchTarget(seat,friendly.Id));
   Check(g.Play(seat,hand,new MatchTarget(seat,friendly.Id)).Success&&g.AttackValue(friendly)==3,key+" accepts a silenced friendly Beast for seat "+seat);
  }
  var empty=MatchFixture("HUNTER","MAGE");empty.Summon(0,"VAN_CS2_182");empty.Summon(1,"VAN_DS1_175");
  Check(!empty.HasTargets(0,"FRIENDLY_BEAST",false),"Heroes and non-Beasts do not count as available Beast battlecry targets");
  Check(empty.Play(0,Inject(empty,"DS1_070")).Success&&empty.Players[0].Board.Last().CardId=="VAN_DS1_070","Houndmaster can be played without a battlecry when no friendly Beast exists");
  int wrath=Inject(empty,"EX1_549"),beforeMana=empty.Players[0].Mana;
  Check(!empty.Play(0,wrath).Success&&empty.Players[0].Mana==beforeMana&&empty.Players[0].Hand.Any(h=>h.Id==wrath),"Bestial Wrath cannot be spent without a legal Beast target");
  var changed=MatchFixture("HUNTER","MAGE");var unit=changed.Summon(0,"VAN_DS1_175");changed.Transform(new MatchTarget(0,unit.Id),"VAN_CS2_182");
  Check(!changed.HasTargets(0,"FRIENDLY_BEAST",true),"A transformed former Beast no longer qualifies");
  changed.Transform(new MatchTarget(0,changed.Players[0].Board.Single().Id),"VAN_DS1_175");
  Check(changed.HasTargets(0,"FRIENDLY_BEAST",true),"A minion transformed into a Beast qualifies");
 }
 static void Hunter(){HyenaLethalDeathrattleRegression();HunterBeastTargets();
  int before=checks;string[] ids={"CS2_237","DS1_070","DS1_183","DS1_184","DS1_188","EX1_531","EX1_536","EX1_537","EX1_538","EX1_544","EX1_549"};
  Check(ids.All(cardId=>matchRules.Supports(catalog.Card("VAN_"+cardId))),"All eleven remaining Hunter cards are registered");
  var hunterCards=catalog.Deckable(catalog.Pool("classic-2014")).Where(c=>c.CardClass=="HUNTER").ToArray();Check(hunterCards.Length==25&&hunterCards.All(matchRules.Supports),"All 25 classic Hunter cards are playable");
  var g=MatchFixture("HUNTER","MAGE");var hyena=g.Summon(0,"VAN_EX1_531");var beast=g.Summon(0,"VAN_DS1_175");beast.DamageTaken=beast.MaxHealth;g.Cleanup();Check(g.AttackValue(hyena)==4&&hyena.MaxHealth==3,"Scavenging Hyena gains +2/+1 when a friendly Beast dies");
  g=MatchFixture("HUNTER","MAGE");g.Summon(0,"VAN_CS2_237");int beforeDraw=g.Players[0].Hand.Count;g.Summon(0,"VAN_DS1_175");Check(g.Players[0].Hand.Count==beforeDraw+1,"Starving Buzzard draws when a friendly Beast is summoned");
  g=MatchFixture("HUNTER","MAGE");var friendlyBeast=g.Summon(0,"VAN_DS1_175");int houndmaster=Inject(g,"DS1_070");Check(g.Play(0,houndmaster,new MatchTarget(0,friendlyBeast.Id)).Success&&g.AttackValue(friendlyBeast)==3&&friendlyBeast.MaxHealth==3&&friendlyBeast.Taunt,"Houndmaster buffs a friendly Beast and grants Taunt");
  g=MatchFixture("HUNTER","MAGE");g.Players[0].Deck.Clear();g.Players[0].Deck.AddRange(new[]{"VAN_CS2_182","VAN_CS2_179","VAN_CS2_231","VAN_CS2_182"});int tracking=Inject(g,"DS1_184");var options=matchRules.Choices(catalog.Card("VAN_DS1_184"),g,0);Check(options.Length==3&&options[1].Title==g.Card("VAN_CS2_179").Name,"Tracking presents the top three cards privately");Check(g.Play(0,tracking,null,-1,"track:1").Success&&g.Players[0].Hand.Any(h=>h.CardId=="VAN_CS2_179")&&g.Players[0].Deck.SequenceEqual(new[]{"VAN_CS2_182"})&&g.GraveyardFor(0,GraveyardReason.Discarded).Count(e=>e.CardId!="VAN_DS1_184")==2,"Tracking keeps the selected card and discards the other two");Check(!g.Log.Any(entry=>entry.Contains(g.Card("VAN_CS2_182").Name)||entry.Contains(g.Card("VAN_CS2_179").Name)||entry.Contains(g.Card("VAN_CS2_231").Name)),"Tracking choices do not leak through the public match log");
  g=MatchFixture("HUNTER","MAGE");g.Players[0].Deck.Clear();g.Players[0].Deck.AddRange(new[]{"VAN_CS2_182","VAN_CS2_179","VAN_CS2_231"});g.Players[1].Deck.Clear();g.Players[1].Deck.AddRange(new[]{"VAN_CS2_182","VAN_CS2_182"});Inject(g,"DS1_184",1);var snapshot=LanProtocol.View(g,1,"hunter",0);Check(snapshot.Players[0].DeckPreview==null&&snapshot.Players[1].DeckPreview.SequenceEqual(g.Players[1].Deck.Take(3)),"LAN Tracking preview exposes only the viewing player's own deck top");var view=LanProtocol.ReadView(catalog,snapshot);Check(HunterRules.TrackingOptions(view,1).Length==2&&HunterRules.TrackingOptions(view,0).Length==0,"LAN Tracking options remain private and available to the player");
  g=MatchFixture("HUNTER","MAGE");var enemyA=g.Summon(1,"VAN_EX1_076");var enemyB=g.Summon(1,"VAN_EX1_076");var enemyC=g.Summon(1,"VAN_EX1_076");int multi=Inject(g,"DS1_183");Check(g.Play(0,multi).Success&&new[]{enemyA,enemyB,enemyC}.Count(u=>u.DamageTaken==3)==2,"Multi-Shot damages two distinct random enemy minions");
  g=MatchFixture("HUNTER","MAGE");g.Equip(0,"VAN_DS1_188");var longbowTarget=g.Summon(1,"VAN_CS2_179");Check(g.Attack(0,0,new MatchTarget(1,longbowTarget.Id)).Success&&g.Players[0].Health==30,"Gladiator's Longbow prevents combat damage to the attacking hero");
  g=MatchFixture("HUNTER","MAGE");g.EquipRaw(0,"VAN_EX1_536",3,2,false);g.ArmSecret(0,new HandCard{Id=id++,CardId="VAN_EX1_136"});g.Active=1;var rescued=g.Summon(0,"VAN_CS2_182");rescued.DamageTaken=rescued.MaxHealth;g.Cleanup();Check(g.Players[0].WeaponDurability==3,"Eaglehorn Bow gains durability when a friendly Secret is revealed");
  g=MatchFixture("HUNTER","MAGE");var center=g.Summon(1,"VAN_CS2_182");var left=g.Summon(1,"VAN_CS2_182",0);var right=g.Summon(1,"VAN_CS2_182");int explosive=Inject(g,"EX1_537");Check(g.Play(0,explosive,new MatchTarget(1,center.Id)).Success&&center.Health==0&&left.Health==3&&right.Health==3,"Explosive Shot damages its target and adjacent minions");
  g=MatchFixture("HUNTER","MAGE");g.Summon(1,"VAN_CS2_182");g.Summon(1,"VAN_CS2_179");int unleash=Inject(g,"EX1_538");Check(g.Play(0,unleash).Success&&g.Players[0].Board.Count==2&&g.Players[0].Board.All(u=>u.CardId=="GAME_HOUND"&&u.Race=="BEAST"&&g.HasCharge(u)),"Unleash the Hounds summons one charging Beast per enemy minion");
  g=MatchFixture("HUNTER","MAGE");var hidden=g.Summon(0,"VAN_CS2_182");hidden.Stealth=true;g.ArmSecret(1,new HandCard{Id=id++,CardId="VAN_EX1_136"});g.Players[0].Deck.Clear();g.Players[0].Deck.Add("VAN_CS2_179");int flare=Inject(g,"EX1_544");Check(g.Play(0,flare).Success&&!hidden.Stealth&&g.Players[1].SecretCount==0&&g.Players[0].Hand.Any(h=>h.CardId=="VAN_CS2_179"),"Flare removes Stealth, destroys enemy Secrets and draws a card");
  g=MatchFixture("HUNTER","MAGE");var protectedBeast=g.Summon(0,"VAN_DS1_175");int wrath=Inject(g,"EX1_549");Check(g.Play(0,wrath,new MatchTarget(0,protectedBeast.Id)).Success&&g.AttackValue(protectedBeast)==3&&g.Damage(new MatchTarget(0,protectedBeast.Id),3)==0,"Bestial Wrath grants +2 Attack and immunity for the turn");g.EndTurn();Check(g.Damage(new MatchTarget(0,protectedBeast.Id),1)==1&&g.AttackValue(protectedBeast)==1,"Bestial Wrath immunity and attack expire at end of turn");
  g=MatchFixture("HUNTER","MAGE");tracking=Inject(g,"DS1_184");int mana=g.Players[0].Mana;string[] beforeDeck=g.Players[0].Deck.ToArray();Check(!g.Play(0,tracking,null,-1,"track:99").Success&&g.Players[0].Mana==mana&&g.Players[0].Deck.SequenceEqual(beforeDeck),"Invalid Tracking choice preserves resources and deck");
  g=MatchFixture("HUNTER","MAGE");var buzz=g.Summon(0,"VAN_CS2_237");int count=g.Players[0].Hand.Count;g.Transform(new MatchTarget(0,buzz.Id),"VAN_CS2_237");Check(g.Players[0].Hand.Count==count,"Transformation and Buzzard own summon do not draw");var knife=g.Summon(0,"VAN_NEW1_019");g.Summon(1,"VAN_CS2_182");int hp=g.Players[1].Health+g.Players[1].Board.Sum(u=>u.Health);g.Summon(0,"GAME_HOUND");Check(g.Players[0].Hand.Count==count+1&&g.Players[1].Health+g.Players[1].Board.Sum(u=>u.Health)==hp-1,"Hunter draw and neutral dagger share summon trigger phase");
  g=MatchFixture("HUNTER","MAGE");g.Summon(0,"VAN_EX1_563");g.Summon(0,"VAN_EX1_350");enemyA=g.Summon(1,"VAN_EX1_096");enemyA.BuffHealth=50;enemyB=g.Summon(1,"VAN_EX1_096");enemyB.BuffHealth=50;Check(g.Play(0,Inject(g,"DS1_183")).Success&&enemyA.DamageTaken==16&&enemyB.DamageTaken==16,"Hunter spell damage uses spell power plus Velen pipeline");
 }
}
