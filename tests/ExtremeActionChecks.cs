using System;
using System.Linq;
using Touchfish;

public static partial class RuleTests {
 static void ExtremeActions(){
  ExtremeClassPowers();
  ExtremeSpecialPowers();
  ExtremePositionsAndChoices();
  ExtremeAttackAndAvailability();
  ExtremeObservationPrivacy();
 }

 static AgentMove[] ExtremeMoves(MatchEngine game,int seat,string kind){
  return AgentBridge.Observe(game,seat).Moves.Where(move=>move.Kind==kind).ToArray();
 }

 static void CheckExecutableMoves(Func<MatchEngine> setup,int seat,string scenario){
  int count=AgentBridge.Observe(setup(),seat).Moves.Length;
  Check(count>0,scenario+" has available moves");
  for(int i=0;i<count;i++){
   var game=setup();
   var move=AgentBridge.Observe(game,seat).Moves[i];
   var result=AgentBridge.Execute(game,seat,move);
   Check(result.Success,scenario+" "+move.Kind+" "+move.Key+" "+move.Choice+" at "+move.Position+": "+result.Message);
  }
 }

 static void ExtremeClassPowers(){
  foreach(string profession in catalog.Pool("classic-2014").HeroDbfIds.Keys){
   for(int seat=0;seat<2;seat++){
    var game=MatchFixture(profession,profession);
    game.Active=seat;
    game.Players[seat].Health=25;
    var moves=ExtremeMoves(game,seat,"power");
    bool targeted=profession=="MAGE"||profession=="PRIEST";
    Check(moves.Length==(targeted?2:1)&&moves.All(m=>m.Cost==2&&(m.Target!=null)==targeted),profession+" power candidates for seat "+seat);
    var action=moves.First(m=>!targeted||m.Target.Seat==(profession=="PRIEST"?seat:1-seat));
    Check(AgentBridge.Execute(game,seat,action).Success&&game.Players[seat].PowerUsed&&game.Players[seat].Mana==8,profession+" executes and spends two mana");
    Check(ExtremeMoves(game,seat,"power").Length==0,profession+" cannot reuse power");
    var player=game.Players[seat];
    switch(profession){
     case "DRUID":Check(player.Armor==1&&game.HeroAttack(seat)==1,"Druid attack and armor");break;
     case "HUNTER":Check(game.Players[1-seat].Health==28,"Hunter hits opposing hero");break;
     case "MAGE":Check(game.Players[1-seat].Health==29,"Mage deals one");break;
     case "PALADIN":Check(player.Board.Single().CardId=="VAN_CS2_101t","Paladin summons recruit");break;
     case "PRIEST":Check(player.Health==27,"Priest heals two");break;
     case "ROGUE":Check(player.WeaponAttack==1&&player.WeaponDurability==2,"Rogue equips dagger");break;
     case "SHAMAN":Check(player.Board.Count==1&&player.Board[0].Race=="TOTEM","Shaman summons a basic totem");break;
     case "WARLOCK":Check(player.Health==23&&player.Hand.Count==1,"Warlock draws and takes two");break;
     case "WARRIOR":Check(player.Armor==2,"Warrior gains armor");break;
    }
   }
  }
  foreach(string profession in new[]{"PALADIN","SHAMAN"}){
   var game=MatchFixture(profession);
   for(int i=0;i<game.Mode.BoardSlots;i++)game.Summon(0,"VAN_CS2_231");
   Check(ExtremeMoves(game,0,"power").Length==0&&!game.HeroPower(0).Success,profession+" full board blocks hero power");
  }
  var shaman=MatchFixture("SHAMAN");
  foreach(string totem in new[]{"VAN_CS2_050","VAN_CS2_051","VAN_CS2_052","VAN_NEW1_009"})shaman.Summon(0,totem);
  Check(ExtremeMoves(shaman,0,"power").Length==0&&!shaman.HeroPower(0).Success,"All four basic totems present blocks power with spare slots");
 }

