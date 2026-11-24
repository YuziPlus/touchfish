using System;
using System.Collections.Generic;
using System.Linq;
namespace Touchfish {
public sealed class HeroAbilityDefinition {
 public readonly string Id,Name,Target;public readonly int Cost;
 public readonly Action<MatchEngine,int,MatchTarget> Apply;public readonly Func<MatchEngine,int,string> Requirement;
 public HeroAbilityDefinition(string id,string name,int cost,string target,Action<MatchEngine,int,MatchTarget> apply,Func<MatchEngine,int,string> requirement=null){Id=id;Name=name;Cost=cost;Target=target;Apply=apply;Requirement=requirement;}
}
public sealed class HeroReplacementDefinition {
 public readonly string Id,Name,Race,AbilityId;public readonly int Health;public readonly bool PreserveArmor,PreserveHealth;public readonly int ArmorGain;
 public HeroReplacementDefinition(string id,string name,int health,string abilityId,string race=null,bool preserveArmor=false,bool preserveHealth=false,int armorGain=0){Id=id;Name=name;Health=health;AbilityId=abilityId;Race=race;PreserveArmor=preserveArmor;PreserveHealth=preserveHealth;ArmorGain=armorGain;}
}
public static class HeroReplacementRules {
 static readonly HashSet<string> replacementMinions=new HashSet<string>{"EX1_323"};
 public static void RegisterReplacementMinion(string effectId){if(String.IsNullOrWhiteSpace(effectId)||!effectId.Contains(":" )||!replacementMinions.Add(effectId))throw new ArgumentException("英雄替换战吼 ID 无效或重复。");}
 public static bool ReplacesHeroOnPlay(CardRecord card){return replacementMinions.Contains(MatchRules.EffectKey(card));}
 static readonly Dictionary<string,HeroAbilityDefinition> abilities=new Dictionary<string,HeroAbilityDefinition>();
 static readonly Dictionary<string,HeroReplacementDefinition> forms=new Dictionary<string,HeroReplacementDefinition>();
 public const string JaraxxusId="classic:jaraxxus@1",InfernoId="classic:inferno@1";
 static HeroReplacementRules(){RegisterAbility(new HeroAbilityDefinition(InfernoId,"地狱火！",2,"NONE",(g,s,t)=>g.Summon(s,"VAN_EX1_tk34"),(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"战场已满。":null));RegisterHero(new HeroReplacementDefinition(JaraxxusId,"加拉克苏斯",15,InfernoId,"DEMON"));}
 public static void RegisterAbility(HeroAbilityDefinition ability){if(ability==null||String.IsNullOrWhiteSpace(ability.Id)||!ability.Id.Contains(":" )||String.IsNullOrWhiteSpace(ability.Name)||String.IsNullOrWhiteSpace(ability.Target)||ability.Cost<0||ability.Apply==null||abilities.ContainsKey(ability.Id))throw new ArgumentException("英雄技能定义无效或重复。");abilities.Add(ability.Id,ability);}
 public static void RegisterHero(HeroReplacementDefinition hero){if(hero==null||String.IsNullOrWhiteSpace(hero.Id)||!hero.Id.Contains(":" )||String.IsNullOrWhiteSpace(hero.Name)||hero.Health<=0||hero.ArmorGain<0||Ability(hero.AbilityId)==null||forms.ContainsKey(hero.Id))throw new ArgumentException("替换英雄定义无效或重复。");forms.Add(hero.Id,hero);}
 public static HeroAbilityDefinition Ability(string id){HeroAbilityDefinition value;return id!=null&&abilities.TryGetValue(id,out value)?value:null;}
 public static HeroReplacementDefinition Hero(string id){HeroReplacementDefinition value;return id!=null&&forms.TryGetValue(id,out value)?value:null;}
 public static void Jaraxxus(EffectContext c){var g=c.Game;if(c.Summoned==null||!g.Players[c.Seat].Board.Remove(c.Summoned))return;g.ReplaceHero(c.Seat,JaraxxusId);g.EquipRaw(c.Seat,"GAME_BLOOD_FURY",3,8,false);g.RefreshAuras();g.EmitVisual("hero-speech",c.Seat,name:"我是加拉克苏斯，燃烧军团的艾瑞达之王");}
}
public sealed partial class MatchEngine {
 public int HeroHealthLimit(int seat){return Players[seat].HeroMaxHealth>0?Players[seat].HeroMaxHealth:Mode.StartingHealth;}
 public string HeroRace(int seat){var form=HeroReplacementRules.Hero(Players[seat].HeroFormId);return form==null?null:form.Race;}
 public int HeroPowerCost(int seat){var ability=HeroReplacementRules.Ability(Players[seat].HeroAbilityId);return ability==null?2:ability.Cost;}
 public void ReplaceHero(int seat,string definitionId){var form=HeroReplacementRules.Hero(definitionId);if(seat<0||seat>1||form==null)throw new InvalidOperationException("替换英雄定义无效。");var p=Players[seat];p.HeroFormId=form.Id;p.HeroAbilityId=form.AbilityId;if(!form.PreserveHealth)p.HeroMaxHealth=p.Health=form.Health;p.HeroDestroyed=false;if(!form.PreserveArmor)p.Armor=0;p.Armor+=form.ArmorGain;p.Frozen=false;p.FrozenTurn=0;p.TempAttack=0;p.SecretImmuneTurn=-1;p.Shadowform=0;p.PowerUsed=false;p.EmoteText=null;Log.Add(PlayerLabel(seat)+" · 英雄替换："+form.Name+" · 生命 "+p.Health);}
 public string HeroPowerTargetMask(int seat){
  var player=Players[seat];
  var ability=HeroReplacementRules.Ability(player.HeroAbilityId);
  if(ability!=null)return ability.Target;
  if(player.Shadowform>0||player.ClassId=="MAGE"||player.ClassId=="PRIEST")return "ANY_CHARACTER";
  if(player.ClassId=="HUNTER"&&GvgClassRules.Has(this,seat,"GVG_087"))return "HUNTER_POWER";
  return "NONE";
 }
 public bool PowerNeedsTarget(int seat){return HeroPowerTargetMask(seat)!="NONE";}
 static readonly string[] BasicTotems={"VAN_CS2_050","VAN_CS2_051","VAN_CS2_052","VAN_NEW1_009"};
 string[] AvailableBasicTotems(int seat){
  return BasicTotems.Where(id=>!Players[seat].Board.Any(unit=>unit.CardId==id)).ToArray();
 }
 internal string HeroPowerAvailability(int seat){
  var player=Players[seat];
  if(player.PowerUsed)return "本回合已使用英雄技能。";
  if(player.Mana<HeroPowerCost(seat))return "英雄技能法力不足。";
  var ability=HeroReplacementRules.Ability(player.HeroAbilityId);
  if(ability!=null)return ability.Requirement==null?null:ability.Requirement(this,seat);
  if(player.Shadowform>0)return null;
  bool full=player.Board.Count>=Mode.BoardSlots;
  if(player.ClassId=="PALADIN"&&full)return "战场已满。";
  if(player.ClassId=="SHAMAN"&&(full||AvailableBasicTotems(seat).Length==0))return "没有可召唤的基础图腾。";
  return null;
 }
 void UseRegisteredHeroPower(int seat,MatchTarget target,HeroAbilityDefinition ability){
  var player=Players[seat];
  player.Mana-=ability.Cost;
  player.PowerUsed=true;
  Log.Add(PlayerLabel(seat)+" · 英雄技能："+ability.Name+"（"+ability.Cost+"费）");
  DamageBatch(()=>ability.Apply(this,seat,target));
  Cleanup();
 }
 public void DestroyHero(int seat){Players[seat].HeroDestroyed=true;Players[seat].Health=0;}
}
}
