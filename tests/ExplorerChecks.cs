using System;
using System.Linq;
using Touchfish;

public static partial class RuleTests {
 static MatchEngine ExplorerGame(string profession="MAGE"){
  var g=BrmGame(profession);
  g.CardPoolId=ExplorerRules.PoolId;
  return g;
 }

 static void Explorers(){
  if(ExplorerRules.IsLockedPool(catalog.Pool(ExplorerRules.PoolId))){
   LockedExplorers();
   return;
  }
  var active=catalog.Pools.Single(p=>!p.Archived);
  var all=catalog.Deckable(active);
  var cards=all.Where(c=>c.SetId==ExplorerRules.PoolId).ToArray();
  Check(active.Id==ExplorerRules.PoolId&&all.Count==743&&all.All(matchRules.Supports),"743 supported cards in the Explorer pool");
  Check(cards.Length==45&&cards.Count(c=>c.CardClass=="NEUTRAL")==18&&
   cards.Where(c=>c.CardClass!="NEUTRAL").GroupBy(c=>c.CardClass).Count(g=>g.Count()==3)==9,
   "18 neutral and three cards per class");
  var set=catalog.Cards.Values.Where(c=>c.SetId==ExplorerRules.PoolId).ToArray();
  Check(set.Count(c=>!c.Collectible)==13&&set.All(matchRules.Supports),"All bonus cards have implemented effects and remain outside deck construction");
  Check(catalog.Card("LOE_077").Health==4&&catalog.Card("LOE_038").Cost==5&&catalog.Card("LOE_118").Durability==3,
   "Frozen launch stats");
  var old=DeckRules.ForCurrentPlay(catalog,matchRules.TrainingDeck("MAGE"));
  old.Cards.Remove(old.Cards.Keys.First());
  old.Cards["LOE_002"]=2;
  var copy=DeckCode.Decode(catalog,DeckCode.Encode(catalog,old),active.Id);
  Check(copy.Count==30&&copy.Cards["LOE_002"]==2,"Explorer deckstring imports into current pool");

  foreach(var card in cards){
   var g=ExplorerGame(card.CardClass=="NEUTRAL"?"MAGE":card.CardClass);
   var ally=g.Summon(0,"FP1_002");ally.Race="MECHANICAL";
   var enemy=g.Summon(1,"VAN_CS2_182");
   string choice=card.BaseId=="LOE_115"?"minion":null;
   string mask=g.Rules.Target(card,choice,g,0);
   var target=mask=="NONE"?null:mask.StartsWith("FRIENDLY")?BT(ally):BT(enemy);
   BrmPlay(g,card.Id,target,choice);
   var decision=g.ObserveDecision(0);
   if(decision!=null)
    Check(g.SubmitDecision(0,decision.Id,decision.Options[0].Id).Success&&!g.HasPendingDecision,"Choose and finish "+card.Id);
  }
  ExplorerDecisions();
  ExplorerDeathsAndDecks();
  ExplorerTriggers();
 }

 static void LockedExplorers(){
  var pool=catalog.Pool(ExplorerRules.PoolId);
  var active=catalog.Pools.Single(p=>!p.Archived);
  var retained=catalog.Cards.Values.Where(c=>c.SetId==pool.Id).ToArray();
  Check(active.Id==TournamentClassRules.PoolId&&catalog.Deckable(active).Count==698,"Locked release keeps the Tournament pool active");
  Check(retained.Length==58&&retained.Count(c=>c.Collectible)==45&&retained.All(c=>!matchRules.Supports(c)),
   "Both collectible and generated Explorer cards are unavailable");
  var saved=matchRules.TrainingDeck("MAGE");
  saved.Cards.Remove(saved.Cards.Keys.First());
  saved.Cards.Add("LOE_002",2);
  saved.PoolId=pool.Id;
  string code=DeckCode.Encode(catalog,saved);
  var restored=DeckCode.Decode(catalog,code,pool.Id);
  Check(restored.Count==30&&restored.Cards["LOE_002"]==2,"Existing saved deck stays decodable");
  try{DeckCode.Decode(catalog,code,active.Id);Check(false,"An Explorer card imported into Tournament");}
  catch(System.IO.InvalidDataException){checks++;}
  try{DeckRules.ForCurrentPlay(catalog,restored);Check(false,"A locked deck reached the game lobby");}
  catch(System.IO.InvalidDataException ex){Check(ex.Message==ExplorerRules.LockedMessage,"Lobby reports the Explorer lock");}
  try{new MatchEngine(catalog,restored,restored,1,true);Check(false,"Local test accepted a locked deck");}
  catch(System.IO.InvalidDataException ex){Check(ex.Message==ExplorerRules.LockedMessage,"Authoritative engine rejects a locked deck");}
  var ordinary=DeckRules.ForCurrentPlay(catalog,matchRules.TrainingDeck("MAGE"));
  Check(DeckRules.CanAdd(catalog,ordinary,catalog.Card("LOE_011"))==ExplorerRules.LockedMessage,"Deck editor reports lock");
  Check(ArenaDraftSession.CurrentPool(catalog).Id==active.Id&&new SealedSession(catalog,3,matchRules.Supports).PoolId==active.Id,
   "Arena and sealed omit the locked pool");
  var game=BrmGame();
  Check(game.GenerationPool().Length==698&&game.GenerationPool().All(c=>c.SetId!=pool.Id),"Random generation cannot offer Explorer cards");
 }

