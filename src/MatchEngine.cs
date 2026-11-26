using System;
using System.Linq;
using System.Collections.Generic;

namespace Touchfish {
public sealed class MatchTarget {public int Seat,UnitId;public MatchTarget(){}public MatchTarget(int seat,int unitId=0){Seat=seat;UnitId=unitId;}public bool Hero{get{return UnitId==0;}}}
public struct HandCostChange {
 public int AfterSource;
 public int Adjustment;
}
public sealed class HandCard {
 public int Id,CostAdjustment,AttackBonus,HealthBonus;
 public string CardId;
 public bool Generated;
 public HandCostChange[] FixedCostChanges;
 internal HandCard CopyForSimulation(){
  var copy=(HandCard)MemberwiseClone();
  if(FixedCostChanges!=null)copy.FixedCostChanges=(HandCostChange[])FixedCostChanges.Clone();
  return copy;
 }
 internal int AdjustmentAfter(int sourceId){
  return FixedCostChanges==null?0:FixedCostChanges.Where(change=>change.AfterSource>=sourceId).Sum(change=>change.Adjustment);
 }
 internal void ApplyAfterFixedCost(int sourceId,int adjustment){
  var changes=FixedCostChanges??new HandCostChange[0];
  if(changes.Length>0&&changes[changes.Length-1].AfterSource==sourceId){
   changes[changes.Length-1]=new HandCostChange{AfterSource=sourceId,Adjustment=changes[changes.Length-1].Adjustment+adjustment};
  }else FixedCostChanges=changes.Concat(new[]{new HandCostChange{AfterSource=sourceId,Adjustment=adjustment}}).ToArray();
 }
}
public sealed class BattleUnit {
 public int Id,Owner,BaseAttack,BaseHealth,DamageTaken,AuraHealth,AttacksUsed,SummonTurn,FrozenTurn,DeathAnchor;
 public EnchantmentCollection Enchantments=new EnchantmentCollection();public int BuffAttack{get{return Enchantments.Stat(false);}set{Enchantments.SetStat(false,value);}}public int BuffHealth{get{return Enchantments.Stat(true);}set{Enchantments.SetStat(true,value);}}public int CommandingShoutTurn{get{return Enchantments.Expiry("commanding-shout");}set{Enchantments.SetExpiry("commanding-shout",value);}}public int OriginalOwner;
 public int ImmuneUntilTurn{get{return Enchantments.Expiry("immune");}set{Enchantments.SetExpiry("immune",value);}}
 public int TempAttack{get{return Enchantments.Counter("temporary-attack");}set{Enchantments.SetCounter("temporary-attack",value);}}
 public int AncestralSpirits{get{return Enchantments.Counter("ancestral-spirit");}set{Enchantments.SetCounter("ancestral-spirit",value);}}
 public int ForestDeathrattles{get{return Enchantments.Counter("forest-deathrattle");}set{Enchantments.SetCounter("forest-deathrattle",value);}}
 public int WisdomBlessings0{get{return Enchantments.Counter("wisdom-0");}set{Enchantments.SetCounter("wisdom-0",value);}}
 public int WisdomBlessings1{get{return Enchantments.Counter("wisdom-1");}set{Enchantments.SetCounter("wisdom-1",value);}}
 public int StealthExpirySeat{get{return Enchantments.Value("stealth-seat");}set{Enchantments.SetValue("stealth-seat",value);}}
 public int StealthUntilTurn{get{return Enchantments.Value("stealth-turn");}set{Enchantments.SetValue("stealth-turn",value);}}
 public int DestroyAtEnd{get{return Enchantments.Value("destroy-end");}set{Enchantments.SetValue("destroy-end",value);}}
 public int DestroyAtStart0{get{return Enchantments.Value("destroy-start-0");}set{Enchantments.SetValue("destroy-start-0",value);}}
 public int DestroyAtStart1{get{return Enchantments.Value("destroy-start-1");}set{Enchantments.SetValue("destroy-start-1",value);}}
 public int ReturnSeat{get{return Enchantments.Value("return-seat");}set{Enchantments.SetValue("return-seat",value);}}
 public int ReturnTurn{get{return Enchantments.Value("return-turn");}set{Enchantments.SetValue("return-turn",value);}}
 public string CardId,Race;public bool Taunt,Shield,Windfury,Charge,Poison,Stealth,Frozen,Silenced;
 internal BattleUnit CopyForSimulation(){var copy=(BattleUnit)MemberwiseClone();copy.Enchantments=Enchantments.CopyForSimulation();return copy;}
 public int SpellDamage;public int MaxHealth{get{return Math.Max(1,BaseHealth+BuffHealth+AuraHealth);}}public int Health{get{return MaxHealth-DamageTaken;}}
}
public sealed class MatchPlayer {
 public string DisplayName;public string HeroFormId,HeroAbilityId;public int HeroMaxHealth;public bool HeroDestroyed;public string ClassId;public int Health=30,Armor,MaxMana,Mana,Overload,LockedMana,Fatigue,SecretCount,MinionsPlayedThisTurn,HeroAttacks,TempAttack,FrozenTurn;
 public int BeastsSummoned,LockAndLoadTurn=-1;
 public int PowersThisTurn,PowersThisGame,PowerDiscount,PowerTax,PowerTaxTurn=-1;
 public int NextDragonDiscount;public int Shadowform;public int WeaponInstanceId;public bool FreeSecretThisTurn,Frozen,PowerUsed;public string WeaponId;public int WeaponAttack,WeaponDurability;public bool WeaponWindfury;
 public int SpellCostPenalty,SpellCostPenaltyUntilTurn=-1;
 public int SecretImmuneTurn=-1;public int CommandingShoutTurn=-1;public int PreparationDiscount;public int FreeSpellsUntilTurn=-1;public int ReportedHandCount=-1;public bool MulliganDone;public string EmoteText;public int HandCount{get{return ReportedHandCount<0?Hand.Count:ReportedHandCount;}}
 public readonly List<string> Deck=new List<string>();public readonly List<HandCard> Hand=new List<HandCard>();public readonly List<BattleUnit> Board=new List<BattleUnit>();
}
public sealed class ActionResult {public bool Success;public string Message;}
public sealed class EffectContext {public MatchEngine Game;public int Seat;public CardRecord Card;public MatchTarget Target;public BattleUnit Summoned;public bool Combo;public int PreviousCardsPlayed;public HandCard PlayedHand;public string Choice;}
public sealed class MatchChoiceOption {public string Key,Title,Description;public MatchChoiceOption(string key,string title,string description){Key=key;Title=title;Description=description;}}
public sealed class CardEffect {public string Target="NONE";public Action<EffectContext> Apply;public Func<MatchEngine,int,string> Requirement;internal EffectInstruction[] Program;}
public sealed partial class MatchEngine {
 public readonly CardCatalog Catalog;public readonly MatchRules Rules;public readonly MatchPlayer[] Players=new MatchPlayer[]{new MatchPlayer(),new MatchPlayer()};
 public readonly string[][] PrivateDeckPreview=new string[2][] {new string[0],new string[0]};bool heroAttackImmune;
 internal int NewEffectInstanceId(){return nextId++;}
 public readonly List<string> Log=new List<string>();public int FirstSeat;public int SecondSeat{get{return 1-FirstSeat;}}public int Active,Turn=1,Winner=-2,CardsPlayed;public readonly int Seed;int nextId=1;readonly SimulationRandom random;
 public readonly GameModeDefinition Mode;public readonly bool TestMode;bool openingEmotesPlayed,defeatEmotePlayed;public bool MulliganComplete{get{return Players[0].MulliganDone&&Players[1].MulliganDone;}}
 public bool Finished{get{return Winner!=-2;}}
 internal MatchEngine(CardCatalog catalog,GameModeDefinition mode=null){Mode=mode??GameModes.Classic;TestMode=Mode.AllowTestTools;Catalog=catalog;Rules=new MatchRules(catalog);Seed=0;random=new SimulationRandom(0);VisualSession=Guid.NewGuid().ToString("N");}
 public MatchEngine(CardCatalog catalog,DeckDocument first,DeckDocument second,int seed,bool testMode=false,GameModeDefinition mode=null,Action<MatchRules> configureEffects=null,int startingSeat=0,string firstName=null,string secondName=null){
  if(startingSeat<0||startingSeat>1)throw new ArgumentOutOfRangeException("startingSeat");Players[0].DisplayName=PlayerNames.Normalize(firstName,0);Players[1].DisplayName=PlayerNames.Normalize(secondName,1);FirstSeat=Active=startingSeat;Mode=mode??(testMode?GameModes.LocalTest:GameModes.Classic);TestMode=Mode.AllowTestTools;Catalog=catalog;CardPoolId=first.PoolId;Rules=new MatchRules(catalog);customSimulationCallbacks=configureEffects!=null;if(configureEffects!=null)configureEffects(Rules);Seed=seed;random=new SimulationRandom(seed);VisualSession=Guid.NewGuid().ToString("N");var validate=Mode.ValidateDeck??DeckRules.Validate;validate(catalog,first,Mode.RequireCompleteDeck);validate(catalog,second,Mode.RequireCompleteDeck);
  DeckRules.RequirePlayablePool(catalog,first.PoolId);
  DeckRules.RequirePlayablePool(catalog,second.PoolId);
  if(new[]{first.ClassId,second.ClassId}.Any(c=>!new[]{"MAGE","WARRIOR","WARLOCK","ROGUE","PRIEST","PALADIN","HUNTER","SHAMAN","DRUID"}.Contains(c)))throw new InvalidOperationException("当前对战支持经典九职业。");
  var documents=new[]{first,second};for(int seat=0;seat<2;seat++){var missing=documents[seat].Cards.Keys.Where(id=>!Rules.Supports(catalog.Card(id))).ToArray();if(missing.Length>0)throw new InvalidOperationException("卡组含未实现效果："+String.Join("、",missing.Take(4).Select(id=>catalog.Card(id).Name))+"。可选择基础练习卡组。");var p=Players[seat];p.ClassId=documents[seat].ClassId;p.Health=Mode.StartingHealth;foreach(var pair in documents[seat].Cards)for(int n=0;n<pair.Value;n++)p.Deck.Add(pair.Key);for(int n=p.Deck.Count-1;n>0;n--){int index=random.Next(n+1);string temp=p.Deck[n];p.Deck[n]=p.Deck[index];p.Deck[index]=temp;}}
  Log.Add("对局开始 · "+PlayerLabel(0)+" / "+PlayerLabel(1));Draw(FirstSeat,Mode.FirstHand);Draw(SecondSeat,Mode.SecondHand);if(TestMode)foreach(var player in Players)player.MaxMana=Mode.ManaLimit;
 }
 public ActionResult Mulligan(int seat,int[] selectedIds){return Act(()=>{if(MulliganComplete)throw new InvalidOperationException("起手换牌已结束。");if(seat<0||seat>1||Players[seat].MulliganDone)throw new InvalidOperationException("该玩家已完成换牌。");selectedIds=selectedIds??new int[0];if(selectedIds.Distinct().Count()!=selectedIds.Length||selectedIds.Length>(seat==FirstSeat?Mode.FirstHand:Mode.SecondHand))throw new InvalidOperationException("换牌选择无效。");var hand=Players[seat].Hand;var selected=selectedIds.Select(id=>hand.FirstOrDefault(c=>c.Id==id)).ToArray();if(selected.Any(c=>c==null))throw new InvalidOperationException("所选手牌已变化，请重新选择。");foreach(var c in selected)hand.Remove(c);for(int n=Players[seat].Deck.Count-1;n>0;n--){int index=random.Next(n+1);string temp=Players[seat].Deck[n];Players[seat].Deck[n]=Players[seat].Deck[index];Players[seat].Deck[index]=temp;}int replacements=Math.Min(selected.Length,Players[seat].Deck.Count);Draw(seat,replacements,recordHistory:false);foreach(var c in selected)Players[seat].Deck.Add(c.CardId);for(int n=Players[seat].Deck.Count-1;n>0;n--){int index=random.Next(n+1);string temp=Players[seat].Deck[n];Players[seat].Deck[n]=Players[seat].Deck[index];Players[seat].Deck[index]=temp;}NoteHistory(HistoryKind.Mulligan,seat,count:0);NoteHistory(HistoryKind.Mulligan,seat,count:selected.Length,visibleTo:seat);Players[seat].MulliganDone=true;Log.Add(PlayerLabel(seat)+" · 完成起手换牌");if(MulliganComplete){Players[SecondSeat].Hand.Add(new HandCard{Id=nextId++,CardId="GAME_COIN"});NoteHistory(HistoryKind.Gain,SecondSeat,"GAME_COIN");Players[FirstSeat].MaxMana=Players[FirstSeat].Mana=Mode.StartingMana;if(Mode.StartingMana>1)foreach(var p in Players)p.MaxMana=p.Mana=Mode.StartingMana;Log.Add("起手换牌结束 · 对局开始");ResolveTurnStart(FirstSeat);if(!openingEmotesPlayed){openingEmotesPlayed=true;for(int player=0;player<2;player++)AutoEmote(player,HeroEmotes.Opening(Players[player].ClassId),"开场白");}}});}
 public ActionResult Emote(int seat,string phrase){return Act(()=>{if(seat<0||seat>1||String.IsNullOrWhiteSpace(phrase)||phrase.Length>80||!HeroEmotes.Contains(Players[seat].ClassId,phrase))throw new InvalidOperationException("对话内容无效。");if(!MulliganComplete)throw new InvalidOperationException("请先完成起手换牌。");Players[seat].EmoteText=phrase;EmitVisual("emote",seat,name:phrase);Log.Add(PlayerLabel(seat)+" · "+phrase);});}
 void AutoEmote(int seat,string phrase,string label){Players[seat].EmoteText=phrase;EmitVisual("emote",seat,name:phrase);Log.Add(PlayerLabel(seat)+" · "+label+"："+phrase);}
 public void MarkMulliganCompleteForTests(){Players[0].MulliganDone=Players[1].MulliganDone=true;}
 public ActionResult TestTakeCard(int seat,string cardId){return Act(()=>{
  if(!TestMode)throw new InvalidOperationException("仅本地 test 可指定取牌。");RequireTurn(seat);var p=Players[seat];int index=p.Deck.IndexOf(cardId);if(index<0)throw new InvalidOperationException("剩余牌库中没有这张牌。");var card=Card(cardId);if(card.CardClass!="NEUTRAL"&&card.CardClass!=p.ClassId)throw new InvalidOperationException("剩余牌库包含其他职业卡牌。");if(p.Hand.Count>=Mode.HandLimit)throw new InvalidOperationException("手牌已满十张，请先使用手牌。");p.Deck.RemoveAt(index);NoteHistory(HistoryKind.Unmodeled,seat);p.Hand.Add(new HandCard{Id=nextId++,CardId=cardId});Log.Add(PlayerLabel(seat)+" · test 取牌："+Card(cardId).Name);
 });}
 public CardRecord Card(string id){var auxiliary=HighCostRules.Auxiliary(id);if(auxiliary!=null)return auxiliary;if(id=="GAME_BANANA")return new CardRecord{Id=id,BaseId=id,Name="香蕉",Type="SPELL",CardClass="NEUTRAL",Cost=1,Collectible=false,Text="使一个随从获得+1/+1。",Mechanics=new string[0]};if(id=="GAME_MECHANICAL_DRAGON")return new CardRecord{Id=id,BaseId=id,Name="机械幼龙",Type="MINION",CardClass="NEUTRAL",Cost=1,Attack=2,Health=1,Race="MECHANICAL",Collectible=false,Text="",Mechanics=new string[0]};if(id=="GAME_HOUND")return new CardRecord{Id=id,BaseId=id,Name="猎犬",Type="MINION",CardClass="HUNTER",Cost=1,Attack=1,Health=1,Race="BEAST",Collectible=false,Text="冲锋。",Mechanics=new[]{"CHARGE"}};if(id=="GAME_BLOOD_FURY")return new CardRecord{Id=id,BaseId=id,Name="血怒",Type="WEAPON",CardClass="NEUTRAL",Attack=3,Durability=8,Collectible=false,Mechanics=new string[0]};if(id=="GAME_FROG")return new CardRecord{Id=id,BaseId=id,Name="青蛙",Type="MINION",CardClass="NEUTRAL",Attack=0,Health=1,Collectible=false,Text="嘲讽",Mechanics=new[]{"TAUNT"}};if(id=="GAME_DEFENDER"||id=="GAME_SPELLBENDER"||id=="GAME_SNAKE")return new CardRecord{Id=id,BaseId=id,Name=id=="GAME_DEFENDER"?"防御者":id=="GAME_SPELLBENDER"?"扰咒师":"蛇",Type="MINION",CardClass="NEUTRAL",Attack=id=="GAME_DEFENDER"?2:1,Health=id=="GAME_SPELLBENDER"?3:1,Race=id=="GAME_SNAKE"?"BEAST":null,Collectible=false,Mechanics=new string[0]};if(id=="GAME_BATTLE_AXE"||id=="GAME_HEAVY_AXE")return new CardRecord{Id=id,BaseId=id,Name=id=="GAME_BATTLE_AXE"?"战斧":"重斧",Type="WEAPON",CardClass="WARRIOR",Attack=id=="GAME_BATTLE_AXE"?2:1,Durability=id=="GAME_BATTLE_AXE"?2:3,Collectible=false,Mechanics=new string[0]};if(id=="GAME_WORTHLESS_IMP")return new CardRecord{Id=id,BaseId=id,Name="无用的小鬼",Type="MINION",CardClass="WARLOCK",Cost=1,Attack=1,Health=1,Race="DEMON",Collectible=false,Text="",Mechanics=new string[0]};if(id=="GAME_DEFIAS_BANDIT")return new CardRecord{Id=id,BaseId=id,Name="迪菲亚强盗",Type="MINION",CardClass="ROGUE",Cost=1,Attack=2,Health=1,Collectible=false,Text="",Mechanics=new string[0]};if(id=="GAME_EXCESS_MANA")return new CardRecord{Id=id,BaseId=id,Name="法力过剩",Type="SPELL",CardClass="DRUID",Cost=0,Collectible=false,Text="抽一张牌。",Mechanics=new string[0]};if(id=="GAME_HYENA")return new CardRecord{Id=id,BaseId=id,Name="土狼",Type="MINION",CardClass="NEUTRAL",Cost=2,Attack=2,Health=2,Race="BEAST",Collectible=false,Text="",Mechanics=new string[0]};if(id=="GAME_ASHBRINGER")return new CardRecord{Id=id,BaseId=id,Name="灰烬使者",Type="WEAPON",CardClass="NEUTRAL",Cost=5,Attack=5,Durability=3,Mechanics=new string[0]};if(id=="GAME_GNOLL")return new CardRecord{Id=id,BaseId=id,Name="豺狼人",Type="MINION",CardClass="NEUTRAL",Cost=2,Attack=2,Health=2,Mechanics=new[]{"TAUNT"}};if(id=="GAME_WHELP")return new CardRecord{Id=id,BaseId=id,Name="雏龙",Type="MINION",CardClass="NEUTRAL",Cost=1,Attack=1,Health=1,Race="DRAGON",Collectible=false,Text="",Mechanics=new string[0]};if(id=="GAME_COIN")return new CardRecord{Id=id,BaseId=id,Name="幸运币",Type="SPELL",CardClass="NEUTRAL",Cost=0,Text="仅在本回合获得一个法力水晶。",Mechanics=new string[0]};if(id=="GAME_DAGGER")return new CardRecord{Id=id,BaseId=id,Name="英雄匕首",Type="WEAPON",Attack=1,Durability=2,Text="1点攻击，2点耐久。",Mechanics=new string[0]};if(id=="GAME_PANTHER")return new CardRecord{Id=id,BaseId=id,Name="猎豹",Type="MINION",CardClass="NEUTRAL",Attack=3,Health=2,Race="BEAST",Mechanics=new string[0]};if(id=="GAME_TREANT_TAUNT")return new CardRecord{Id=id,BaseId=id,Name="树人",Type="MINION",CardClass="NEUTRAL",Attack=2,Health=2,Mechanics=new[]{"TAUNT"}};return Catalog.Card(id);}
 public BattleUnit Unit(MatchTarget target){return target==null||target.Hero||target.Seat<0||target.Seat>=Players.Length?null:Players[target.Seat].Board.FirstOrDefault(m=>m.Id==target.UnitId);}
 public int AttackValue(BattleUnit unit){return Math.Max(0,UnflooredAttack(unit));}
 internal int UnflooredAttack(BattleUnit unit){if(!unit.Silenced&&Card(unit.CardId).BaseId=="EX1_335")return unit.Health;return unit.BaseAttack+unit.BuffAttack+unit.TempAttack+EnrageRules.AttackBonus(unit,Card(unit.CardId).BaseId)+AuraAttack(unit);}

