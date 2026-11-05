using System;using System.Linq;using Touchfish;
public static partial class RuleTests {
 static void WriteCurrentCoverage(string folder){var cards=catalog.Deckable(catalog.Pools.First(p=>!p.Archived));System.IO.Directory.CreateDirectory(System.IO.Path.Combine(folder,"previews"));JsonData.Write(System.IO.Path.Combine(folder,"previews","match-rule-coverage.json"),new{SupportedCards=cards.Count(matchRules.Supports),TotalCards=cards.Count,GvgClassSupported=cards.Count(c=>c.SetId==GvgClassRules.SetId&&matchRules.Supports(c)),GvgNeutralSupported=cards.Count(c=>c.SetId=="goblins-gnomes-2014"&&matchRules.Supports(c)),Cards=cards.Select(c=>new{c.Id,c.Name,Supported=matchRules.Supports(c)}).ToArray(),Scope="Current active multiplayer pool; 123 GvG cards supported"});}
 static int GH(MatchEngine g,string key,int seat=0){return NaxxHand(g,key,seat);}
 static MatchEngine GG(string cl="MAGE"){var g=MatchFixture(cl,"MAGE");foreach(var p in g.Players){p.Deck.Clear();p.Deck.AddRange(Enumerable.Repeat("VAN_CS2_182",20));}g.CardPoolId=GvgClassRules.PoolId;return g;}
 static MatchTarget GT(BattleUnit u){return new MatchTarget(u.Owner,u.Id);}
 static bool GP(MatchEngine g,string key,MatchTarget t=null,string choice=null){return g.Play(0,GH(g,key),t,-1,choice).Success;}
 static void GvgSmoke(string[] classes){
  foreach(var c in catalog.BrowseDeckable(GvgClassRules.SetId).Where(c=>classes.Contains(c.CardClass))){
   var g=GG(c.CardClass);var mech=g.Summon(0,"GVG_058");g.Summon(0,"VAN_CS2_171");g.Summon(0,"VAN_CS2_101t");g.Summon(1,"VAN_CS2_182");
   var mask=g.Rules.Target(c,null,false);var target=mask=="NONE"?null:mask=="ENEMY_MINION"?GT(g.Players[1].Board[0]):GT(mech);var choices=g.Rules.Choices(c);
   int hand=GH(g,c.Id);var result=g.Play(0,hand,target,-1,choices.Length>0?choices[0].Key:null);
   Check(result.Success,"GvG real play succeeds: "+c.Id+" "+result.Message);
   Check(!g.Players[0].Hand.Any(h=>h.Id==hand)&&g.Players[0].Mana==10-c.Cost,"GvG play consumes exactly one card and its cost: "+c.Id);
   Check(g.Players.All(p=>p.Hand.Count<=10&&p.Board.Count<=7),"GvG respects hand/board limits: "+c.Id);
  }
 }
 static void GvgOne(){
  var all=catalog.BrowseDeckable(GvgClassRules.SetId);Check(all.Count==72&&all.All(matchRules.Supports),"All 72 GvG class cards supported");
  foreach(var cl in all.GroupBy(c=>c.CardClass))Check(cl.Count()==8,"Eight cards in "+cl.Key);
  Check(catalog.Deckable(catalog.Pool("classic-2014")).Count==382&&catalog.Deckable(catalog.Pool("naxxramas-2014")).Count==412&&catalog.Deckable(catalog.Pool(GvgClassRules.PoolId)).Count==535,"Old pools frozen; class-only GvG has 484 cards");
  Check(catalog.Cards.Values.Count(c=>c.SetId==GvgClassRules.SetId&&!c.Collectible)==4,"Four additional helper cards are uncollectible");
  Check(catalog.BrowseDeckable("goblins-gnomes-2014").Count==51&&catalog.BrowseDeckable("goblins-gnomes-2014").All(c=>matchRules.Supports(c)),"All 51 upstream neutral cards retain their implementations");
  Check(catalog.Deckable(catalog.Pool(GvgClassRules.PoolId)).Count(matchRules.Supports)==535,"All 535 current cards playable");
  GvgDraftFilters();
  var legacy=matchRules.TrainingDeck("MAGE");var promoted=DeckRules.ForCurrentPlay(catalog,legacy);Check(promoted.PoolId==GvgClassRules.PoolId&&legacy.PoolId=="classic-2014","Old decks promoted without saved-data mutation");
  GvgSmoke(new[]{"DRUID","MAGE","PALADIN"});
  var g=GG("DRUID");Check(GP(g,"GVG_030",choice:"health")&&g.Players[0].Board.Single().MaxHealth==3&&g.Players[0].Board.Single().Taunt,"Robo Cub health choice retains Taunt");
  g=GG("DRUID");Check(GP(g,"GVG_030",choice:"attack")&&g.AttackValue(g.Players[0].Board.Single())==3,"Robo Cub attack choice");
  g=GG("DRUID");g.Players[0].MaxMana=g.Players[0].Mana=5;g.Players[1].MaxMana=g.Players[1].Mana=3;Check(GP(g,"GVG_032",choice:"mana")&&g.Players[0].MaxMana==6&&g.Players[1].MaxMana==4,"Grove Tender gives both players full crystal");
  g=GG("DRUID");Check(GP(g,"GVG_032",choice:"draw")&&g.Players[0].Hand.Count==1&&g.Players[1].Hand.Count==1,"Grove Tender both draw");
  g=GG("DRUID");var b=g.Summon(1,"VAN_CS2_182");int deck=g.Players[1].Deck.Count;Check(GP(g,"GVG_031",GT(b))&&!g.Players[1].Board.Contains(b)&&g.Players[1].Deck.Count==deck+1&&!g.Graveyard.Any(e=>e.Reason==GraveyardReason.MinionDied),"Recycle shuffles without death");
  g=GG("DRUID");g.Players[0].Health=5;g.Players[1].Health=8;b=g.Summon(1,"VAN_CS2_182");b.DamageTaken=2;Check(GP(g,"GVG_033")&&g.Players.All(p=>p.Health==30)&&b.DamageTaken==0,"Tree of Life heals all characters");
  g=GG("DRUID");Check(GP(g,"GVG_041",choice:"wisps")&&g.Players[0].Board.Count==5,"Dark Wispers five Wisps");
  g=GG("DRUID");b=g.Summon(1,"VAN_CS2_182");Check(GP(g,"GVG_041",GT(b),"buff")&&b.Taunt&&b.MaxHealth==10&&g.AttackValue(b)==9,"Dark Wispers targeted +5/+5");
  g=GG("DRUID");g.Summon(0,"VAN_CS2_171");Check(GP(g,"GVG_080")&&g.Players[0].Board.Last().CardId=="GVG_080t"&&g.Players[0].Board.Last().Race=="BEAST","Druid of Fang transforms with Beast");
  g=GG("DRUID");b=g.Summon(0,"GVG_034");g.Damage(GT(b),1);Check(g.Players[0].Hand.Count==1&&GvgClassRules.Parts.Contains(g.Players[0].Hand[0].CardId),"Mech Bear Cat generates spare part");g.Silence(GT(b));g.Damage(GT(b),1);Check(g.Players[0].Hand.Count==1,"Silence suppresses spare parts");
  g=GG("DRUID");b=g.Summon(0,"GVG_035");NaxxKill(g,b);Check(g.Players[0].Deck.Contains("GVG_035"),"Malorne shuffles into owner deck");
  g=GG();b=g.Summon(1,"VAN_CS2_182");Check(GP(g,"GVG_001")&&b.Health==1,"Flamecannon deals four");
  g=GG();Check(GP(g,"GVG_003")&&g.Players[0].Hand.Single().CostAdjustment==-3&&g.Players[0].Hand.Single().Generated,"Portal grants random minion with instance discount");
  g=GG();g.Summon(0,"GVG_058");Check(GP(g,"GVG_004")&&g.Players[1].Health==26,"Blastmage four battlecry missiles");
  g=GG();b=g.Summon(0,"VAN_CS2_182");g.Buff(GT(b),4,4);Check(GP(g,"GVG_005")&&g.Players[0].Hand.Single().CardId==b.CardId&&g.Players[0].Hand.Single().AttackBonus==0,"Echo copies base cards, not battlefield buffs");
  g=GG();g.Players[0].Deck.Clear();g.Players[0].Deck.Add("GVG_007");g.Draw(0,1);Check(g.Players.All(p=>p.Health==28)&&g.Players[0].Hand.Single().CardId=="GVG_007","Flame Leviathan damages all when drawn");
  g=GG();g.Players[0].Deck.Clear();g.Players[0].Deck.Add("GVG_007");for(int i=0;i<10;i++)GH(g,"PART_001");g.Draw(0,1);Check(g.Players.All(p=>p.Health==30),"Burned Leviathan does not trigger draw effect");
  g=GG();b=g.Summon(0,"GVG_122");var adj=g.Summon(0,"VAN_CS2_182");Check(!g.Play(0,Hand(g,"CS2_029"),GT(adj)).Success,"Wee Spellstopper protects adjacent from spells");g.Silence(GT(b));Check(g.Play(0,Hand(g,"CS2_029"),GT(adj)).Success,"Silencing Spellstopper removes protection");
  g=GG();g.Summon(0,"GVG_123");Check(g.SpellPower(0)==1,"Soot Spewer spell damage");
  g=GG();b=g.Summon(0,"GVG_002");b.SummonTurn=0;Check(g.Attack(0,b.Id,new MatchTarget(1)).Success&&g.Players[1].Frozen,"Snowchugger freezes combat target");
  GvgPaladin();
 }
 static void GvgPaladin(){var g=GG("PALADIN");g.Players[0].Health=20;Check(GP(g,"GVG_057")&&g.Players[0].Health==24&&g.HeroAttack(0)==2,"Seal of Light heals and gives temporary attack");
  g=GG("PALADIN");Check(GP(g,"GVG_058")&&g.Players[0].Board.Single().Shield,"Shielded Minibot shield");
  g=GG("PALADIN");var b=g.Summon(0,"VAN_CS2_182");Check(GP(g,"GVG_059")&&b.Shield&&b.Taunt,"Coghammer shield and Taunt");
  g=GG("PALADIN");Check(GP(g,"GVG_061")&&g.Players[0].Board.Count==3&&g.Players[0].WeaponAttack==1&&g.Players[0].WeaponDurability==4,"Muster summons three and equips 1/4");g.Players[0].Mana=10;Check(GP(g,"GVG_060")&&g.Players[0].Board.Take(3).All(u=>u.MaxHealth==3&&g.AttackValue(u)==3),"Quartermaster buffs recruits only");
  g=GG("PALADIN");b=g.Summon(0,"GVG_062");g.Summon(0,"GVG_058");Check(b.Shield,"Cobalt Guardian gains shield on other Mech summon");
  g=GG("PALADIN");int hand=GH(g,"GVG_063");b=g.Summon(0,"VAN_CS2_171");NaxxKill(g,b);Check(g.Players[0].Hand.Single(h=>h.Id==hand).AttackBonus==1,"Bolvar grows only in hand");Check(g.Play(0,hand).Success&&g.AttackValue(g.Players[0].Board.Single())==2,"Bolvar retains hand attack on play");
  g=GG("PALADIN");b=g.Summon(1,"VAN_EX1_096");Check(GP(g,"GVG_101")&&g.Players[1].Board.Count==0&&g.Players[1].Hand.Count==1,"Scarlet Purifier damages native deathrattles and resolves them");
 }
 static void GvgDraftFilters(){
  bool appeared=false;for(int seed=0;seed<12;seed++){var arena=new ArenaDraftSession(catalog,seed,matchRules.Supports);var v=arena.View(0);arena.Pick(0,v.Session,v.Revision,0);for(int n=0;n<30;n++){v=arena.View(0);Check(v.Choices.All(id=>matchRules.Supports(catalog.Card(id))),"Arena never offers unimplemented GvG neutrals");appeared|=v.Choices.Any(id=>catalog.Card(id).SetId==GvgClassRules.SetId);arena.Pick(0,v.Session,v.Revision,0);}}
  Check(appeared,"Implemented GvG class cards actually enter Arena offers");
  var sealedSession=new SealedSession(catalog,1234,matchRules.Supports);var view=sealedSession.View(0);sealedSession.Act(0,view.Session,view.Revision,"class",view.Classes[0]);view=sealedSession.View(0);sealedSession.Act(0,view.Session,view.Revision,"open-all");Check(sealedSession.View(0).Pool.Keys.All(id=>matchRules.Supports(catalog.Card(id))),"Sealed packs never include unavailable neutrals");
  var unavailable=new DeckDocument{PoolId=GvgClassRules.PoolId,ClassId="MAGE"};unavailable.Cards["GVG_110"]=1;Check(new MatchEngine(catalog,unavailable,unavailable,42,true)!=null,"Implemented upstream Dr Boom can start a match");
 }
 static void GvgTwo(){GvgSmoke(new[]{"HUNTER","WARRIOR","WARLOCK"});GvgTwoEffects();}
 static void GvgMergeRegression(){
  var g=GG("SHAMAN");var mech=g.Summon(0,"GVG_058");g.Equip(0,"GVG_036");g.Equip(0,"VAN_CS2_106");Check(g.AttackValue(mech)==4&&mech.MaxHealth==4,"Replacing Powermace triggers its deathrattle exactly once");
  g=GG("WARLOCK");var watcher=g.Summon(0,"GVG_100");g.Players[0].Armor=3;g.Damage(new MatchTarget(0),2);Check(g.Players[0].Health==30&&g.Players[0].Armor==1&&g.AttackValue(watcher)==6&&watcher.MaxHealth==6,"Armor-only hero damage triggers Floating Watcher");
  g=GG("SHAMAN");int bolvar=GH(g,"GVG_063");g.Players[0].Hand.Single(h=>h.Id==bolvar).AttackBonus=3;Check(GP(g,"GVG_029")&&g.AttackValue(g.Players[0].Board.Single())==4,"Ancestor's Call preserves Bolvar hand attack enchantment");
  g=GG("PALADIN");var bot=g.Summon(0,"VAN_EX1_006");bolvar=GH(g,"GVG_063");g.Players[0].Hand.Single(h=>h.Id==bolvar).AttackBonus=3;g.SwapAlarmBot(bot);Check(g.AttackValue(g.Players[0].Board.Single())==4,"Alarm-o-Bot preserves hand attack enchantment without battlecry");
  g=GG("PALADIN");g.Summon(0,"GVG_104");bolvar=GH(g,"GVG_063");g.Players[0].Hand.Single(h=>h.Id==bolvar).AttackBonus=3;Check(g.Play(0,bolvar).Success&&g.AttackValue(g.Players[0].Board.Last())==4&&g.Players[0].Board.Last().MaxHealth==7,"Hobgoblin checks grown hand attack rather than Bolvar's printed one");
  g=GG("PALADIN");g.Summon(0,"VAN_EX1_084");bolvar=GH(g,"GVG_063");g.Players[0].Hand.Single(h=>h.Id==bolvar).AttackBonus=4;Check(g.Play(0,bolvar).Success&&!g.HasCharge(g.Players[0].Board.Last()),"Warsong sees hand growth before granting Charge");
  g=GG("ROGUE");var left=g.Summon(1,"VAN_CS2_182");var snow=g.Summon(1,"GVG_002");var right=g.Summon(1,"VAN_CS2_182");Check(GP(g,"VAN_EX1_126",GT(snow))&&left.Frozen&&right.Frozen,"Snowchugger also freezes damage dealt through Betrayal");
  g=GG("ROGUE");g.Players[1].Health=20;g.Summon(1,"VAN_CS2_182");var mistress=g.Summon(1,"GVG_018");g.Summon(1,"VAN_CS2_182");Check(GP(g,"VAN_EX1_126",GT(mistress))&&g.Players[1].Health==22,"Mistress heals owner for damage dealt through Betrayal");
  g=GG("HUNTER");g.Summon(0,"GVG_087");g.Players[0].Shadowform=1;Check(g.HeroPower(0,new MatchTarget(0)).Success&&g.Players[0].Health==28,"Sniper does not restrict a copied Shadowform hero power");
  g=GG("HUNTER");g.Summon(0,"GVG_087");g.Players[0].HeroAbilityId=HeroReplacementRules.InfernoId;Check(!g.PowerNeedsTarget(0)&&g.HeroPower(0).Success,"Sniper does not force a target after hero power replacement");
  g=GG();var demon=g.Summon(1,"GVG_021");Check(AgentBridge.Observe(g,0).Enemy.Immune,"AI public observation includes Malganis hero immunity");g.Silence(GT(demon));Check(!AgentBridge.Observe(g,0).Enemy.Immune,"AI observes immunity ending when Malganis is silenced");
  g=GG("WARRIOR");var acolyte=g.Summon(0,"VAN_EX1_007");g.Players[0].Deck.Clear();g.Players[0].Deck.AddRange(new[]{"GVG_007","VAN_CS2_182"});Check(g.GvgBladeDamage(acolyte,1),"Bouncing Blade stops when a draw-triggered Leviathan kills a minion during its hit");
  g=GG("PRIEST");int deckBefore=g.Players[0].Deck.Count;Check(GP(g,"GVG_056t")&&g.Players[0].Health==30&&g.Players[0].Deck.Count==deckBefore,"A copied mine can be played without falsely firing its draw-only trigger or throwing after payment");
  Check(!matchRules.Supports(new CardRecord{Id="GVG_REVIEW_UNKNOWN",BaseId="GVG_REVIEW_UNKNOWN",EffectId="GVG_REVIEW_UNKNOWN",SetId=GvgClassRules.SetId,Type="MINION"}),"Future unknown class card remains unavailable rather than consuming resources on default error");
 }
 static void GvgThree(){GvgMergeRegression();GvgSmoke(new[]{"PRIEST","ROGUE","SHAMAN"});GvgThreeEffects();}
 static void GvgTwoEffects(){
  var g=GG("HUNTER");g.Players[0].Deck.Clear();g.Players[0].Deck.Add("VAN_DS1_175");Check(GP(g,"GVG_017")&&g.HandCost(0,g.Players[0].Hand.Single())==0,"Call Pet discounts drawn Beast by four");
  g=GG("HUNTER");g.Players[0].Deck.Clear();g.Players[0].Deck.Add("VAN_CS2_182");Check(GP(g,"GVG_017")&&g.Players[0].Hand.Single().CostAdjustment==0,"Call Pet does not discount non-Beast");
  g=GG("HUNTER");var b=g.Summon(0,"VAN_EX1_096");Check(GP(g,"GVG_026")&&g.Players[0].Board.Contains(b)&&g.Players[0].Hand.Count==1&&!g.Graveyard.Any(e=>e.Reason==GraveyardReason.MinionDied),"Feign Death triggers draw without killing source");
  g=GG("HUNTER");g.Summon(0,"FP1_031");b=g.Summon(0,"VAN_EX1_096");Check(GP(g,"GVG_026")&&g.Players[0].Hand.Count==2,"Feign Death honors Baron doubled deathrattles");
  g=GG("HUNTER");b=g.Summon(0,"VAN_EX1_096");g.Silence(GT(b));Check(GP(g,"GVG_026")&&g.Players[0].Hand.Count==0,"Feign Death respects native silence");
  g=GG("HUNTER");b=g.Summon(0,"VAN_CS2_171");Check(GP(g,"GVG_043")&&g.AttackValue(b)==2&&g.Players[0].WeaponAttack==2&&g.Players[0].WeaponDurability==2,"Glaivezooka equips and buffs random friendly attack");
  g=GG("HUNTER");g.Summon(0,"VAN_CS2_171");Check(GP(g,"GVG_046")&&g.AttackValue(g.Players[0].Board.Last())==3&&g.Players[0].Board.Last().Taunt,"King of Beasts counts other friendly Beasts");
  g=GG("HUNTER");b=g.Summon(0,"GVG_058");Check(GP(g,"GVG_048")&&g.AttackValue(b)==4&&g.AttackValue(g.Players[0].Board.Last())==3,"Metaltooth buffs other Mechs only");
  g=GG("HUNTER");b=g.Summon(0,"GVG_049");g.Damage(GT(b),1);g.Damage(GT(b),1);Check(g.AttackValue(b)==24,"Gahzrilla doubles on each damage instance");g.Silence(GT(b));g.Damage(GT(b),1);Check(g.AttackValue(b)==6,"Silence removes Gahzrilla growth and trigger");
  g=GG("HUNTER");b=g.Summon(1,"VAN_CS2_182");Check(GP(g,"GVG_073",GT(b))&&b.Health==2&&g.Players[1].Health==27,"Cobra Shot hits selected minion and enemy hero");
  g=GG("HUNTER");g.Summon(0,"GVG_087");b=g.Summon(0,"VAN_CS2_182");Check(g.HeroPower(0,GT(b)).Success&&b.Health==3,"Steamwheedle permits friendly minion target");
  g=GG("HUNTER");g.Summon(0,"GVG_087");Check(!g.HeroPower(0,new MatchTarget(0)).Success&&g.Players[0].Mana==10,"Steamwheedle still forbids own hero");
  g=GG("HUNTER");b=g.Summon(0,"VAN_CS2_182");Check(g.HeroPower(0,GT(b)).Success&&b.Health==5&&g.Players[1].Health==28,"Without Sniper a forged target cannot redirect Hunter power");
  g=GG("WARRIOR");b=g.Summon(0,"VAN_CS2_231");Check(GP(g,"GVG_050")&&g.Players.All(p=>p.Board.Count==0),"Bouncing Blade stops at first death");
  g=GG("WARRIOR");g.Summon(0,"VAN_EX1_007");Check(GP(g,"GVG_050")&&g.Players[0].Hand.Count==3,"Bouncing Blade processes each Acolyte damage trigger");
  g=GG("WARRIOR");b=g.Summon(0,"GVG_051");g.Damage(GT(b),1);Check(g.AttackValue(b)==2&&g.IsEnraged(b),"Warbot enrages");g.Heal(GT(b),1);Check(g.AttackValue(b)==1,"Warbot enrage ends when healed");
  g=GG("WARRIOR");b=g.Summon(0,"VAN_CS2_182");g.Damage(GT(b),1);int crush=GH(g,"GVG_052");Check(g.HandCost(0,g.Players[0].Hand.Single())==3,"Crush damaged-friendly discount");var enemy=g.Summon(1,"VAN_EX1_096");Check(g.Play(0,crush,GT(enemy)).Success&&g.Players[0].Mana==7&&g.Players[1].Hand.Count==1,"Crush destroys and resolves deathrattle");
  g=GG("WARRIOR");g.Summon(0,"GVG_086");Check(GP(g,"GVG_053")&&g.Players[0].Armor==5&&g.AttackValue(g.Players[0].Board.First())==6,"Shieldmaiden armor triggers Siege Engine once");g.Players[0].Mana=10;Check(g.HeroPower(0).Success&&g.Players[0].Armor==7&&g.AttackValue(g.Players[0].Board.First())==7,"Armor hero power triggers Siege Engine");
  g=GG("WARRIOR");b=g.Summon(0,"GVG_058");Check(GP(g,"GVG_055",GT(b))&&g.AttackValue(b)==4&&b.MaxHealth==4,"Screwjank buffs friendly Mech");
  g=GG("WARRIOR");b=g.Summon(0,"VAN_CS2_182");Check(!GP(g,"GVG_055",GT(b))&&g.Players[0].Mana==10,"Screwjank refuses non-Mech without payment");
  g=GG("WARRIOR");Check(GP(g,"GVG_056")&&g.Players[1].Deck.Contains("GVG_056t"),"Juggernaut shuffles mine");g.Players[1].Deck.Clear();g.Players[1].Deck.AddRange(new[]{"GVG_056t","VAN_CS2_182"});g.Draw(1,1);Check(g.Players[1].Health==20&&g.Players[1].Hand.Single().CardId=="VAN_CS2_182"&&!g.Players[1].Deck.Any(),"Mine explodes and replaces itself with next draw");
  GvgOgreTests();
  g=GG("WARLOCK");Check(GP(g,"GVG_015",new MatchTarget(1))&&g.Players[1].Health==27,"Darkbomb three damage");
  g=GG("WARLOCK");g.Players[0].Health=20;b=g.Summon(0,"GVG_018");b.SummonTurn=0;Check(g.Attack(0,b.Id,new MatchTarget(1)).Success&&g.Players[0].Health==21,"Mistress heals for combat damage");
  g=GG("WARLOCK");b=g.Summon(0,"GVG_018");Check(GP(g,"GVG_019",GT(b))&&g.AttackValue(b)==6&&b.MaxHealth==9,"Demonheart buffs friendly Demon");
  g=GG("WARLOCK");b=g.Summon(1,"GVG_018");Check(GP(g,"GVG_019",GT(b))&&!g.Players[1].Board.Contains(b),"Demonheart damages enemy Demon instead");
  g=GG("WARLOCK");g.Summon(0,"GVG_020");b=g.Summon(1,"VAN_CS2_182");g.EndTurn();Check(b.Health==3,"Fel Cannon only non-Mech damaged");
  g=GG("WARLOCK");b=g.Summon(0,"GVG_018");var mal=g.Summon(0,"GVG_021");Check(g.AttackValue(b)==3&&b.MaxHealth==6&&g.Damage(new MatchTarget(0),5)==0,"Malganis Demon aura and hero immunity");g.Silence(GT(mal));Check(g.AttackValue(b)==1&&b.MaxHealth==4&&g.Damage(new MatchTarget(0),1)==1,"Malganis silence removes aura and immunity");
  g=GG("WARLOCK");b=g.Summon(1,"VAN_CS2_182");Check(GP(g,"GVG_045",GT(b))&&g.Players[0].Board.Count==b.DamageTaken&&g.Players[0].Board.All(u=>u.Race=="DEMON"),"Imp-losion summons one Imp per damage");
  g=GG("WARLOCK");b=g.Summon(1,"GVG_058");Check(GP(g,"GVG_045",GT(b))&&g.Players[0].Board.Count==0,"Shield prevents Imp-losion tokens");
  g=GG("WARLOCK");g.Summon(0,"GVG_077");g.EndTurn();Check(!g.Players[0].Board.Any(),"Anima Golem self-destructs alone at end of either turn");
  g=GG("WARLOCK");b=g.Summon(0,"GVG_100");g.Damage(new MatchTarget(0),1);Check(g.AttackValue(b)==6&&b.MaxHealth==6,"Floating Watcher grows on own-turn hero damage");g.Active=1;g.Damage(new MatchTarget(0),1);Check(g.AttackValue(b)==6,"Floating Watcher does not grow on opponent turn");
 }
 static void GvgOgreTests(){foreach(string key in new[]{"GVG_054","GVG_066","GVG_088"}){bool redirected=false,original=false;for(int seed=0;seed<40;seed++){var first=matchRules.TrainingDeck(key=="GVG_054"?"WARRIOR":key=="GVG_066"?"SHAMAN":"ROGUE");var second=matchRules.TrainingDeck("MAGE");var g=new MatchEngine(catalog,first,second,seed,true);g.MarkMulliganCompleteForTests();foreach(var p in g.Players){p.Hand.Clear();p.Board.Clear();p.Mana=p.MaxMana=10;}var b=g.Summon(1,"VAN_CS2_182");int attacker=0;if(key=="GVG_054")g.Equip(0,key);else{var source=g.Summon(0,key);source.SummonTurn=0;attacker=source.Id;}Check(g.Attack(0,attacker,new MatchTarget(1)).Success,"Ogre attack legal "+key);if(b.DamageTaken>0)redirected=true;else original=true;}Check(redirected&&original,"Ogre random redirection and normal outcome both reachable "+key);}}
 static void GvgThreeEffects(){
  var g=GG("PRIEST");var b=g.Summon(0,"VAN_CS2_182");var enemy=g.Summon(1,"VAN_CS2_182");Check(GP(g,"GVG_008")&&b.Health==1&&enemy.Health==1,"Lightbomb hits each minion for own attack");
  g=GG("PRIEST");Check(GP(g,"GVG_009")&&g.Players.All(p=>p.Health==27),"Shadowbomber both heroes");
  g=GG("PRIEST");b=g.Summon(0,"VAN_CS2_182");Check(GP(g,"GVG_010",GT(b))&&g.AttackValue(b)==6&&b.MaxHealth==9&&g.SpellPower(0)==1,"Velen Chosen stats and spell power");
  g=GG("PRIEST");b=g.Summon(1,"VAN_CS2_182");Check(GP(g,"GVG_011",GT(b))&&g.AttackValue(b)==2,"Shrinkmeister temporary attack reduction");g.EndTurn();Check(g.AttackValue(b)==4,"Shrinkmeister reduction expires");
  g=GG("PRIEST");g.Players[0].Health=20;Check(GP(g,"GVG_012",new MatchTarget(0))&&g.Players[0].Health==23&&g.Players[0].Board.Single().CardId=="VAN_EX1_001","Light of Naaru wounded target summons Lightwarden");
  g=GG("PRIEST");g.Players[0].Health=29;Check(GP(g,"GVG_012",new MatchTarget(0))&&!g.Players[0].Board.Any(),"Light of Naaru no summon on fully healed target");
  g=GG("PRIEST");b=g.Summon(1,"VAN_CS2_182");g.Damage(GT(b),1);Check(GP(g,"GVG_014",GT(b))&&b.Health==2&&g.Players[0].Board.Single().Health==4,"Voljin exchanges current health, not maximum");
  g=GG("PRIEST");g.Summon(0,"GVG_072");g.Players[0].Health=29;g.Heal(new MatchTarget(0),1);Check(g.Players[1].Health==29,"Original Shadowboxer triggers for hero healing");
  g=GG("PRIEST");b=g.Summon(0,"GVG_058");Check(GP(g,"GVG_083",GT(b))&&b.MaxHealth==6,"Upgraded Repair Bot +4 health to Mech");
  g=GG("ROGUE");g.EquipRaw(0,"GAME_DAGGER",1,2,false);Check(GP(g,"GVG_023")&&g.WeaponAttackValue(0)==2,"Autobarber buffs equipped weapon");
  g=GG("ROGUE");g.Equip(0,"GVG_024");Check(g.WeaponAttackValue(0)==1,"Cogmaster Wrench without Mech");b=g.Summon(0,"GVG_058");Check(g.WeaponAttackValue(0)==3,"Cogmaster Wrench with Mech");NaxxKill(g,b);Check(g.WeaponAttackValue(0)==1,"Cogmaster Wrench aura removed with last Mech");
  g=GG("ROGUE");b=g.Summon(0,"GVG_025");g.Summon(0,"VAN_CS2_146");Check(b.Stealth,"One-eyed Cheat gains stealth from another Pirate");
  g=GG("ROGUE");g.EquipRaw(0,"GAME_DAGGER",1,2,false);b=g.Summon(0,"VAN_CS2_182");g.CardsPlayed=1;Check(GP(g,"GVG_022")&&g.WeaponAttackValue(0)==4&&g.AttackValue(b)==7,"Oil weapon and combo random minion buff");
  g=GG("ROGUE");g.Summon(0,"GVG_027");b=g.Summon(0,"GVG_058");g.EndTurn();Check(g.AttackValue(b)==4&&b.MaxHealth==4,"Iron Sensei buffs another Mech at owner end");
  g=GG("ROGUE");g.Summon(0,"GVG_028");g.Active=1;Check(g.Play(1,Hand(g,"CS2_029",1),new MatchTarget(0)).Success&&g.Players[0].Hand.Any(h=>h.CardId=="VAN_CS2_029")&&g.Players[1].Hand.Any(h=>h.CardId=="GVG_028t"),"Gallywix copies opponent spell and grants special Coin");g.Players[1].Mana=0;Check(g.Play(1,g.Players[1].Hand.Single(h=>h.CardId=="GVG_028t").Id).Success&&g.Players[0].Hand.Count==1,"Gallywix special Coin cannot retrigger Gallywix");
  g=GG("ROGUE");g.Summon(1,"VAN_CS2_182");g.EquipRaw(1,"GAME_DAGGER",1,2,false);g.CardsPlayed=1;Check(GP(g,"GVG_047")&&!g.Players[1].Board.Any()&&g.Players[1].WeaponId==null,"Sabotage combo destroys random minion and enemy weapon");
  g=GG("SHAMAN");GH(g,"GVG_058");GH(g,"GVG_037",1);Check(GP(g,"GVG_029")&&g.Players.All(p=>p.Board.Count==1&&p.Hand.Count==0),"Ancestors Call summons from both hands without battlecries");
  g=GG("SHAMAN");g.Equip(0,"GVG_036");b=g.Summon(0,"GVG_058");g.BreakWeapon(0);Check(g.AttackValue(b)==4&&b.MaxHealth==4,"Powermace deathrattle buffs friendly Mech");
  g=GG("SHAMAN");Check(GP(g,"GVG_037")&&g.HasWindfury(g.Players[0].Board.Single()),"Whirling Zap-o-matic Windfury");
  g=GG("SHAMAN");Check(GP(g,"GVG_038",new MatchTarget(1))&&g.Players[1].Health>=24&&g.Players[1].Health<=27&&g.Players[0].Overload==1,"Crackle 3-6 and overload");
  g=GG("SHAMAN");g.Players[0].Health=20;g.Summon(0,"GVG_039");g.EndTurn();Check(g.Players[0].Health==24,"Vitality Totem heals owner at end");
  g=GG("SHAMAN");g.Summon(0,"GVG_040");b=g.Summon(0,"VAN_EX1_506a");NaxxKill(g,b);Check(g.Players[0].Hand.Count==1,"Siltfin draws for another friendly Murloc death");
  g=GG("SHAMAN");Check(GP(g,"GVG_042")&&g.Players[0].Hand.Count==4&&g.Players[0].Hand.All(h=>g.Card(h.CardId).Race=="MURLOC")&&g.Players[0].Overload==3,"Neptulon grants four Murlocs and overload three");
  GvgHelpersAndPrivacy();
  g=GG("SHAMAN");g.Summon(0,"GVG_006");int zap=GH(g,"GVG_037");Check(g.HandCost(0,g.Players[0].Hand.Single())==1&&g.Play(0,zap).Success&&g.Players[0].Mana==9,"Upstream Mechwarper discounts new class Mech");
  g=GG("PRIEST");g.Summon(0,"GVG_117");g.Players[0].Health=20;Check(GP(g,"GVG_012",new MatchTarget(0))&&g.Players[0].Hand.Count==1&&g.Card(g.Players[0].Hand.Single().CardId).Race=="MECHANICAL","Upstream Gazlowe recognizes actual paid cost of class spell");
 }
 static void GvgHelpersAndPrivacy(){
  foreach(string part in GvgClassRules.Parts){var g=GG();var b=g.Summon(0,"VAN_CS2_182");Check(GP(g,part,GT(b)),"Spare part resolves "+part);Check(!g.Players[0].Hand.Any(h=>h.CardId==part),"Spare part consumes hand "+part);bool effect=part=="PART_001"?b.MaxHealth==6:part=="PART_002"?g.Players[0].Hand.Single().CardId==b.CardId:part=="PART_003"?b.Taunt:part=="PART_004"?b.Stealth:part=="PART_005"?b.Frozen:part=="PART_006"?b.Health==4&&g.AttackValue(b)==5:g.AttackValue(b)==5;Check(effect,"Spare part applies intended effect "+part);}
  var game=GG("PALADIN");int hand=GH(game,"GVG_063");game.GiveGvgCard(0,"GVG_058",-3);var bdy=game.Summon(0,"VAN_CS2_171");NaxxKill(game,bdy);
  var own=LanProtocol.ReadView(catalog,LanProtocol.Copy(LanProtocol.View(game,0,"gvg",1)));var other=LanProtocol.View(game,1,"gvg",1);
  Check(own.Players[0].Hand.Single(h=>h.Id==hand).AttackBonus==1&&own.Players[0].Hand.Any(h=>h.CostAdjustment==-3),"Private hand growth and discount round trip");Check(other.Players[0].Hand.Length==0&&!LanProtocol.Copy(other).Players[0].Hand.Any(),"Opponent does not receive generated hand identities or growth");
  game=GG();for(int i=0;i<10;i++)GH(game,"PART_001");game.GiveGvgCard(0,"GVG_058");Check(game.Players[0].Hand.Count==10,"Generation never exceeds ten cards");
  foreach(string helper in GvgClassRules.Parts.Concat(new[]{"GVG_028t","GVG_056t","GVG_045t","GVG_080t"})){var deck=new DeckDocument{PoolId=GvgClassRules.PoolId,ClassId="MAGE"};Check(DeckRules.CanAdd(catalog,deck,catalog.Card(helper))!=null,"Helper cannot be included in a deck "+helper);}
 }
}