 static void ExplorerDecisions(){
  var g=ExplorerGame("WARLOCK");
  g.Summon(0,"LOE_077");
  g.Summon(0,"LOE_077");
  BrmPlay(g,"LOE_023");
  var first=g.ObserveDecision(0);
  Check(first.Options.Length==3&&first.Options.Select(o=>o.Id).Distinct().Count()==3&&first.Options.All(o=>
   g.Card(o.Id).Cost==1&&(g.Card(o.Id).CardClass=="WARLOCK"||g.Card(o.Id).CardClass=="NEUTRAL")),
   "Launch Discover offers three distinct neutral or own-class cards");
  Check(g.ObserveDecision(1)==null&&!g.EndTurn().Success&&!g.SubmitDecision(1,first.Id,first.Options[0].Id).Success,
   "Decision owner and command lock are authoritative");
  var owner=LanProtocol.Copy(LanProtocol.View(g,0,"explorer",1));
  var opponent=LanProtocol.Copy(LanProtocol.View(g,1,"explorer",1));
  Check(owner.OwnDecision.Options.Length==3&&opponent.OwnDecision==null&&opponent.DecisionPending,
   "Wire discloses candidate options only to their owner");
  Check(LanProtocol.ReadView(catalog,owner).ObserveDecision(0).Options[0].Id==first.Options[0].Id&&
   LanProtocol.ReadView(catalog,opponent).ObserveDecision(0)==null,"Replica applies the private projection");
  var a=SimulationSnapshot.Capture(g).Fork();
  var b=SimulationSnapshot.Capture(g).Fork();
  Check(a.SubmitDecision(0,first.Id,first.Options[0].Id).Success&&
   b.SubmitDecision(0,first.Id,first.Options[0].Id).Success&&
   a.ObserveDecision(0).Options.Select(o=>o.Id).SequenceEqual(b.ObserveDecision(0).Options.Select(o=>o.Id)),
   "Paused snapshot reproduces second Discover");
  Check(g.SubmitDecision(0,first.Id,first.Options[0].Id).Success,"First battlecry resumes");
  var second=g.ObserveDecision(0);
  Check(second.Id!=first.Id&&!g.SubmitDecision(0,first.Id,first.Options[0].Id).Success&&
   g.SubmitDecision(0,second.Id,second.Options[0].Id).Success&&!g.HasPendingDecision&&g.Players[0].Hand.Count==2,
   "Two Branns still produce exactly two choices with unique decision IDs");

  g=ExplorerGame("MAGE");
  Check(g.HeroPower(0,new MatchTarget(1)).Success,"Used original hero power");
  BrmPlay(g,"LOE_076");
  var finley=g.ObserveDecision(0);
  Check(finley.Options.All(o=>o.Id!="tgt:basic-MAGE"&&o.Cost==2),"Finley excludes current basic power");
  Check(g.SubmitDecision(0,finley.Id,finley.Options[0].Id).Success&&!g.PowerSpent(0)&&
   g.Players[0].HeroAbilityId==finley.Options[0].Id&&g.Players[0].ClassId=="MAGE","Finley changes and refreshes power, not profession");
  Check(SimulationSnapshot.Capture(g).Fork().Observe(0).Self.HeroAbilityId==finley.Options[0].Id,
   "Replacement power survives an authoritative simulation fork");

  g=ExplorerGame();BrmPlay(g,"LOE_039");
  Check(!g.HasPendingDecision,"Gorillabot alone does not Discover");
  g=ExplorerGame("DRUID");BrmPlay(g,"LOE_115",choice:"spell");
  var idol=g.ObserveDecision(0);
  Check(idol.Options.All(o=>g.Card(o.Id).Type=="SPELL"),"Raven Idol Discover uses chosen card type");
  g=ExplorerGame();BrmPlay(g,"LOE_092");
  Check(g.ObserveDecision(0).Options.Select(o=>o.Id).SequenceEqual(new[]{"LOEA16_3","LOEA16_4","LOEA16_5"}),
   "Rafaam offers all three artifacts");
 }