 public int HeroAttack(int seat){return Math.Max(0,WeaponAttackValue(seat)+Players[seat].TempAttack);}
 public int SpellPower(int seat){return Players[seat].Board.Where(m=>m.Health>0).Sum(m=>m.SpellDamage)+2*Players.SelectMany(p=>p.Board).Count(m=>m.Health>0&&!m.Silenced&&Card(m.CardId).BaseId=="LOE_051");}
 public string CanAttack(int seat,BattleUnit unit){if(!MulliganComplete)return "请先完成起手换牌。";if(Finished)return "对局已结束。";if(seat!=Active)return "当前不是你的回合。";if(unit==null||unit.Owner!=seat)return "请选择己方随从。";if(unit.Frozen)return "随从被冻结。";if(ExplorerRules.StatueBlocked(this,unit))return "场上还有其他随从，雕像不能攻击。";if(AttackValue(unit)<=0)return "攻击力为零。";if(unit.SummonTurn==Turn&&!HasCharge(unit))return "刚召唤的随从下回合才能攻击。";if(unit.AttacksUsed>=MinionAttackLimit(unit))return "本回合攻击次数已用完。";if(!unit.Silenced&&Has(Card(unit.CardId),"CANT_ATTACK")&&unit.Enchantments.Expiry("tgt-can-attack")!=Turn)return "该随从不能攻击。";return null;}
 public string CanHeroAttack(int seat){if(!MulliganComplete)return "请先完成起手换牌。";var p=Players[seat];if(Finished)return "对局已结束。";if(seat!=Active)return "当前不是你的回合。";if(p.Frozen)return "英雄被冻结。";if(HeroAttack(seat)<=0)return "英雄没有攻击力。";if(p.HeroAttacks>=(p.WeaponWindfury?2:1))return "英雄本回合已攻击。";return null;}
 public ActionResult Attack(int seat,int attackerId,MatchTarget target){return Act(()=>{
  var attacker=attackerId==0?null:Players[seat].Board.FirstOrDefault(m=>m.Id==attackerId);Require(attackerId==0?CanHeroAttack(seat):CanAttack(seat,attacker));ValidateTarget(seat,target,"ENEMY_CHARACTER",false);
  if(attacker!=null&&!attacker.Silenced&&Card(attacker.CardId).BaseId=="AT_125"&&target.Hero)throw new InvalidOperationException("该随从无法攻击英雄。");
  var victim=Unit(target);if(Players[1-seat].Board.Any(m=>m.Taunt&&!m.Stealth)&&(victim==null||!victim.Taunt))throw new InvalidOperationException("必须先攻击嘲讽随从。");
  if(attacker==null){Players[seat].HeroAttacks++;if(CardOrNull(Players[seat].WeaponId)!=null&&Card(Players[seat].WeaponId).BaseId=="CS2_097")RestoreHealth(seat,new MatchTarget(seat),2);}else{attacker.AttacksUsed++;attacker.Stealth=false;}
  target=GvgAttackTarget(seat,attacker,target);if(!SecretBeforeAttack(seat,attackerId,ref target)){Log.Add(PlayerLabel(seat)+" · 攻击被奥秘中止");Cleanup();return;}if(!headless){EmitVisual("attack",seat,attackerId);var attackVisual=VisualEvents[VisualEvents.Count-1];attackVisual.TargetSeat=target.Seat;attackVisual.TargetUnitId=target.UnitId;}victim=Unit(target);int outgoing=attacker==null?HeroAttack(seat):AttackValue(attacker),incoming=victim==null?0:AttackValue(victim);
  if(attacker!=null)TournamentAttack(attacker,target);
  var cleave=GvgCleaveTargets(attacker,target);int dealt=0,received=0;heroAttackImmune=attacker==null&&Players[seat].WeaponId!=null&&Card(Players[seat].WeaponId).BaseId=="DS1_188";try{DamageBatch(()=>{dealt=Damage(target,outgoing);received=incoming>0?Damage(new MatchTarget(seat,attackerId),incoming):0;foreach(var adjacent in cleave)Damage(adjacent,outgoing);});}finally{heroAttackImmune=false;}
  GvgMinionDamage(attacker,target,dealt);GvgMinionDamage(victim,new MatchTarget(seat,attackerId),received);if(attacker!=null&&attacker.Poison&&dealt>0&&victim!=null)victim.DamageTaken=victim.MaxHealth;
  if(victim!=null&&victim.Poison&&received>0&&attacker!=null)attacker.DamageTaken=attacker.MaxHealth;


  if(attacker!=null){if(attacker.WisdomBlessings0>0)Draw(0,attacker.WisdomBlessings0);if(attacker.WisdomBlessings1>0)Draw(1,attacker.WisdomBlessings1);}Log.Add(PlayerLabel(seat)+" · "+(attacker==null?"英雄":Card(attacker.CardId).Name)+" → "+TargetLabel(target)+" · 伤害 "+dealt+(victim==null?"":" / 反击 "+received));if(attacker==null)WarriorRules.UseWeapon(this,seat,target);Cleanup();TournamentBearAfterAttack(target);
 });}
 public ActionResult Play(int seat,int handId,MatchTarget target=null,int position=-1,string choice=null){return Act(()=>{
  RequireTurn(seat);
  var p=Players[seat];
  var hand=p.Hand.FirstOrDefault(c=>c.Id==handId);
  if(hand==null)throw new InvalidOperationException("手牌已不存在。");
  var card=Card(hand.CardId);
  int cost=HandCost(seat,hand);
  bool fizzle=ValidatePlay(seat,hand,card,cost,target,choice);
  var cardListeners=Players.SelectMany(player=>player.Board).Where(u=>u.Health>0&&!u.Silenced).OrderBy(u=>u.Id).ToArray();
  string targetName=target==null?"":TargetLabel(target);
  var context=new EffectContext{Game=this,Seat=seat,Card=card,Combo=ComboRules.Active(this),PreviousCardsPlayed=CardsPlayed,PlayedHand=hand,Choice=choice};
  p.Mana-=cost;
  p.Hand.Remove(hand);
  if(card.Type=="MINION"&&card.Race=="DRAGON")p.NextDragonDiscount=0;
  bool secret=Has(card,"SECRET");
  if(secret)p.FreeSecretThisTurn=false;
  if(card.Type=="SPELL"){
   p.PreparationDiscount=0;
   RecordGraveyard(seat,seat,hand.CardId,hand.Id,GraveyardReason.SpellCast);
  }
  Log.Add(PlayerLabel(seat)+" · 使用 "+(secret?"一张奥秘":card.Name)+"（"+cost+"费）"+
   (card.Type!="MINION"?"":" · 第 "+((position<0||position>p.Board.Count?p.Board.Count:position)+1)+" 位")+
   (target==null?"":" → "+targetName));
  NoteHistory(HistoryKind.Played,seat,secret?null:card.Id);
  CardsPlayed++;
  ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.CardPlayed,seat,card);
  if(card.Type=="SPELL"){
   if(p.LockAndLoadTurn==Turn)TournamentClassRules.RandomCard(this,seat,x=>x.CardClass=="HUNTER");
   ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.SpellCast,seat,card,paidCost:cost);
  }
  if(Finished)return;
  if(card.Type=="MINION")p.MinionsPlayedThisTurn++;
  if(card.Type=="SPELL"){
   BlackrockRules.SpellTargeted(this,seat,target);
   TournamentRules.SpellTargeted(this,seat,target);
   EmitSpell(card,seat,cost,choice);
   if(SecretSpell(seat,ref target)){
    Log.Add(PlayerLabel(seat)+" · 法术已被反制");
    Cleanup();
    ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.CardResolved,seat,card,listeners:cardListeners);
    return;
   }
  }
  context.Target=target;
  if(DeferPyroblast(context,()=>CompletePlayedCard(context,cardListeners,cost)))return;
  int repeats=Has(card,"BATTLECRY")&&ExplorerRules.Has(this,seat,"LOE_077")?2:1;
  if(card.Type=="MINION"){
   context.Summoned=SummonHandCard(seat,hand,position,!HeroReplacementRules.ReplacesHeroOnPlay(card),true);
   ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.MinionPlayed,seat,card,summoned:context.Summoned,listeners:cardListeners,playedHand:hand);
  }
  if(card.Type=="WEAPON")Equip(seat,card.Id);
  if(Rules.IsChoice(card)&&card.BaseId!=HunterRules.TrackingCardId)
   Log.Add(PlayerLabel(seat)+" · 抉择："+Rules.Choices(card).First(option=>option.Key==choice).Title);
  if(!fizzle&&Rules.DynamicProgram(card)!=null){
   StartDecisionProgram(seat,card,context.Summoned,cardListeners,repeats,cost,choice);
   return;
  }
  if(!fizzle){
   ApplyCardEffect(context);
   if(repeats==2){
    Cleanup();
    if(!Finished)ApplyCardEffect(context);
   }
  }
  CompletePlayedCard(context,cardListeners,cost);
 });}

 void CompletePlayedCard(EffectContext context,BattleUnit[] cardListeners,int paidCost,bool stopOnFinish=false){
  int seat=context.Seat;
  var card=context.Card;
  FlushSummonTriggers();
  if(stopOnFinish&&Finished)return;
  OverloadRules.Queue(this,seat,card.Overload);
  Cleanup();
  if(stopOnFinish&&Finished)return;
  if(card.Type=="SPELL")ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.SpellResolved,seat,card,
   listeners:cardListeners,paidCost:paidCost,spell:context);
  if(card.Type=="MINION")SecretMinionPlayed(seat,context.Summoned,HeroReplacementRules.ReplacesHeroOnPlay(card));
  if(stopOnFinish&&Finished)return;
  Cleanup();
  ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.CardResolved,seat,card,summoned:context.Summoned,listeners:cardListeners);
 }

 internal bool ValidatePlay(int seat,HandCard hand,CardRecord card,int cost,MatchTarget target,string choice){
  var player=Players[seat];
  if(!CanUseCard(seat,card,hand))throw new InvalidOperationException("只能使用本职业或中立卡牌。");
  if(!Rules.Supports(card))throw new InvalidOperationException("该卡效果尚未实现。");
  bool requiresChoice=Rules.IsChoice(card,this,seat);
  if((requiresChoice||card.BaseId==HunterRules.TrackingCardId&&choice!=null)&&!Rules.ValidChoice(card,choice,this,seat))
   throw new InvalidOperationException("请先选择一项抉择效果。");
  if(cost>player.Mana)throw new InvalidOperationException("法力不足。");
  if(player.Board.Count>=Mode.BoardSlots&&(card.Type=="MINION"||card.BaseId=="EX1_160"&&choice=="panther"))
   throw new InvalidOperationException("战场已满。");
  var effect=Rules.Effect(card);
  if(effect.Requirement!=null)Require(effect.Requirement(this,seat));
  string mask=Rules.Target(card,choice,this,seat);
  bool fizzle=card.Type=="MINION"&&mask!="NONE"&&target==null&&!HasTargets(seat,mask,false);
  if(mask!="NONE"&&!fizzle)ValidateTarget(seat,target,mask,card.Type=="SPELL");
  return fizzle;
 }
 public bool HasTargets(int seat,string mask,bool ability){foreach(var target in Characters(0).Concat(Characters(1)))try{ValidateTarget(seat,target,mask,ability);return true;}catch(InvalidOperationException){}return false;}
 public ActionResult HeroPower(int seat,MatchTarget target=null,string choice=null){return Act(()=>{
  RequireTurn(seat);
  Require(HeroPowerAvailability(seat));
  string targetMask=HeroPowerTargetMask(seat);
  if(targetMask!="NONE")ValidateTarget(seat,target,targetMask,true);
  var p=Players[seat];
  var registered=HeroReplacementRules.Ability(p.HeroAbilityId);
  if(p.HeroAbilityId=="tgt:upgraded-SHAMAN"){
   if(!TournamentTotems.Contains(choice))throw new InvalidOperationException("请选择一个基础图腾。");
   p.Mana-=HeroPowerCost(seat);Summon(seat,choice);Cleanup();if(!Finished)FinishTournamentPower(seat);return;
  }
  if(registered!=null){UseRegisteredHeroPower(seat,target,registered);return;}
  p.Mana-=HeroPowerCost(seat);p.PowerUsed=true;Log.Add(PlayerLabel(seat)+" · 英雄技能（2费）"+(target==null?"":" → "+TargetLabel(target)));if(p.Shadowform>0)Damage(target,TournamentPowerDamage(seat,p.Shadowform+1));else switch(p.ClassId){case "MAGE":Damage(target,TournamentPowerDamage(seat,1));break;case "PRIEST":RestoreHealth(seat,target,2,true);break;case "WARRIOR":GainGvgArmor(seat,2);break;case "HUNTER":Damage(GvgClassRules.Has(this,seat,"GVG_087")?target:new MatchTarget(1-seat),TournamentPowerDamage(seat,2));break;case "WARLOCK":TournamentPowerDraw(seat);Damage(new MatchTarget(seat),2*AbilityMultiplier(seat));break;case "ROGUE":if(p.WeaponId!=null&&Card(p.WeaponId).BaseId=="AT_034"&&p.WeaponDurability>0)p.WeaponAttack++;else EquipRaw(seat,"GAME_DAGGER",1,2,false);break;case "PALADIN":Summon(seat,"VAN_CS2_101t");break;case "DRUID":p.TempAttack++;GainGvgArmor(seat,1);break;case "SHAMAN":var totems=AvailableBasicTotems(seat);Summon(seat,totems[random.Next(totems.Length)]);break;default:throw new InvalidOperationException("该职业技能尚未实现。");}Cleanup();if(!Finished)FinishTournamentPower(seat);
 });}
 public ActionResult EndTurn(){return Act(()=>{
  RequireTurn(Active);if(Mode.TurnEnded!=null)Mode.TurnEnded(this,Active);var p=Players[Active];ResolveEndTriggers(Active);if(Finished)return;ResolveEndDelayed();if(Finished)return;
  TimedCostRules.Expire(this);foreach(var player in Players){player.TempAttack=0;foreach(var unit in player.Board)unit.TempAttack=0;}
  foreach(var unit in p.Board)if(unit.Frozen&&CanThaw(unit))unit.Frozen=false;if(p.Frozen&&p.HeroAttacks<(p.WeaponWindfury?2:1))p.Frozen=false;
  Active=1-Active;Turn++;CardsPlayed=0;DeathsThisTurn=0;p=Players[Active];p.MaxMana=Math.Min(Mode.ManaLimit,p.MaxMana+1);OverloadRules.StartTurn(this,Active);p.PowerUsed=false;p.PowersThisTurn=0;p.MinionsPlayedThisTurn=0;p.HeroAttacks=0;foreach(var unit in p.Board)unit.AttacksUsed=0;ResolveTurnStart(Active);if(Finished)return;Log.Add("第 "+((Turn+1)/2)+" 回合 · 轮到"+PlayerLabel(Active));
 },true);}
 void ResolveTurnStart(int seat){if(Mode.TurnStarted!=null)Mode.TurnStarted(this,seat);ExpireRogueStealth(seat);ReturnComboCards(seat);ResolveStartDelayed(seat);if(Finished)return;TurnStartRules.Resolve(this,seat);if(Finished)return;Draw(seat,1);Cleanup();}
 public void ValidateTarget(int seat,MatchTarget target,string mask,bool ability){
  if(mask=="HUNTER_POWER"&&target!=null&&target.Hero&&target.Seat==seat)throw new InvalidOperationException("请选择随从或敌方英雄。");
  if(mask=="FRIENDLY_MECH"&&(target==null||target.Seat!=seat||LivingUnit(target)==null||Unit(target).Race!="MECHANICAL"))throw new InvalidOperationException("请选择己方机械随从。");
  if(target!=null&&target.Seat>=0&&target.Seat<2){var gvgUnit=Unit(target);if(ability&&gvgUnit!=null&&GvgClassRules.Protected(this,gvgUnit))throw new InvalidOperationException("目标不能被法术或英雄技能指定。");if(target.Hero&&target.Seat!=seat&&GvgClassRules.HeroImmune(this,target.Seat))throw new InvalidOperationException("该英雄免疫。");}
  if(target==null||target.Seat<0||target.Seat>1)throw new InvalidOperationException("请选择一个目标。");var unit=Unit(target);if(target.Hero&&target.Seat!=seat&&Players[target.Seat].SecretImmuneTurn==Turn)throw new InvalidOperationException("该英雄本回合免疫。");if(!target.Hero&&unit==null)throw new InvalidOperationException("目标已离开战场。");if(target.Seat!=seat&&unit!=null&&unit.ImmuneUntilTurn==Turn)throw new InvalidOperationException("该随从本回合免疫。");if(target.Seat!=seat&&unit!=null&&unit.Stealth)throw new InvalidOperationException("不能指定敌方潜行随从。");if(ability&&unit!=null&&!unit.Silenced&&(Has(Card(unit.CardId),"CANT_BE_TARGETED_BY_ABILITIES")||Has(Card(unit.CardId),"ELUSIVE")))throw new InvalidOperationException("目标不能被法术或英雄技能指定。");
  if(mask=="FRIENDLY_DEATHRATTLE_MINION"&&(target.Seat!=seat||!DeathrattleRules.HasDeathrattle(this,unit)))throw new InvalidOperationException("请选择具有亡语的友方随从。");
  if(mask=="LEGENDARY_MINION"&&(unit==null||Card(unit.CardId).Rarity!="LEGENDARY"))throw new InvalidOperationException("请选择一个传说随从。");if(mask=="BEAST_MINION"&&(unit==null||unit.Health<=0||unit.Race!="BEAST"))throw new InvalidOperationException("请选择一个野兽随从。");if(mask=="FRIENDLY_BEAST"&&(unit==null||unit.Health<=0||unit.Race!="BEAST"))throw new InvalidOperationException("请选择己方野兽随从。");if(mask=="HERO"&&!target.Hero)throw new InvalidOperationException("请选择一个英雄。");if(mask=="ENEMY_ATTACK_TWO"&&(unit==null||target.Seat==seat||AttackValue(unit)>2))throw new InvalidOperationException("请选择攻击力不超过2的敌方随从。");if(mask=="DEMON_CHARACTER"&&(target.Hero?HeroRace(target.Seat)!="DEMON":unit.Race!="DEMON"))throw new InvalidOperationException("请选择恶魔角色。");if(mask=="MURLOC_MINION"&&unit!=null&&unit.Race!="MURLOC")throw new InvalidOperationException("请选择一个鱼人随从。");if(mask=="DEMON_MINION"&&unit!=null&&unit.Race!="DEMON")throw new InvalidOperationException("请选择一个恶魔随从。");if(mask.Contains("MINION")&&unit==null)throw new InvalidOperationException("请选择随从目标。");if(mask.StartsWith("ENEMY")&&target.Seat==seat)throw new InvalidOperationException("请选择敌方目标。");if(mask.StartsWith("FRIENDLY")&&target.Seat!=seat)throw new InvalidOperationException("请选择己方目标。");if(mask=="UNDAMAGED_MINION"&&(unit==null||unit.DamageTaken!=0))throw new InvalidOperationException("只能指定未受伤的随从。");if((mask=="LOW_ATTACK"||mask=="ENEMY_LOW_ATTACK")&&(unit==null||AttackValue(unit)>3))throw new InvalidOperationException("只能指定攻击力不超过3的随从。");if(mask=="ENEMY_TAUNT_MINION"&&(unit==null||!unit.Taunt))throw new InvalidOperationException("请选择具有嘲讽的敌方随从。");if(mask=="ATTACK_SEVEN"&&(unit==null||AttackValue(unit)<7))throw new InvalidOperationException("请选择攻击力至少7的随从。");if(mask=="HIGH_ATTACK"&&(unit==null||AttackValue(unit)<5))throw new InvalidOperationException("只能指定攻击力至少5的随从。");
  if(mask=="DAMAGED_MINION"&&unit.DamageTaken==0)throw new InvalidOperationException("只能指定受伤的随从。");if(mask=="ENEMY_DAMAGED_MINION"&&unit.DamageTaken==0)throw new InvalidOperationException("只能斩杀受伤的敌方随从。");
 }
 public BattleUnit Summon(int seat,string id,int position=-1,bool summonEvent=true,bool deferAfterSummon=false,Action<BattleUnit> initialize=null,Action<BattleUnit> beforeTriggers=null){if(Finished||Players[seat].Board.Count>=Mode.BoardSlots)return null;var card=Card(id);var commanders=Players[seat].Board.Where(m=>!m.Silenced&&m.Health>0&&Card(m.CardId).BaseId=="EX1_084").ToArray();var existingTidecallers=Players.SelectMany(p=>p.Board).Where(m=>!m.Silenced&&m.Health>0&&Card(m.CardId).BaseId=="EX1_509").ToArray();var summonListeners=Players.SelectMany(p=>p.Board).Where(u=>u.Health>0&&!u.Silenced).OrderBy(u=>u.Id).ToArray();var unit=new BattleUnit{Id=nextId++,Owner=seat,OriginalOwner=seat,CardId=id,BaseAttack=card.Attack,BaseHealth=card.Health,Race=card.Race,Taunt=Has(card,"TAUNT"),Shield=Has(card,"DIVINE_SHIELD"),Charge=Has(card,"CHARGE"),Windfury=Has(card,"WINDFURY"),Poison=Has(card,"POISONOUS")||card.BaseId=="EX1_170"||card.BaseId=="EX1_522",Stealth=Has(card,"STEALTH"),SpellDamage=card.SpellDamage,SummonTurn=Turn};if(initialize!=null)initialize(unit);if(unit.Race=="BEAST"&&MulliganComplete)Players[seat].BeastsSummoned++;var board=Players[seat].Board;if(position<0||position>board.Count)position=board.Count;board.Insert(position,unit);RefreshAuras();if(beforeTriggers!=null)beforeTriggers(unit);if(summonEvent){PaladinRules.Summoned(this,unit);MurlocSummonRules.Resolve(this,unit,existingTidecallers);WarriorRules.Summoned(this,unit,commanders);MinionSpeechRules.Entrance(this,unit);if(card.BaseId=="NEW1_030"){EmitVisual("entrance",seat,unit.Id,position,name:card.BaseId);if(HighAnimationsEnabled)StartEntranceWait();}QueueSummonTrigger(unit,summonListeners,deferAfterSummon);}SyncTurnClock();return unit;}
 // Freeze expires after a skipped attack opportunity, including the second Windfury attack.
 bool CanThaw(BattleUnit unit){return (unit.SummonTurn!=Turn||HasCharge(unit))&&unit.AttacksUsed<MinionAttackLimit(unit);}
 public BattleUnit LivingUnit(MatchTarget target){var unit=Unit(target);return unit!=null&&unit.Health>0?unit:null;}
 public void Draw(int seat,int count,int costAdjustment=0,bool recordHistory=true){var p=Players[seat];int drawn=0;for(int i=0;i<count;i++){if(p.Deck.Count==0){int fatigue=++p.Fatigue;int visualStart=VisualEvents.Count==0?0:VisualEvents.Last().Sequence;Damage(new MatchTarget(seat),fatigue);var damage=VisualEvents.FirstOrDefault(e=>e.Sequence>visualStart&&e.Kind=="damage"&&e.Seat==seat&&e.UnitId==0);if(damage!=null)VisualEvents.Remove(damage);EmitVisual("fatigue",seat,amount:damage==null?0:damage.Amount);Log.Add(PlayerLabel(seat)+" · 疲劳伤害 "+fatigue);continue;}string id=p.Deck[0];p.Deck.RemoveAt(0);if(id==ExplorerRules.AncientCurse&&MulliganComplete&&p.Hand.Count<Mode.HandLimit){NoteHistory(HistoryKind.Destroy,seat,id);Log.Add(PlayerLabel(seat)+" · 抽到远古诅咒");Damage(new MatchTarget(seat),7);Cleanup();if(Finished)break;i--;continue;}if(id=="AT_035t"&&MulliganComplete&&p.Hand.Count<Mode.HandLimit){Summon(1-seat,"AT_036t");i--;continue;}if(id=="GVG_056t"&&MulliganComplete&&p.Hand.Count<Mode.HandLimit){NoteHistory(HistoryKind.Destroy,seat,id);Damage(new MatchTarget(seat),10);Log.Add(PlayerLabel(seat)+" · 地雷爆炸");Cleanup();if(Finished)break;i--;continue;}if(p.Hand.Count<Mode.HandLimit){var drawnCard=new HandCard{Id=nextId++,CardId=id,CostAdjustment=costAdjustment,Generated=blackrockGeneratedDeck[seat].Contains(id)};ApplyTournamentDraw(seat,drawnCard);p.Hand.Add(drawnCard);if(recordHistory)NoteHistory(HistoryKind.Draw,seat,adjustment:costAdjustment);drawn++;BlackrockDrawn(seat,drawnCard);GvgDrawn(seat,id);ResolveTournamentDraw(seat,drawnCard);if(Finished)break;}else{RecordGraveyard(seat,seat,id,nextId++,GraveyardReason.Burned);var card=Card(id);NoteHistory(HistoryKind.Burn,seat,id);if(!headless){EmitVisual("burn",seat,name:card.Name);var burned=VisualEvents[VisualEvents.Count-1];burned.CardId=id;burned.CardCost=card.Cost;burned.CardText=card.Type+"\n"+(card.Type=="MINION"?card.Attack+" / "+card.Health+"\n":"")+card.Text;EmitVisual("burn-speech",seat,name:"我的手牌已经满了！");}Log.Add(PlayerLabel(seat)+" · 爆牌："+card.Name);}}if(drawn>0)Log.Add(PlayerLabel(seat)+" · 抽牌 "+drawn+" 张");}
 public int Damage(MatchTarget target,int amount){if(amount<=0||!IsPresentTarget(target))return 0;var unit=Unit(target);if(target.Hero){if(GvgClassRules.HeroImmune(this,target.Seat)||heroAttackImmune&&target.Seat==Active)return 0;int heroDamage=ExplorerRules.DamageToHero(this,target.Seat,amount);if(SecretPreHeroDamage(target.Seat,heroDamage))return 0;var protector=Players[target.Seat].Board.FirstOrDefault(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="AT_124");if(protector!=null)return Damage(new MatchTarget(target.Seat,protector.Id),amount);amount=heroDamage;var p=Players[target.Seat];int armor=Math.Min(p.Armor,amount);p.Armor-=armor;p.Health-=amount-armor;GvgClassRules.HeroDamaged(this,target.Seat,amount);EmitVisual("damage",target.Seat,amount:amount);SecretHeroDamaged(target.Seat,amount);return amount;}if(unit==null||unit.ImmuneUntilTurn==Turn)return 0;if(unit.Shield){unit.Shield=false;return 0;}if(unit.CommandingShoutTurn==Turn)amount=Math.Min(amount,Math.Max(0,unit.Health-1));if(amount<=0)return 0;var listeners=damageListeners??Players.SelectMany(p=>p.Board).Where(m=>m.Health>0&&!m.Silenced).OrderBy(m=>m.Id).ToArray();RememberLethalDamage(unit,amount);unit.DamageTaken+=amount;TriggerDamage(unit,amount,listeners);EmitVisual("damage",target.Seat,unit.Id,Players[target.Seat].Board.IndexOf(unit),amount);return amount;}
 public void Heal(MatchTarget target,int amount){if(amount<=0||target==null||target.Seat<0||target.Seat>=Players.Length)return;if(target.Hero){if(!Players[target.Seat].HeroDestroyed){int before=Players[target.Seat].Health;Players[target.Seat].Health=Math.Min(HeroHealthLimit(target.Seat),before+amount);int restored=Players[target.Seat].Health-before;if(restored>0)TriggerHealing(target,null,restored);}}else{var unit=Unit(target);if(unit==null||unit.Health<=0)return;int restored=Math.Min(unit.DamageTaken,amount);unit.DamageTaken-=restored;if(restored>0)TriggerHealing(target,unit,restored);}}
 public void Freeze(MatchTarget target){if(!IsPresentTarget(target))return;var unit=Unit(target);if(target.Hero){Players[target.Seat].Frozen=true;Players[target.Seat].FrozenTurn=Turn;Log.Add(PlayerLabel(target.Seat)+" · 英雄被冻结，不能攻击");}else if(unit!=null){unit.Frozen=true;unit.FrozenTurn=Turn;}}
 public void Buff(MatchTarget target,int attack,int health){var unit=LivingUnit(target);if(unit==null)return;unit.BuffAttack+=attack;unit.BuffHealth+=health;}
 public void SetHealth(MatchTarget target,int value){var unit=LivingUnit(target);if(unit==null)return;unit.BuffHealth=value-unit.BaseHealth-unit.AuraHealth;unit.DamageTaken=0;}
 public void Silence(MatchTarget target){var unit=Unit(target);if(unit==null)return;int health=unit.Health;unit.Silenced=true;unit.Enchantments.Silence();unit.CommandingShoutTurn=-1;unit.BuffAttack=unit.BuffHealth=unit.TempAttack=0;unit.Taunt=unit.Shield=unit.Charge=unit.Windfury=unit.Poison=unit.Stealth=unit.Frozen=false;unit.StealthExpirySeat=unit.StealthUntilTurn=-1;unit.SpellDamage=unit.ForestDeathrattles=unit.WisdomBlessings0=unit.WisdomBlessings1=0;unit.DestroyAtEnd=unit.DestroyAtStart0=unit.DestroyAtStart1=-1;unit.DamageTaken=Math.Max(0,unit.MaxHealth-Math.Min(health,unit.MaxHealth));RefreshAuras();}
 public void Transform(MatchTarget target,string id){var old=Unit(target);if(old==null)return;int position=Players[target.Seat].Board.IndexOf(old);Players[target.Seat].Board.Remove(old);var replacement=Summon(target.Seat,id,position,false);if(replacement!=null){replacement.OriginalOwner=old.OriginalOwner;replacement.SummonTurn=old.SummonTurn;replacement.AttacksUsed=old.AttacksUsed;replacement.ReturnSeat=old.ReturnSeat;replacement.ReturnTurn=old.ReturnTurn;}}
 public void ReturnToHand(MatchTarget target){var unit=Unit(target);if(unit!=null)BounceRogueUnits(new[]{unit},0);}


 public void StealRandomEnemyMinion(int seat){var enemy=Players[1-seat].Board;var eligible=enemy.Where(m=>m.Health>0).ToArray();if(eligible.Length==0||Players[seat].Board.Count>=Mode.BoardSlots)return;var unit=eligible[random.Next(eligible.Length)];enemy.Remove(unit);unit.Owner=seat;unit.OriginalOwner=seat;unit.ReturnSeat=unit.ReturnTurn=-1;unit.SummonTurn=Turn;unit.AttacksUsed=0;Players[seat].Board.Add(unit);RefreshAuras();Log.Add(PlayerLabel(seat)+" · 随机获得敌方随从："+Card(unit.CardId).Name);}
 public void Equip(int seat,string id){var card=Card(id);EquipRaw(seat,id,card.Attack,card.Durability,Has(card,"WINDFURY"));}
 public void EquipRaw(int seat,string id,int attack,int durability,bool windfury){var p=Players[seat];string replaced=p.WeaponId;if(replaced!=null)RecordGraveyard(seat,seat,replaced,p.WeaponInstanceId,GraveyardReason.WeaponReplaced);p.WeaponInstanceId=nextId++;p.WeaponId=id;p.WeaponAttack=attack;p.WeaponDurability=durability;p.WeaponWindfury=windfury;p.WeaponAttack+=p.Board.Count(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="AT_029");NaxxRules.WeaponRemoved(this,seat,replaced);GvgClassRules.WeaponRemoved(this,seat,replaced);}
 public void BreakWeapon(int seat){var p=Players[seat];string removed=p.WeaponId;if(p.WeaponId!=null){RecordGraveyard(seat,seat,p.WeaponId,p.WeaponInstanceId,GraveyardReason.WeaponDestroyed);Log.Add(PlayerLabel(seat)+" · 武器移除："+Card(p.WeaponId).Name);}p.WeaponInstanceId=0;p.WeaponId=null;p.WeaponAttack=p.WeaponDurability=0;p.WeaponWindfury=false;NaxxRules.WeaponRemoved(this,seat,removed);GvgClassRules.WeaponRemoved(this,seat,removed);}
 string TargetLabel(MatchTarget target){if(target.Hero)return PlayerLabel(target.Seat)+" 英雄";var unit=Unit(target);return unit==null?"随从":Card(unit.CardId).Name;}
 public MatchTarget[] Characters(int seat,bool hero=true){var targets=Players[seat].Board.Where(m=>m.Health>0).Select(m=>new MatchTarget(seat,m.Id)).ToList();if(hero&&IsPresentTarget(new MatchTarget(seat)))targets.Add(new MatchTarget(seat));return targets.ToArray();}
 public MatchTarget RandomTarget(IEnumerable<MatchTarget> targets){return SelectRandomTargets(targets,1).FirstOrDefault();}
 public void Cleanup(){FlushDamageTriggers();for(int wave=0;wave<32;wave++){RefreshAuras();var dead=Players.SelectMany(p=>p.Board).Where(m=>m.Health<=0).OrderBy(m=>m.Id).ToArray();if(dead.Length==0)break;DeathsThisTurn+=dead.Length;var deathSecrets=secrets.ToArray();GvgClassRules.MinionsDied(this,dead);foreach(var unit in dead){EmitMinionDeath(unit);var board=Players[unit.Owner].Board;var anchor=board.Skip(board.IndexOf(unit)+1).FirstOrDefault(m=>m.Health>0);unit.DeathAnchor=anchor==null?0:anchor.Id;}foreach(var unit in dead){RecordGraveyard(unit.Owner,unit.OriginalOwner,unit.CardId,unit.Id,GraveyardReason.MinionDied,AttackValue(unit),unit.MaxHealth);Log.Add(PlayerLabel(unit.Owner)+" · 随从阵亡："+Card(unit.CardId).Name);Players[unit.Owner].Board.Remove(unit);}RefreshAuras();var deathListeners=Players.SelectMany(p=>p.Board).Where(u=>u.Health>0&&!u.Silenced).OrderBy(u=>u.Id).ToArray();foreach(var unit in dead)ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.MinionDied,unit.Owner,listeners:deathListeners,died:unit,cleanup:false);foreach(var unit in dead){HunterRules.MinionDied(this,unit);Rules.Death(this,unit);SecretMinionDied(unit,deathSecrets);}}lethalVisualDamage.Clear();if(Players[0].HeroDestroyed||Players[1].HeroDestroyed)Winner=Players[0].HeroDestroyed&&Players[1].HeroDestroyed?-1:Players[0].HeroDestroyed?1:0;else if(Mode.EvaluateWinner!=null)Winner=Mode.EvaluateWinner(this);else if(Players[0].Health<=0&&Players[1].Health<=0)Winner=-1;else if(Players[0].Health<=0)Winner=1;else if(Players[1].Health<=0)Winner=0;SyncTurnClock();if(Finished&&!defeatEmotePlayed){defeatEmotePlayed=true;if(Winner==-1){for(int seat=0;seat<2;seat++)AutoEmote(seat,HeroEmotes.Defeat(Players[seat].ClassId),"输了");}else{int loser=1-Winner;AutoEmote(loser,HeroEmotes.Defeat(Players[loser].ClassId),"输了");}}}
 public static bool Has(CardRecord card,string keyword){return card.Mechanics!=null&&card.Mechanics.Contains(keyword);}
 CardRecord CardOrNull(string id){CardRecord card;return id!=null&&Catalog.Cards.TryGetValue(id,out card)?card:null;}
 void RequireTurn(int seat){if(!MulliganComplete)throw new InvalidOperationException("请先完成起手换牌。");if(Finished)throw new InvalidOperationException("对局已结束。");if(seat!=Active)throw new InvalidOperationException("当前不是你的回合。");}
 static void Require(string error){if(error!=null)throw new InvalidOperationException(error);}
 ActionResult Act(Action action,bool allowExpired=false,bool decisionAction=false){if(HasPendingDecision&&!decisionAction)return new ActionResult{Message="请先完成当前决策。"};resolvingActions++;try{if(!allowExpired&&TurnClockExpired)throw new InvalidOperationException("倒计时已结束，等待自动换边。");if(AnimationPlaying)throw new InvalidOperationException("正在播放动画，请稍候。");visualAction++;action();SyncTurnClock();MarkSearchStateChanged();if(Finished)Log.Add(Winner==-1?"对局结束 · 平局":"对局结束 · "+PlayerLabel(Winner)+" 获胜");return new ActionResult{Success=true,Message=Finished?(Winner==-1?"双方英雄同时阵亡，平局。":PlayerLabel(Winner)+" 获胜。"):"操作完成。"};}catch(InvalidOperationException ex){return new ActionResult{Success=false,Message=ex.Message};}finally{resolvingActions--;}}
}
}