 static void ExtremeSpecialPowers(){
  foreach(string hero in new[]{HeroReplacementRules.JaraxxusId,BlackrockRules.Ragnaros}){
   CheckExecutableMoves(()=>{
    var game=MatchFixture("WARLOCK");
    game.ReplaceHero(0,hero);
    return game;
   },0,hero);
  }
  var full=MatchFixture("WARLOCK");
  full.ReplaceHero(0,HeroReplacementRules.JaraxxusId);
  for(int i=0;i<full.Mode.BoardSlots;i++)full.Summon(0,"VAN_CS2_231");
  Check(ExtremeMoves(full,0,"power").Length==0&&!full.HeroPower(0).Success,"Inferno requirement blocks full board");
  for(int form=1;form<=2;form++){
   var priest=MatchFixture("PRIEST");
   priest.Players[0].Shadowform=form;
   var power=ExtremeMoves(priest,0,"power").Single(move=>move.Target.Seat==1);
   Check(power.Damage==form+1&&AgentBridge.Execute(priest,0,power).Success&&priest.Players[1].Health==29-form,"Shadowform damage metadata and execution");
  }
  Func<MatchEngine> sniperSetup=()=>{
   var game=MatchFixture("HUNTER");
   game.Summon(0,"GVG_087");
   game.Summon(1,"VAN_CS2_182");
   game.Summon(1,"VAN_NEW1_023");
   return game;
  };
  var sniper=sniperSetup();
  var powers=ExtremeMoves(sniper,0,"power");
  Check(powers.Length==3&&powers.All(m=>m.Damage==2&&!(m.Target.Hero&&m.Target.Seat==0)),"Sniper targets own minion, enemy minion and enemy hero, excluding Elusive and own hero");
  CheckExecutableMoves(sniperSetup,0,"Steamwheedle Sniper");

  const string custom="test:extreme-power@1";
  HeroReplacementRules.RegisterAbility(new HeroAbilityDefinition(custom,"测试技能",3,"FRIENDLY_MINION",
   (game,seat,target)=>game.Buff(target,1,0),(game,seat)=>game.Players[seat].Armor>0?null:"需要护甲。"));
  var customGame=MatchFixture("MAGE");
  customGame.Players[0].HeroAbilityId=custom;
  var unit=customGame.Summon(0,"VAN_CS2_182");
  Check(ExtremeMoves(customGame,0,"power").Length==0,"Registered custom requirement blocks candidate");
  customGame.Players[0].Armor=1;
  var customMove=ExtremeMoves(customGame,0,"power").Single();
  Check(customMove.Cost==3&&customMove.Target.UnitId==unit.Id&&AgentBridge.Execute(customGame,0,customMove).Success&&unit.BuffAttack==1,"Registered custom cost, target and execution");
 }

 static void ExtremePositionsAndChoices(){
  Func<MatchEngine> keeperSetup=()=>{
   var game=MatchFixture("DRUID");
   game.Summon(0,"VAN_CS2_182");
   game.Summon(0,"VAN_CS2_182");
   game.Summon(1,"VAN_CS2_182");
   Hand(game,"EX1_166");
   return game;
  };
  var keeper=ExtremeMoves(keeperSetup(),0,"play");
  Check(keeper.Length==18&&keeper.Select(m=>m.Position).Distinct().OrderBy(p=>p).SequenceEqual(new[]{0,1,2})&&keeper.Select(m=>m.Choice).Distinct().Count()==2,"Every Keeper choice and legal target appears at each of three positions");
  CheckExecutableMoves(keeperSetup,0,"Keeper choices and positions");
  for(int position=0;position<=2;position++){
   var game=keeperSetup();
   var move=ExtremeMoves(game,0,"play").First(m=>m.Position==position&&m.Target.Seat==1);
   Check(AgentBridge.Execute(game,0,move).Success&&game.Players[0].Board[position].CardId=="VAN_EX1_166","Requested insertion slot is used: "+position);
  }
  var empty=MatchFixture("DRUID");
  Hand(empty,"EX1_166");
  Hand(empty,"EX1_154");
  var noTargets=ExtremeMoves(empty,0,"play");
  Check(noTargets.Length==2&&noTargets.All(m=>m.Key=="EX1_166"&&m.Target==null&&m.Position==0),"Targetless battlecry can fizzle, targeted spell cannot");
  var full=MatchFixture("DRUID");
  for(int i=0;i<full.Mode.BoardSlots;i++)full.Summon(0,"VAN_CS2_231");
  Hand(full,"EX1_160");
  Hand(full,"EX1_166");
  var onlyBuff=ExtremeMoves(full,0,"play").Single();
  Check(onlyBuff.Choice=="buff"&&AgentBridge.Execute(full,0,onlyBuff).Success,"Full board permits Power of the Wild buff only");

  for(int count=0;count<=3;count++){
   int deckCount=count;
   Func<MatchEngine> trackingSetup=()=>{
    var game=MatchFixture("HUNTER");
    game.Players[0].Deck.Clear();
    game.Players[0].Deck.AddRange(new[]{"VAN_CS2_182","VAN_CS2_231","VAN_CS2_029"}.Take(deckCount));
    Hand(game,"DS1_184");
    return game;
   };
   var choices=ExtremeMoves(trackingSetup(),0,"play");
   Check(choices.Length==Math.Max(1,count)&&choices.Select(m=>m.Choice).SequenceEqual(count==0?new string[]{null}:Enumerable.Range(0,count).Select(i=>"track:"+i)),"Tracking options for deck size "+count);
   CheckExecutableMoves(trackingSetup,0,"Tracking with "+count+" cards");
  }
 }