 static void ExplorerDeathsAndDecks(){
  var g=ExplorerGame("ROGUE");
  var p=g.Players[0];
  var spider=g.Summon(0,"FP1_002");
  BrmPlay(g,"LOE_105",BT(spider));
  g.Summon(0,"LOE_077");
  BrmPlay(g,"LOE_019",BT(spider));
  var raptor=p.Board.Last();
  raptor.DamageTaken=raptor.MaxHealth;g.Cleanup();
  Check(p.Board.Count(u=>u.CardId=="FP1_002t")==4&&p.Hand.Count(h=>h.CardId=="LOE_105")==2,
   "Both copied native and attached deathrattles resolve twice with Brann");
  g=ExplorerGame("ROGUE");spider=g.Summon(0,"GVG_096");
  BrmPlay(g,"LOE_019",BT(spider));
  raptor=g.Players[0].Board.Last();raptor.DamageTaken=raptor.MaxHealth;g.Cleanup();
  Check(g.Players[0].Board.Any(u=>u!=spider&&g.Card(u.CardId).Cost==2),"Copied GvG death dispatches by effect identity");
  g=ExplorerGame("ROGUE");spider=g.Summon(0,"LOE_012");
  BrmPlay(g,"LOE_019",BT(spider));
  raptor=g.Players[0].Board.Last();g.Silence(BT(raptor));raptor.DamageTaken=raptor.MaxHealth;g.Cleanup();
  Check(g.Players[0].Hand.Count==0,"Silence removes copied deathrattle");

  g=ExplorerGame("PRIEST");p=g.Players[0];p.Deck.Clear();
  var enemy=g.Summon(1,"GVG_021");
  BrmPlay(g,"LOE_104",BT(enemy));
  Check(g.Graveyard.All(e=>e.InstanceId!=enemy.Id)&&p.Deck.Single()=="GVG_021","Entomb shuffles the minion without killing it");
  g.Draw(0,1);
  p.Mana=10;
  Check(p.Hand.Single().Generated&&g.Play(0,p.Hand.Single().Id).Success,"Entombed foreign class card can be played");

  g=ExplorerGame("PALADIN");
  for(int seat=0;seat<2;seat++)for(int i=0;i<2;i++){
   var fish=g.Summon(seat,"LOEA10_3");fish.DamageTaken=fish.MaxHealth;g.Cleanup();
  }
  BrmPlay(g,"LOE_026");
  Check(g.Players[0].Board.Count(u=>u.CardId=="LOEA10_3")==4,"Anyfin resurrects separate death entries across both teams");
  g=ExplorerGame("HUNTER");
  p=g.Players[0];p.Deck.Clear();p.Deck.Add("LOEA10_3");p.Deck.Add("VAN_EX1_029");
  g.Players[1].Deck.Add("LOE_077");
  BrmPlay(g,"LOE_020");
  Check(p.Board.Any(u=>u.CardId=="VAN_EX1_029")&&p.Deck.Single()=="LOEA10_3"&&g.Players[1].Board.Count==0,
   "Camel selects only one-cost minions and leaves zero-cost minions in deck");

  g=ExplorerGame();p=g.Players[0];p.Deck.Clear();
  p.Deck.Add("LOE_110t");p.Deck.Add("VAN_CS2_182");
  g.Draw(0,1);
  Check(p.Health==23&&p.Hand.Single().CardId=="VAN_CS2_182","Ancient Curse applies damage and replaces its draw");
  g=ExplorerGame();p=g.Players[0];p.Deck.Clear();
  p.Deck.Add("LOE_110t");p.Deck.Add("VAN_CS2_182");
  for(int i=0;i<g.Mode.HandLimit;i++)GvgHand(g,"GAME_COIN");
  g.Draw(0,1);
  Check(p.Health==30&&p.Deck.Single()=="VAN_CS2_182","Overdrawing Ancient Curse burns it without damage or a replacement draw");
  g=ExplorerGame();p=g.Players[0];p.Deck.Clear();
  BrmPlay(g,"LOE_079");g.Draw(0,1);
  Check(p.Hand.Single().CardId=="LOE_019t","Elise shuffles the map");
  Check(g.Play(0,p.Hand.Single().Id).Success&&p.Hand.Single().CardId=="LOE_019t2","Map shuffles and draws the Monkey");
  p.Deck.Add("VAN_CS2_182");GvgHand(g,"VAN_CS2_029");p.Mana=10;
  Check(g.Play(0,p.Hand.First().Id).Success&&p.Hand.Count==1&&p.Deck.Count==1&&
   p.Hand.Concat(p.Deck.Select(card=>new HandCard{CardId=card})).All(h=>g.Card(h.CardId).Rarity=="LEGENDARY"),
   "Golden Monkey replaces both zones without changing their counts");
 }

