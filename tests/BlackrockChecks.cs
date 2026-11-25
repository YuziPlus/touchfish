using System;
using System.IO;
using System.Linq;
using Touchfish;
public static partial class RuleTests {
 static MatchTarget BT(BattleUnit u){return new MatchTarget(u.Owner,u.Id);}
 static MatchEngine BrmGame(string profession="MAGE"){var g=GvgGame();g.Players[0].ClassId=profession;foreach(var p in g.Players){p.Deck.Clear();p.Deck.AddRange(Enumerable.Repeat("VAN_CS2_182",12));}return g;}
 static void BrmPlay(MatchEngine g,string card,MatchTarget target=null,string choice=null){g.Players[0].Mana=10;var result=g.Play(0,GvgHand(g,card),target,choice:choice);Check(result.Success,card+": "+result.Message);}
 static void Blackrock(){BrmLibrary();BrmBalance();BrmSpells();BrmBattlecries();BrmTriggers();BrmCopies();BrmEdges();BrmNetwork();}
 static void BrmLibrary(){
  var pool=catalog.Pool(BlackrockRules.SetId);var cards=catalog.Deckable(pool).Where(c=>c.SetId==BlackrockRules.SetId).ToArray();
  Check(cards.Length==31&&cards.All(matchRules.Supports),"All 31 Blackrock collectibles supported");
  Check(catalog.Deckable(pool).Count==566&&catalog.Deckable(catalog.Pool(GvgRules.SetId)).Count==535&&catalog.Deckable(catalog.Pool("classic-2014")).Count==382&&catalog.Deckable(catalog.Pool("naxxramas-2014")).Count==412,"Frozen pool sizes preserved");
  Check(cards.Count(c=>c.CardClass=="NEUTRAL")==13&&cards.Where(c=>c.CardClass!="NEUTRAL").GroupBy(c=>c.CardClass).All(g=>g.Count()==2),"Thirteen neutral plus two per profession");
  Check(catalog.Card("BRM_028").Cost==6&&catalog.Card("BRM_028").Attack==5&&catalog.Card("BRM_008").Cost==5&&catalog.Card("BRM_016").Health==5,"Original 2015 stats");
  Check(!matchRules.Supports(new CardRecord{Id="BRM_future",EffectId="BRM_future",SetId=BlackrockRules.SetId,Type="MINION"}),"Future effects cannot silently become supported");
  var current=catalog.Pools.Single(p=>!p.Archived);
  var old=matchRules.TrainingDeck("ROGUE");var code=DeckCode.Encode(catalog,old);var promoted=DeckRules.ForCurrentPlay(catalog,old);Check(promoted.PoolId==current.Id&&old.PoolId=="classic-2014"&&DeckCode.Encode(catalog,old)==code,"Old decks promoted without rewriting originals");
  Check(DeckCode.Decode(catalog,DeckCode.Encode(catalog,promoted),promoted.PoolId).Count==30,"Deckstring roundtrip");
  Check(ArenaDraftSession.CurrentPool(catalog).Id==current.Id,"Arena uses active full pool");var sealedGame=new SealedSession(catalog,42,matchRules.Supports);Check(sealedGame.PoolId==current.Id,"Sealed uses active full pool");
 }
 static void BrmBalance(){
  var pool=catalog.Pool(BlackrockRules.CurrentPoolId);
  var deckable=catalog.Deckable(pool);
  Check(deckable.Count==566&&deckable.All(matchRules.Supports),"Revised pool has 566 supported cards");
  foreach(var entry in pool.CardReplacements){
   var original=catalog.Card(entry.Key);
   Check(catalog.ResolveDbf(pool,original.DbfId).Id==entry.Value,"Original deckstring resolves to "+entry.Value);
  }
  var old=new DeckDocument{PoolId=BlackrockRules.SetId,ClassId="SHAMAN"};
  old.Cards.Add("FP1_025",2);
  var promoted=DeckRules.ForCurrentPlay(catalog,old);
  Check(promoted.Cards.ContainsKey("BRM9554_FP1_025")&&old.Cards.ContainsKey("FP1_025"),"Saved Blackrock 1.0.0 deck upgrades without mutation");
  var g=BrmGame();
  var undertaker=g.Summon(0,"BRM9554_FP1_028");
  g.Summon(0,"FP1_007");
  Check(undertaker.BuffAttack==1&&undertaker.BuffHealth==0,"Revised Undertaker gains attack only");
  g=BrmGame("WARLOCK");
  var victim=g.Summon(1,"VAN_CS2_182");
  BrmPlay(g,"BRM9554_EX1_320",BT(victim));
  Check(victim.Health==3&&g.Players[0].Board.Count==0,"Revised Bane deals two and does not summon without killing");
  bool expandedPoolObserved=false;
  for(int seed=0;seed<24;seed++){
   g=GvgGame(seed);
   g.CardPoolId=pool.Id;
   g.Players[0].ClassId="WARLOCK";
   victim=g.Summon(1,"VAN_CS2_231");
   BrmPlay(g,"BRM9554_EX1_320",BT(victim));
   var demon=g.Card(g.Players[0].Board.Single().CardId);
   Check(demon.Race=="DEMON"&&demon.Collectible&&pool.Contains(demon),"Bane summons an eligible collectible demon");
   expandedPoolObserved|=!WarlockRules.BaneDemons.Contains(demon.Id);
  }
  Check(expandedPoolObserved,"Bane can summon beyond original six demons");
 }
 static void BrmSpells(){
  var g=BrmGame("PALADIN");var a=g.Summon(0,"VAN_CS2_231");var b=g.Summon(1,"VAN_CS2_231");g.DamageBatch(()=>{g.Damage(BT(a),1);g.Damage(BT(b),1);});g.Cleanup();
  Check(g.DeathsThisTurn==2&&g.CardCost(0,g.Card("BRM_001"))==3&&g.CardCost(0,g.Card("BRM_003"))==3&&g.CardCost(0,g.Card("BRM_009"))==7&&g.CardCost(0,g.Card("BRM_025"))==4,"Both sides' deaths discount all four cards");
  BrmPlay(g,"BRM_001");Check(g.Players[0].Hand.Count==2,"Vigil draws two");g.EndTurn();Check(g.DeathsThisTurn==0&&g.CardCost(0,g.Card("BRM_001"))==5,"Death discount expires next turn");
  g=BrmGame();BrmPlay(g,"BRM_003",new MatchTarget(1));Check(g.Players[1].Health==26,"Dragon Breath four damage");
  g=BrmGame("WARLOCK");a=g.Summon(0,"BRM_006t");b=g.Summon(1,"VAN_CS2_182");BrmPlay(g,"BRM_005");Check(a.Health==1&&b.DamageTaken==2,"Demonwrath spares demons both sides");
  g=BrmGame("SHAMAN");g.Players[0].LockedMana=3;g.Players[0].Overload=4;g.Players[0].Mana=5;var hand=GvgHand(g,"BRM_011");Check(g.Play(0,hand,new MatchTarget(1)).Success&&g.Players[0].Mana==6&&g.Players[0].LockedMana==0&&g.Players[0].Overload==0,"Lava Shock releases locked and pending overload after paying two");
  g=BrmGame("HUNTER");BrmPlay(g,"BRM_013",new MatchTarget(1));Check(g.Players[0].Hand.Count==1&&g.Players[1].Health==27,"Quick Shot draws on empty hand");GvgHand(g,"VAN_CS2_182");int count=g.Players[0].Hand.Count;BrmPlay(g,"BRM_013",new MatchTarget(1));Check(g.Players[0].Hand.Count==count,"Quick Shot not empty does not draw");
  g=BrmGame("WARRIOR");a=g.Summon(0,"VAN_CS2_182");b=g.Summon(1,"VAN_CS2_182");g.Players[0].Health=12;BrmPlay(g,"BRM_015");Check(a.DamageTaken==3&&b.DamageTaken==3&&g.Players[0].Health==12,"Revenge low-health deals three only to minions");
  g=BrmGame("PRIEST");BrmPlay(g,"BRM_017");Check(!g.Players[0].Board.Any(),"Resurrection with empty graveyard resolves without summon");a=g.Summon(0,"GVG_110t");g.Buff(BT(a),4,4);GvgKill(g,a);BrmPlay(g,"BRM_017");Check(g.Players[0].Board.Single().CardId=="GVG_110t"&&g.Players[0].Board.Single().Health==1,"Resurrection produces base token without old buffs");
 }
 static void BrmBattlecries(){
  var g=BrmGame("PRIEST");BrmPlay(g,"BRM_004");Check(g.Players[0].Board.Single().MaxHealth==1,"Whelp without dragon stays 2/1");GvgHand(g,"BRM_025");BrmPlay(g,"BRM_004");Check(g.Players[0].Board.Last().MaxHealth==3,"Whelp with dragon gains two health");
  g=BrmGame("DRUID");BrmPlay(g,"BRM_010",choice:"attack");Check(g.Players[0].Board.Single().CardId=="BRM_010t"&&g.Players[0].Board.Single().BaseAttack==5,"Druid attack form");g.Players[0].Board.Clear();BrmPlay(g,"BRM_010",choice:"health");Check(g.Players[0].Board.Single().MaxHealth==5,"Druid health form");
  g=BrmGame("ROGUE");var a=g.Summon(1,"VAN_CS2_182");var b=g.Summon(1,"VAN_CS2_182");g.Damage(BT(b),1);BrmPlay(g,"BRM_008");Check(a.DamageTaken==2&&b.DamageTaken==1,"Skulker hits only undamaged enemies without spell power");
  g=BrmGame("SHAMAN");BrmPlay(g,"BRM_012");Check(g.AttackValue(g.Players[0].Board.Single())>=4&&g.AttackValue(g.Players[0].Board.Single())<=7&&g.Players[0].Overload==1,"Fireguard gains one through four attack and queues overload");
  g=BrmGame("HUNTER");BrmPlay(g,"BRM_014");Check(g.AttackValue(g.Players[0].Board.Single())==7&&g.Players[0].Board.Single().Health==7,"Core Rager empty-hand buff");
  g=BrmGame("PALADIN");BrmPlay(g,"BRM_018");g.EndTurn();g.EndTurn();int dragon=GvgHand(g,"BRM_025");Check(g.HandCost(0,g.Players[0].Hand.Single(h=>h.Id==dragon))==4,"Consort discount survives turns");BrmPlay(g,"BRM_018");Check(g.Players[0].NextDragonDiscount==2,"Second dragon Consort consumes old discount and grants a fresh two");g.Players[0].Mana=10;Check(g.Play(0,dragon).Success&&g.Players[0].NextDragonDiscount==0,"Playing a dragon consumes whole discount");
  g=BrmGame();g.Players[1].Health=15;BrmPlay(g,"BRM_024");Check(g.AttackValue(g.Players[0].Board.Single())==9,"Crusher low enemy health gains three three");
  g=BrmGame();BrmPlay(g,"BRM_026");Check(g.Players[1].Board.Count==1&&g.Card(g.Players[1].Board.Single().CardId).Cost==1&&g.Card(g.Players[1].Board.Single().CardId).Collectible,"Hungry Dragon summons supported collectible one-drop");
  g=BrmGame();var target=g.Summon(1,"GVG_110");BrmPlay(g,"BRM_029");Check(g.Players[1].Board.Contains(target),"Rend without dragon needs no target and has no battlecry");GvgHand(g,"BRM_025");int rend=GvgHand(g,"BRM_029");g.Players[0].Mana=10;var nonlegend=g.Summon(1,"VAN_CS2_182");Check(!g.Play(0,rend,BT(nonlegend)).Success&&g.Players[0].Mana==10,"Rend illegal target preserves mana and hand");Check(g.Play(0,rend,BT(target)).Success&&!g.Players[1].Board.Contains(target),"Rend holding dragon destroys legendary");
  g=BrmGame();g.Players[1].ClassId="ROGUE";BrmPlay(g,"BRM_030");Check(g.Players[0].Hand.Count==2&&g.Players[0].Hand.All(h=>g.Card(h.CardId).Type=="SPELL"&&g.Card(h.CardId).CardClass=="ROGUE"),"Nefarian generates two opponent-class spells from match pool");
  g=BrmGame();GvgHand(g,"BRM_025");BrmPlay(g,"BRM_033");Check(g.AttackValue(g.Players[0].Board.Single())==3&&g.Players[0].Board.Single().MaxHealth==5,"Technician dragon buff");BrmPlay(g,"BRM_034",new MatchTarget(1));Check(g.Players[1].Health==27,"Corruptor original battlecry three");
 }
 static void BrmTriggers(){
  var g=BrmGame();var u=g.Summon(0,"BRM_019");g.Summon(0,"VAN_EX1_084");g.Damage(BT(u),1);Check(g.Players[0].Board.Count(x=>x.CardId=="BRM_019")==2&&g.Players[0].Board[1].CardId=="BRM_019"&&g.Players[0].Board[1].Charge,"Patron surviving damage produces fresh charging Patron with old Warsong");g.Damage(BT(u),2);g.Cleanup();Check(g.Players[0].Board.Count(x=>x.CardId=="BRM_019")==1,"Lethal damage never duplicates Patron");
  foreach(var pair in new[]{new[]{"BRM_006","BRM_006t"},new[]{"BRM_022","BRM_022t"}}){g=BrmGame();u=g.Summon(0,pair[0]);g.Damage(BT(u),20);g.Cleanup();Check(g.Players[0].Board.Any(x=>x.CardId==pair[1]),"Lethal hit still triggers "+pair[0]);}
  g=BrmGame();u=g.Summon(0,"BRM_016");g.Damage(BT(u),20);g.Cleanup();Check(g.Players[1].Health==28,"Lethal damage still triggers Axe Flinger");
  g=BrmGame();u=g.Summon(0,"BRM_019");u.Shield=true;g.Damage(BT(u),1);Check(g.Players[0].Board.Count==1,"Divine Shield is not damage");g.Silence(BT(u));g.Damage(BT(u),1);Check(g.Players[0].Board.Count==1,"Silence disables Patron");
  g=BrmGame();g.Summon(0,"BRM_002");BrmPlay(g,"GAME_COIN");Check(g.Players[1].Health==28,"Flamewaker shoots twice after successful spell");g.ArmSecret(1,new HandCard{Id=id++,CardId="VAN_EX1_287"});BrmPlay(g,"VAN_CS2_029",new MatchTarget(1));Check(g.Players[1].Health==28,"Countered spell does not trigger Flamewaker");
  g=BrmGame();u=g.Summon(0,"BRM_020");BrmPlay(g,"VAN_CS2_029",BT(u));Check(g.Players[0].Board.Count==0,"Dragonkin buffs before fireball but still dies at six health");
  g=BrmGame();u=g.Summon(0,"BRM_020");g.ArmSecret(1,new HandCard{Id=id++,CardId="VAN_EX1_287"});BrmPlay(g,"VAN_CS2_029",BT(u));Check(u.MaxHealth==6&&g.AttackValue(u)==4&&u.DamageTaken==0,"Dragonkin triggers even when targeted spell is countered");
  g=BrmGame();u=g.Summon(0,"BRM_028");int fire=GvgHand(g,"VAN_CS2_029");g.EndTurn();Check(g.Players[0].Hand.Single(h=>h.Id==fire).CostAdjustment==-1,"Emperor reduces existing hand on own end");int late=GvgHand(g,"VAN_CS2_029");g.EndTurn();Check(g.Players[0].Hand.Single(h=>h.Id==late).CostAdjustment==0,"Opponent end and later cards not discounted");g.EndTurn();Check(g.Players[0].Hand.Single(h=>h.Id==fire).CostAdjustment==-2,"Emperor reductions stack over turns");
  g=BrmGame();u=g.Summon(0,"BRM_027");GvgKill(g,u);Check(g.Players[0].Health==8&&g.Players[0].HeroMaxHealth==8&&g.Players[0].HeroFormId==BlackrockRules.Ragnaros,"Majordomo replaces hero on death");g.Players[0].Mana=10;Check(g.HeroPower(0).Success&&g.Players[1].Health==22,"Ragnaros power deals random eight without target");
 }
 static void BrmCopies(){
  var g=BrmGame("ROGUE");g.Players[0].Deck.Clear();var bomb=g.Summon(0,"GVG_110t");g.Buff(BT(bomb),5,5);BrmPlay(g,"BRM_007",BT(bomb));Check(g.Players[0].Deck.Count==3&&g.Players[0].Deck.All(x=>x=="GVG_110t"),"Gang Up shuffles three copies of uncollectible bomb");g.Draw(0,1);Check(g.Players[0].Hand.Single().CostAdjustment==0&&g.HandCost(0,g.Players[0].Hand.Single())==1,"Shuffled bomb costs original one");
  g=BrmGame("ROGUE");g.Players[0].Deck.Clear();var demon=g.Summon(1,"GVG_021");BrmPlay(g,"BRM_007",BT(demon));g.Draw(0,1);var card=g.Players[0].Hand.Single();Check(card.Generated&&g.CanUseCard(0,g.Card(card.CardId),card),"Opponent-class minion shuffled by Gang Up remains playable after drawing");g.Players[0].Mana=10;Check(g.Play(0,card.Id).Success,"Stolen Malganis actually plays");
  g=BrmGame();g.Summon(0,"BRM_031");g.Players[0].Deck.Clear();g.Players[0].Deck.Add("VAN_CS2_029");g.Draw(0,1);Check(g.Players[0].Hand.Count==2&&g.Players[0].Deck.Count==0,"Chromaggus copies draw without another deck draw");
  g=BrmGame();g.Summon(0,"BRM_031");g.Summon(0,"BRM_031");g.Draw(0,1);Check(g.Players[0].Hand.Count==3,"Multiple Chromaggus each copy once without recursive draw");
  g=BrmGame();g.Summon(0,"BRM_031");for(int n=0;n<9;n++)GvgHand(g,"VAN_CS2_182");g.Draw(0,1);Check(g.Players[0].Hand.Count==10,"Chromaggus obeys hand limit");g.Players[0].Hand.Clear();g.Players[0].Deck.Clear();g.Draw(0,1);Check(g.Players[0].Hand.Count==0&&g.Players[0].Fatigue==1,"Fatigue is not a drawn card");
  g=BrmGame();g.Summon(0,"BRM_031");g.Players[0].Deck.Clear();g.Players[0].Deck.Add("VAN_CS2_172");g.DrawMatchingCard(0,"BEAST");Check(g.Players[0].Hand.Count==2,"Tutored draw uses same copy hook");
 }
 static void BrmEdges(){
  var g=BrmGame("ROGUE");var bomb=g.Summon(0,"GVG_110t");g.Players[0].Deck.Clear();g.Players[0].Deck.AddRange(Enumerable.Repeat("VAN_CS2_182",59));BrmPlay(g,"BRM_007",BT(bomb));Check(g.Players[0].Deck.Count==60&&g.Players[0].Deck.Count(c=>c=="GVG_110t")==1,"2015 sixty-card deck cap truncates Gang Up safely");
  g=BrmGame("WARRIOR");g.Players[0].Health=12;g.Players[1].Health=100;for(int i=0;i<6;i++)g.Summon(0,"GVG_110t");var baron=g.Summon(0,"FP1_031");BrmPlay(g,"BRM_015");Check(g.Players[0].Board.Count==1&&baron.Health==4,"Revenge kills all six bombs while Baron survives");Check(g.VisualEvents.Count(e=>e.Kind=="damage"&&e.Seat==1&&e.UnitId==0)==12,"Six bombs and Baron deliver exactly twelve separate explosions");
  g=BrmGame();var u=g.Summon(0,"BRM_006");g.Silence(BT(u));g.Damage(BT(u),1);Check(g.Players[0].Board.Count==1,"Silenced Imp Gang Boss cannot summon");
  g=BrmGame();u=g.Summon(0,"BRM_019");for(int i=0;i<6;i++)g.Summon(0,"BRM_019");g.DamageBatch(()=>{foreach(var t in g.Characters(0,false).ToArray())g.Damage(t,1);});g.Cleanup();Check(g.Players[0].Board.Count==7,"Simultaneous Patron damage respects seven slots");
  g=BrmGame();u=g.Summon(0,"BRM_027");g.Silence(BT(u));GvgKill(g,u);Check(g.Players[0].HeroFormId==null,"Silenced Majordomo does not replace hero");
  g=BrmGame("PRIEST");u=g.Summon(0,"GVG_110");GvgKill(g,u);BrmPlay(g,"BRM_017");Check(g.Players[0].Board.Count==1&&g.Players[0].Board[0].CardId=="GVG_110","Resurrecting Boom does not repeat battlecry");
  g=BrmGame();u=g.Summon(0,"BRM_031");g.Players[0].Deck.Clear();g.Players[0].Deck.AddRange(Enumerable.Repeat("VAN_CS2_182",12));for(int i=0;i<10;i++)GvgHand(g,"VAN_CS2_182");g.Draw(0,1);Check(g.Players[0].Hand.Count==10&&g.Graveyard.Count(e=>e.Reason==GraveyardReason.Burned)==1,"Burned original card does not trigger Chromaggus copying");
  g=BrmGame("WARLOCK");g.Summon(0,"BRM_031");g.Players[0].Deck.Clear();g.Players[0].Deck.Add("BRM_006");g.SenseDemons(0);Check(g.Players[0].Hand.Count(h=>h.CardId=="BRM_006")==2&&g.Players[0].Hand.Count(h=>h.CardId=="GAME_WORTHLESS_IMP")==1,"Sense Demons copies actual draw but not generated fallback imp");
  g=BrmGame("HUNTER");g.Summon(0,"BRM_031");g.Players[0].Deck.Clear();g.Players[0].Deck.AddRange(new[]{"VAN_CS2_182","VAN_CS2_029","VAN_CS2_172"});int tracking=GvgHand(g,"VAN_DS1_184");var option=g.Rules.Choices(g.Card("VAN_DS1_184"),g,0)[1];Check(g.Play(0,tracking,choice:option.Key).Success&&g.Players[0].Hand.Count(h=>h.CardId=="VAN_CS2_029")==2,"Tracking chosen draw copies once; discarded candidates never copy");
 }
 static void BrmNetwork(){
  var g=BrmGame("PALADIN");BrmPlay(g,"BRM_018");var dead=g.Summon(1,"VAN_CS2_231");GvgKill(g,dead);int h=GvgHand(g,"BRM_025");var owner=LanProtocol.ReadView(catalog,LanProtocol.Copy(LanProtocol.View(g,0,"brm",1)));var enemy=LanProtocol.ReadView(catalog,LanProtocol.Copy(LanProtocol.View(g,1,"brm",1)));
  Check(owner.DeathsThisTurn==1&&owner.Players[0].NextDragonDiscount==2&&owner.HandCost(0,owner.Players[0].Hand.Single(x=>x.Id==h))==g.HandCost(0,g.Players[0].Hand.Single(x=>x.Id==h)),"Cost counters and owner hand agree after serialization");Check(enemy.Players[0].Hand.Count==0&&enemy.Players[0].Deck.All(x=>x=="HIDDEN"),"Opponent gets no hand identity or deck order");
  var doom=g.Summon(0,"BRM_027");GvgKill(g,doom);owner=LanProtocol.ReadView(catalog,LanProtocol.Copy(LanProtocol.View(g,0,"brm",2)));Check(owner.Players[0].HeroFormId==BlackrockRules.Ragnaros&&owner.HeroPowerCost(0)==2,"Replacement hero and registered power survive snapshot");
 }
}