 static void ExtremeAttackAndAvailability(){
  Func<MatchEngine> setup=()=>{
   var game=MatchFixture("ROGUE");
   var attacker=game.Summon(0,"VAN_CS2_182");
   attacker.SummonTurn=0;
   game.EquipRaw(0,"GAME_DAGGER",1,2,false);
   game.Summon(1,"VAN_CS2_179");
   game.Summon(1,"VAN_CS2_182");
   return game;
  };
  var gameState=setup();
  var attacks=ExtremeMoves(gameState,0,"attack");
  Check(attacks.Length==2&&attacks.All(m=>m.Target.UnitId==gameState.Players[1].Board[0].Id),"Hero and minion attack must target taunt");
  CheckExecutableMoves(setup,0,"Taunt and weapon combat");
  gameState.Players[0].Mana=1;
  Check(ExtremeMoves(gameState,0,"power").Length==0,"Insufficient mana blocks hero power");
  Check(!AgentBridge.Observe(gameState,1).CanAct&&AgentBridge.Observe(gameState,1).Moves.Length==0,"Inactive seat receives no moves");
  gameState.ReceiveAnimationWait(1,1000);
  Check(!AgentBridge.Observe(gameState,0).CanAct&&AgentBridge.Observe(gameState,0).Moves.Length==0,"Animation lock blocks candidate operations");
 }

 static void ExtremeObservationPrivacy(){
  var game=MatchFixture("HUNTER","MAGE");
  Hand(game,"DS1_184");
  game.Players[0].Deck.Clear();
  game.Players[0].Deck.AddRange(new[]{"VAN_CS2_182","VAN_CS2_231","VAN_CS2_029"});
  game.Players[1].Deck.Clear();
  game.Players[1].Deck.AddRange(new[]{"VAN_EX1_559","VAN_EX1_561"});
  Hand(game,"EX1_279",1);
  game.ArmSecret(1,new HandCard{Id=998001,CardId="VAN_EX1_287"});
  var visible=game.Summon(1,"VAN_NEW1_023");
  var view=AgentBridge.Observe(game,0);
  var before=JsonData.Serializer().Serialize(view);
  Check(view.Enemy.HandCount==1&&view.Enemy.DeckCount==2&&view.Enemy.SecretCount==1&&view.OwnSecrets.Length==0,"Only public enemy hidden-zone counts are exposed");
  Check(view.Enemy.Board.Single().CardId==visible.CardId&&view.Enemy.Board.Single().AbilityTargetProtected&&view.BoardSlots==7&&view.ManaLimit==10,"Public card identity, protection and rules limits are available");
  game.Players[0].Deck.Reverse();
  game.Players[1].Deck.Clear();
  game.Players[1].Deck.AddRange(new[]{"VAN_CS2_231","VAN_CS2_182"});
  game.Players[1].Hand[0].CardId="VAN_EX1_277";
  game.DestroySecrets(1);
  game.ArmSecret(1,new HandCard{Id=998002,CardId="VAN_EX1_295"});
  Check(JsonData.Serializer().Serialize(AgentBridge.Observe(game,0))==before,"Changing hidden identities and deck order leaves the entire observation identical");
  view.Enemy.Board[0].Health=0;
  view.Hand[0].Cost=99;
  Check(visible.Health>0&&game.HandCost(0,game.Players[0].Hand[0])!=99,"Observation mutations cannot change engine state");
 }
}