 static void ExplorerTriggers(){
  var g=ExplorerGame("SHAMAN");var p=g.Players[0];
  var trogg=g.Summon(0,"LOE_018");BrmPlay(g,"VAN_EX1_241",new MatchTarget(1));
  Check(p.Overload==2&&g.AttackValue(trogg)==3,"Trogg gains attack when Overload is queued");
  g=ExplorerGame();p=g.Players[0];p.Health=11;
  p.Deck.Clear();p.Deck.Add("VAN_CS2_182");p.Deck.Add("VAN_CS2_182");
  BrmPlay(g,"LOE_011");Check(p.Health==11,"Reno rejects duplicate cards in remaining deck");
  p.Deck.RemoveAt(0);p.HeroMaxHealth=40;
  BrmPlay(g,"LOE_011");Check(p.Health==40,"Reno heals to hero's current maximum when eligible");
  g=ExplorerGame("PRIEST");p=g.Players[0];p.Health=10;p.Deck.Clear();
  g.Summon(0,"VAN_EX1_591");BrmPlay(g,"LOE_011");
  Check(g.Winner==1&&p.Health==-20,"Reno and Auchenai apply full maximum health as damage");

  g=ExplorerGame("WARLOCK");
  BrmPlay(g,"LOE_007");
  g.EndTurn();
  var cursed=g.Players[1].Hand.Single(h=>h.CardId=="LOE_007t");
  Check(g.Players[1].Health==28&&g.Play(1,cursed.Id).Success,"Rafaam's Curse damages the holder before the holder can spend two mana to remove it");
  g.EndTurn();g.EndTurn();
  Check(g.Players[1].Health==28,"Casting Cursed removes the next turn's penalty");

  g=ExplorerGame("HUNTER");
  BrmPlay(g,"LOE_021");g.EndTurn();
  g.Players[1].ClassId="ROGUE";
  g.Players[1].Mana=10;int enemyHealth=g.Players[1].Health;
  Check(g.HeroPower(1).Success&&g.Players[1].Health==enemyHealth-5&&g.Players[0].SecretCount==0,
   "Dart Trap triggers after opposing hero power");
  g=ExplorerGame("PALADIN");BrmPlay(g,"LOE_027");g.EndTurn();g.Players[1].Mana=10;
  g.Summon(1,"VAN_CS2_182");g.Summon(1,"VAN_CS2_182");g.Summon(1,"VAN_CS2_182");
  Check(g.Play(1,GvgHand(g,"GVG_110",1)).Success&&g.Players[1].Board.Count==5&&
   g.Players[1].Board.All(u=>u.CardId!="GVG_110")&&g.Players[0].SecretCount==0,"Trial destroys the played minion after its battlecry");

  g=ExplorerGame("MAGE");p=g.Players[0];
  g.Summon(0,"LOE_051");
  Check(g.SpellPower(0)==2&&g.SpellPower(1)==2,"Moonkin spell damage applies to both sides");
  g.Summon(0,"LOE_119");g.Equip(0,"LOE_118");p.Armor=1;
  Check(g.Damage(new MatchTarget(0),9)==2&&p.Armor==0&&p.Health==29,"Armor caps before Cursed Blade doubles");

  g=ExplorerGame("DRUID");
  var ally=g.Summon(0,"VAN_CS2_182");var djinni=g.Summon(0,"LOE_053");
  BrmPlay(g,"LOE_105",BT(ally));
  Check(ally.BuffHealth==1&&djinni.BuffHealth==1&&g.CardsPlayed==1,"Djinni copies a spell effect without playing a second card");
  g=ExplorerGame();g.Summon(0,"LOE_086");BrmPlay(g,"LOE_002",new MatchTarget(1));
  Check(g.Players[0].Board.Any(u=>g.Card(u.CardId).Cost==3)&&g.Players[0].Deck.Contains("LOE_002t"),
   "Summoning Stone reads the resolved spell's cost");
  g=ExplorerGame();p=g.Players[0];p.Health=10;g.Summon(0,"LOE_038");
  Check(g.CardCost(0,g.Card("VAN_EX1_620"))==5&&g.CardCost(0,g.Card("GAME_COIN"))==5,"Launch Naga sets both Giants and spells to five");
  var giantId=GvgHand(g,"VAN_EX1_620");
  g.ShiftHandCosts(0,-1);
  Check(g.HandCost(0,p.Hand.Single())==4&&SimulationSnapshot.Capture(g).Fork().Observe(0).Hand.Single(h=>h.Id==giantId).Cost==4,
   "Subsequent Emperor discounts survive the fixed cost and simulation copy");
  g.Summon(0,"AT_045");
  Check(g.CardCost(0,g.Card("VAN_CS2_182"))==1&&g.CardCost(0,g.Card("GAME_COIN"))==5,"Later Aviana takes priority for minions");
 }
}
