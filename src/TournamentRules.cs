using System;
using System.Linq;
namespace Touchfish {
public static class TournamentRules {
 public const string SetId="grand-tournament-neutral-2015";
 public static readonly string[] Ids={"AT_017","AT_070","AT_080","AT_082","AT_083","AT_084","AT_085","AT_086","AT_087","AT_088","AT_089","AT_090","AT_091","AT_092","AT_093","AT_094","AT_095","AT_096","AT_097","AT_098","AT_099","AT_100","AT_101","AT_102","AT_103","AT_105","AT_106","AT_108","AT_109","AT_110","AT_111","AT_112","AT_113","AT_114","AT_115","AT_117","AT_118","AT_119","AT_120","AT_121","AT_122","AT_123","AT_124","AT_125","AT_127","AT_128","AT_129","AT_131","AT_132","AT_133"};
 public static void Register(MatchRules rules){
  foreach(var id in Ids)rules.Register(id,"NONE",Apply);
  rules.Register("AT_084","FRIENDLY_MINION",Apply);
  rules.Register("AT_096","FRIENDLY_MECH",Apply);
  rules.Register("AT_106","DEMON_MINION",Apply);
  rules.Register("AT_103","ANY_CHARACTER",Apply);
  rules.Register("AT_122","ANY_CHARACTER",Apply);
  foreach(var id in new[]{"AT_099t","AT_113t"})rules.Register(id,"NONE",c=>{});
  rules.RegisterTrigger(new ConditionalTrigger{CardId="AT_121",Timing=MatchTriggerTiming.CardPlayed,Condition=c=>c.Source.Owner==c.Seat&&MatchEngine.Has(c.Card,"BATTLECRY"),Apply=c=>c.Game.Buff(new MatchTarget(c.Source.Owner,c.Source.Id),1,1)});
  foreach(var id in new[]{"AT_082","AT_083","AT_089","AT_090","AT_091","AT_099","AT_100","AT_109","AT_110","AT_113","AT_119","AT_127"})
   rules.RegisterTrigger(new ConditionalTrigger{CardId=id,Timing=MatchTriggerTiming.HeroPowerUsed,Condition=c=>c.Source.Owner==c.Seat,Apply=Inspire});

  RegisterAbilities();
 }
 static bool abilitiesRegistered;
 static void RegisterAbilities(){
  if(abilitiesRegistered)return;
  abilitiesRegistered=true;
  HeroReplacementRules.RegisterAbility(new HeroAbilityDefinition("tgt:upgraded-SHAMAN","图腾崇拜",2,"NONE",(g,s,t)=>{},(g,s)=>PowerRequirement(g,s,"SHAMAN")));
  foreach(var profession in new[]{"MAGE","PRIEST","WARRIOR","WARLOCK","HUNTER","PALADIN","ROGUE","DRUID","SHAMAN"}){
   string job=profession;
   HeroReplacementRules.RegisterAbility(new HeroAbilityDefinition("tgt:basic-"+job,"复制的英雄技能",2,job=="MAGE"||job=="PRIEST"?"ANY_CHARACTER":"NONE",(g,s,t)=>BasicPower(g,s,t,job,false),(g,s)=>PowerRequirement(g,s,job)));
   if(job!="SHAMAN")HeroReplacementRules.RegisterAbility(new HeroAbilityDefinition("tgt:upgraded-"+job,"强化英雄技能",2,job=="MAGE"||job=="PRIEST"?"ANY_CHARACTER":"NONE",(g,s,t)=>BasicPower(g,s,t,job,true),(g,s)=>PowerRequirement(g,s,job)));
  }
 }
 static string PowerRequirement(MatchEngine g,int seat,string job){
  if(job=="SHAMAN"&&g.Players[seat].HeroAbilityId!="tgt:upgraded-SHAMAN"&&MatchEngine.TournamentTotems.All(id=>g.Players[seat].Board.Any(u=>u.CardId==id)))return "没有可召唤的基础图腾。";
  if((job=="SHAMAN"||job=="PALADIN")&&g.Players[seat].Board.Count>=g.Mode.BoardSlots)return "战场已满。";
  return null;
 }
 static void BasicPower(MatchEngine g,int s,MatchTarget t,string job,bool up){
  int mult=g.AbilityMultiplier(s);
  switch(job){
   case "MAGE":g.Damage(t,g.TournamentPowerDamage(s,up?2:1));break;
   case "PRIEST":g.RestoreHealth(s,t,up?4:2,true);break;
   case "HUNTER":g.Damage(GvgClassRules.Has(g,s,"GVG_087")?t:new MatchTarget(1-s),g.TournamentPowerDamage(s,up?3:2));break;
   case "WARRIOR":g.GainGvgArmor(s,up?4:2);break;
   case "WARLOCK":g.TournamentPowerDraw(s);if(!up)g.Damage(new MatchTarget(s),2*mult);break;
   case "PALADIN":g.Summon(s,"VAN_CS2_101t");if(up)g.Summon(s,"VAN_CS2_101t");break;
   case "ROGUE":if(g.Players[s].WeaponId!=null&&g.Card(g.Players[s].WeaponId).BaseId=="AT_034"&&g.Players[s].WeaponDurability>0){g.Players[s].WeaponAttack++;break;}g.EquipRaw(s,up?"AT_132_ROGUEt":"GAME_DAGGER",up?2:1,2,false);break;
   case "DRUID":g.Players[s].TempAttack+=up?2:1;g.GainGvgArmor(s,up?2:1);break;
   case "SHAMAN":var pool=new[]{"VAN_CS2_050","VAN_CS2_051","VAN_CS2_052","VAN_NEW1_009"}.Where(id=>!g.Players[s].Board.Any(u=>u.CardId==id)).ToArray();if(pool.Length>0)g.Summon(s,pool[g.RandomIndex(pool.Length)]);break;
  }
 }
 static void Apply(EffectContext c){
  var g=c.Game;int s=c.Seat;var self=c.Summoned;var target=c.Target;
  switch(c.Card.BaseId){
   case "AT_017":if(BlackrockRules.HoldingDragon(g,s)){g.Buff(T(self),1,0);self.Taunt=true;}break;
   case "AT_084":g.Buff(target,2,0);break;
   case "AT_086":g.Players[1-s].PowerTax+=5;g.Players[1-s].PowerTaxTurn=g.Turn+1;break;
   case "AT_094":g.Damage(g.RandomTarget(g.Characters(1-s)),1);break;
   case "AT_096":g.Buff(target,1,1);break;
   case "AT_098":var enemy=g.Players[1-s];g.Players[s].HeroAbilityId=enemy.HeroAbilityId??(enemy.Shadowform>0?null:"tgt:basic-"+enemy.ClassId);g.Players[s].Shadowform=enemy.Shadowform;g.Players[s].PowerUsed=false;g.Players[s].PowersThisTurn=0;break;
   case "AT_103":g.Damage(target,4);break;
   case "AT_105":g.Damage(T(self),3);break;
   case "AT_106":g.Silence(target);break;
   case "AT_108":if(Joust(g,s))self.Charge=true;break;
   case "AT_111":for(int n=0;n<2;n++)g.RestoreHealth(s,new MatchTarget(n),4);break;
   case "AT_112":if(Joust(g,s)){self.Taunt=true;self.Shield=true;}break;
   case "AT_115":g.Players[s].PowerDiscount+=2;break;
   case "AT_117":if(g.Players[s].Board.Any(u=>u!=self&&u.Health>0&&!u.Silenced&&u.SpellDamage>0))g.Buff(T(self),2,2);break;
   case "AT_118":GiveRandom(g,s,g.GenerationPool().Where(card=>card.CardClass=="PALADIN"));break;
   case "AT_122":if(g.Players[s].Board.Count(u=>u!=self&&u.Health>0)>=4&&target!=null)g.Damage(target,4);break;
   case "AT_132":var p=g.Players[s];if(p.HeroAbilityId==null&&p.Shadowform==0){p.HeroAbilityId="tgt:upgraded-"+p.ClassId;p.PowerUsed=false;p.PowersThisTurn=0;}break;
   case "AT_133":if(Joust(g,s))g.Buff(T(self),1,1);break;
  }
 }
 static MatchTarget T(BattleUnit u){return new MatchTarget(u.Owner,u.Id);}
 static void GiveRandom(MatchEngine g,int s,System.Collections.Generic.IEnumerable<CardRecord> candidates){
  var pool=candidates.Where(c=>g.Rules.Supports(c)).ToArray();
  if(pool.Length>0)g.GiveGvgCard(s,pool[g.RandomIndex(pool.Length)].Id);
 }
 public static bool Joust(MatchEngine g,int seat){
  var revealed=new CardRecord[2];
  for(int s=0;s<2;s++){
   var cards=g.Players[s].Deck.Where(id=>g.Card(id).Type=="MINION").ToArray();
   if(cards.Length>0)revealed[s]=g.Card(cards[g.RandomIndex(cards.Length)]);
  }
  g.Log.Add("拼点 · "+String.Join(" / ",revealed.Select(c=>c==null?"无随从":c.Name+"（"+c.Cost+"费）")));
  return revealed[seat]!=null&&(revealed[1-seat]==null||revealed[seat].Cost>revealed[1-seat].Cost);
 }
 public static void Death(MatchEngine g,BattleUnit u){
  if(g.Card(u.CardId).BaseId=="AT_128"){if(Joust(g,u.Owner))g.GiveGvgCard(u.Owner,u.CardId);return;}
  if(BlackrockRules.HoldingDragon(g,u.Owner))g.DamageBatch(()=>{foreach(var t in g.Characters(0,false).Concat(g.Characters(1,false)).ToArray())g.Damage(t,3);});
 }
 static void Inspire(MatchTriggerContext context){
  var g=context.Game;int s=context.Seat;var u=context.Source;
  switch(g.Card(u.CardId).BaseId){
    case "AT_082":g.Buff(T(u),1,0);break;
    case "AT_083":u.Enchantments.SetExpiry("tgt-windfury",g.Turn);break;
    case "AT_089":g.Buff(T(u),0,1);break;
    case "AT_090":foreach(var other in g.Players[s].Board.Where(v=>v!=u&&v.Health>0).ToArray())g.Buff(T(other),1,1);break;
    case "AT_091":g.RestoreHealth(s,new MatchTarget(s),2);break;
    case "AT_099":g.Summon(s,"AT_099t");break;
    case "AT_100":g.Summon(s,"VAN_CS2_101t");break;
    case "AT_109":u.Enchantments.SetExpiry("tgt-can-attack",g.Turn);break;
    case "AT_110":g.ReturnToHand(T(u));break;
    case "AT_113":g.GiveGvgCard(s,"AT_113t");break;
    case "AT_119":g.Buff(T(u),2,2);break;
    case "AT_127":GiveRandom(g,s,g.GenerationPool().Where(c=>c.Type=="SPELL"));break;
  }
 }
 public static void SpellTargeted(MatchEngine g,int s,MatchTarget target){
  var u=g.Unit(target);
  if(u==null||u.Owner!=s||u.Silenced||u.Health<=0)return;
  if(g.Card(u.CardId).BaseId=="AT_129")u.Shield=true;
  if(g.Card(u.CardId).BaseId=="AT_131")g.Damage(g.RandomTarget(g.Characters(1-s)),3);
 }
 public static bool Has(MatchEngine g,int s,string id){return g.Players[s].Board.Any(u=>u.Health>0&&!u.Silenced&&g.Card(u.CardId).BaseId==id);}
}
public sealed partial class MatchEngine {
 public static readonly string[] TournamentTotems={"VAN_CS2_050","VAN_CS2_051","VAN_CS2_052","VAN_NEW1_009"};
 public bool PowerSpent(int seat){var p=Players[seat];return p.PowersThisTurn>=(TournamentRules.Has(this,seat,"AT_008")?Int32.MaxValue:TournamentRules.Has(this,seat,"AT_080")?2:1)||p.PowersThisTurn==0&&p.PowerUsed;}
 internal int TournamentDiscount(int seat,CardRecord card){
  if(card.BaseId=="AT_070")return Players[seat].Board.Count(u=>u.Health>0&&u.Race=="PIRATE");
  return card.BaseId=="AT_120"?Players[seat].PowersThisGame:0;
 }
 internal void FinishTournamentPower(int seat){
  var p=Players[seat];p.PowersThisTurn++;p.PowersThisGame++;p.PowerDiscount=0;
  p.PowerUsed=p.PowersThisTurn>=(TournamentRules.Has(this,seat,"AT_008")?Int32.MaxValue:TournamentRules.Has(this,seat,"AT_080")?2:1);
  ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.HeroPowerUsed,seat);
 }
}
}
