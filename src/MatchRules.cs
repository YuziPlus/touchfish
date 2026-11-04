using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Touchfish {
public sealed class MatchRules {
 readonly Dictionary<string,CardEffect> effects=new Dictionary<string,CardEffect>();readonly CardCatalog catalog;
 public readonly List<ConditionalTrigger> Triggers=new List<ConditionalTrigger>(ConditionalTriggerRules.Definitions);
 public void RegisterTrigger(ConditionalTrigger trigger){if(trigger==null||trigger.Condition==null||trigger.Apply==null||String.IsNullOrWhiteSpace(trigger.CardId))throw new ArgumentException("触发定义不完整。");Triggers.Add(trigger);}
 public IEnumerable<string> RegisteredIds{get{return effects.Keys;}}
 static readonly string[] SimpleMechanics={"TAUNT","CHARGE","DIVINE_SHIELD","WINDFURY","POISONOUS","STEALTH","SPELLPOWER","CANT_ATTACK","CANT_BE_TARGETED_BY_ABILITIES","ELUSIVE","OVERLOAD"};
 public MatchRules(CardCatalog source){catalog=source;
  Register("GAME_COIN","NONE",c=>OverloadRules.GainTemporaryMana(c.Game,c.Seat,1));
  Damage("CS2_029",6);Damage("CS1_130",2);Damage("CS2_008",1);Damage("DS1_185",2);Damage("EX1_238",3);Damage("EX1_241",5);Damage("EX1_279",10);Damage("CS2_057",4,"MINION");Damage("CS2_072",2,"UNDAMAGED_MINION");
  Register("CS2_024","ANY_CHARACTER",c=>{Hit(c,3);c.Game.Freeze(c.Target);});Register("CS2_037","ENEMY_CHARACTER",c=>{Hit(c,1);c.Game.Freeze(c.Target);});Register("CS2_031","ANY_CHARACTER",c=>{var unit=c.Game.Unit(c.Target);bool frozen=c.Target.Hero?c.Game.Players[c.Target.Seat].Frozen:unit.Frozen;if(frozen)Hit(c,4);else c.Game.Freeze(c.Target);});
  Register("CS2_022","MINION",c=>c.Game.Transform(c.Target,"VAN_CS2_tk1"));
  Register("CS2_023","NONE",c=>c.Game.Draw(c.Seat,2));Register("CS2_077","NONE",c=>c.Game.Draw(c.Seat,4));
  Register("EX1_103","NONE",c=>{foreach(var u in c.Game.Players.SelectMany(p=>p.Board).Where(u=>u.Id!=c.Summoned.Id&&u.Race=="MURLOC"&&u.Health>0).ToArray())c.Game.Buff(new MatchTarget(u.Owner,u.Id),0,2);});
  Register("EX1_050","NONE",c=>{c.Game.Draw(c.Seat,2);c.Game.Draw(1-c.Seat,2);});
  Register("CS2_025","NONE",c=>Area(c,1-c.Seat,1,false));Register("CS2_032","NONE",c=>Area(c,1-c.Seat,4,false));Register("CS2_028","NONE",c=>{Area(c,1-c.Seat,2,false);foreach(var target in c.Game.Characters(1-c.Seat,false))c.Game.Freeze(target);});Register("CS2_026","NONE",c=>{foreach(var target in c.Game.Characters(1-c.Seat,false))c.Game.Freeze(target);});
  Register("CS2_027","NONE",c=>{c.Game.Summon(c.Seat,"VAN_CS2_mirror");c.Game.Summon(c.Seat,"VAN_CS2_mirror");},(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"战场已满。":null);
  Register("EX1_277","NONE",c=>Missiles(c,3));Register("EX1_384","NONE",c=>Missiles(c,8));
  Register("CS2_013","NONE",c=>DruidSpellRules.WildGrowth(c.Game,c.Seat));
  Register("EX1_158","NONE",c=>DruidSpellRules.SoulOfForest(c.Game,c.Seat));
  Register("EX1_578","MINION",c=>Hit(c,c.Game.HeroAttack(c.Seat)));
  Register("GAME_EXCESS_MANA","NONE",c=>c.Game.Draw(c.Seat,1));
  Register("EX1_251","NONE",c=>c.Game.ForkedLightning(c.Seat),(g,s)=>g.Players[1-s].Board.Count<2?"对方至少需要两个随从。":null);
  Register("CS2_007","ANY_CHARACTER",c=>c.Game.RestoreHealth(c.Seat,c.Target,8,c.Card.Type=="SPELL",c.Card.Type=="SPELL"));Register("CS2_089","ANY_CHARACTER",c=>c.Game.RestoreHealth(c.Seat,c.Target,6,c.Card.Type=="SPELL",c.Card.Type=="SPELL"));Register("EX1_354","ANY_CHARACTER",c=>{c.Game.RestoreHealth(c.Seat,c.Target,8,c.Card.Type=="SPELL",c.Card.Type=="SPELL");c.Game.Draw(c.Seat,3);});
  Register("CS2_005","NONE",c=>{c.Game.Players[c.Seat].Armor+=2;c.Game.Players[c.Seat].TempAttack+=2;});Register("EX1_570","NONE",c=>{c.Game.Players[c.Seat].Armor+=4;c.Game.Players[c.Seat].TempAttack+=4;});Register("EX1_169","NONE",c=>OverloadRules.GainTemporaryMana(c.Game,c.Seat,2));
  Register("CS2_009","MINION",c=>{c.Game.Buff(c.Target,2,2);c.Game.Unit(c.Target).Taunt=true;});Register("CS2_011","NONE",c=>{c.Game.Players[c.Seat].TempAttack+=2;foreach(var unit in c.Game.Players[c.Seat].Board)unit.TempAttack+=2;});
  Register("CS2_012","ENEMY_CHARACTER",c=>{Hit(c,4);foreach(var target in c.Game.Characters(1-c.Seat))if(target.UnitId!=c.Target.UnitId)c.Game.Damage(target,c.Game.SpellDamageAmount(c.Seat,1));});
  Register("EX1_173","ANY_CHARACTER",c=>{Hit(c,5);c.Game.Draw(c.Seat,1);});Register("EX1_161","MINION",c=>{Kill(c);c.Game.Draw(1-c.Seat,2);});
  Register("CS2_039","MINION",c=>c.Game.Unit(c.Target).Windfury=true);Register("CS2_041","MINION",c=>{c.Game.RestoreHealth(c.Seat,c.Target,c.Game.Unit(c.Target).DamageTaken,c.Card.Type=="SPELL",c.Card.Type=="SPELL");c.Game.Unit(c.Target).Taunt=true;});Register("CS2_045","FRIENDLY_CHARACTER",c=>{if(c.Target.Hero)c.Game.Players[c.Seat].TempAttack+=3;else c.Game.Unit(c.Target).TempAttack+=3;});Register("CS2_046","NONE",c=>{foreach(var unit in c.Game.Players[c.Seat].Board)unit.TempAttack+=3;});
  Register("EX1_245","MINION",c=>{c.Game.Silence(c.Target);Hit(c,1);});Register("EX1_248","NONE",c=>{c.Game.Summon(c.Seat,"VAN_EX1_tk11");c.Game.Summon(c.Seat,"VAN_EX1_tk11");},(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"战场已满。":null);
  Register("EX1_259","NONE",c=>{foreach(var target in c.Game.Characters(1-c.Seat,false)){c.Game.Damage(target,c.Game.SpellDamageAmount(c.Seat,2+c.Game.RandomIndex(2)));}});
  Register("CS2_061","ANY_CHARACTER",c=>{Hit(c,2);c.Game.RestoreHealth(c.Seat,new MatchTarget(c.Seat),2,c.Card.Type=="SPELL",c.Card.Type=="SPELL");});Register("CS2_062","NONE",c=>{Area(c,0,3,true);Area(c,1,3,true);});Register("EX1_308","ANY_CHARACTER",c=>{Hit(c,4);c.Game.DiscardRandom(c.Seat,1);});
  Register("EX1_306","NONE",c=>c.Game.DiscardRandom(c.Seat,1));Register("EX1_310","NONE",c=>c.Game.DiscardRandom(c.Seat,2));Register("NEW1_030","NONE",c=>c.Game.DeathwingBattlecry(c.Seat,c.Summoned));
  Register("EX1_302","MINION",c=>{Hit(c,1);if(c.Game.Unit(c.Target).Health<=0)c.Game.Draw(c.Seat,1);});Register("EX1_309","MINION",c=>{Kill(c);c.Game.RestoreHealth(c.Seat,new MatchTarget(c.Seat),3,c.Card.Type=="SPELL",c.Card.Type=="SPELL");});Register("EX1_312","NONE",c=>{foreach(var target in c.Game.Characters(0,false).Concat(c.Game.Characters(1,false)))c.Game.Unit(target).DamageTaken=c.Game.Unit(target).MaxHealth;});
  Register("CS2_073","MINION",ComboRules.Apply);Register("EX1_124","ANY_CHARACTER",ComboRules.Apply);Register("CS2_075","NONE",c=>c.Game.Damage(new MatchTarget(1-c.Seat),c.Game.SpellDamageAmount(c.Seat,3)));Register("CS2_076","ENEMY_MINION",Kill);Register("EX1_581","ENEMY_MINION",c=>c.Game.ReturnToHand(c.Target));
  Register("EX1_131","NONE",ComboRules.Apply);Register("EX1_133","ANY_CHARACTER",ComboRules.Apply);Register("EX1_134","NONE",ComboRules.Apply);Register("EX1_137","NONE",ComboRules.Apply);Register("EX1_613","NONE",ComboRules.Apply);Register("NEW1_005","NONE",ComboRules.Apply);
  Register("CS2_074","NONE",c=>c.Game.Players[c.Seat].WeaponAttack+=2,(g,s)=>g.Players[s].WeaponDurability==0?"需要先装备武器。":null);Register("EX1_278","ANY_CHARACTER",c=>{Hit(c,1);c.Game.Draw(c.Seat,1);});Register("EX1_129","NONE",c=>{Area(c,1-c.Seat,1,false);c.Game.Draw(c.Seat,1);});
  Register("CS2_084","MINION",c=>c.Game.SetHealth(c.Target,1));Register("EX1_539","ANY_CHARACTER",c=>Hit(c,c.Game.Players[c.Seat].Board.Any(m=>m.Race=="BEAST")?5:3));Register("EX1_617","NONE",c=>{var target=c.Game.RandomTarget(c.Game.Characters(1-c.Seat,false));if(target!=null)c.Game.Unit(target).DamageTaken=c.Game.Unit(target).MaxHealth;},(g,s)=>g.Players[1-s].Board.Count==0?"对方没有随从。":null);
  Register("NEW1_031","NONE",c=>{c.Game.Summon(c.Seat,new[]{"VAN_NEW1_032","VAN_NEW1_033","VAN_NEW1_034"}[c.Game.RandomIndex(3)]);},(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"战场已满。":null);
  Register("CS2_087","MINION",c=>c.Game.Buff(c.Target,3,0));Register("CS2_092","MINION",c=>c.Game.Buff(c.Target,4,4));Register("EX1_371","MINION",c=>c.Game.Unit(c.Target).Shield=true);Register("CS2_093","NONE",c=>Area(c,1-c.Seat,2,true));Register("CS2_094","ANY_CHARACTER",c=>{Hit(c,3);c.Game.Draw(c.Seat,1);});Register("EX1_619","NONE",c=>{foreach(var target in c.Game.Characters(0,false).Concat(c.Game.Characters(1,false)))c.Game.SetHealth(target,1);});Register("EX1_360","MINION",c=>{var unit=c.Game.Unit(c.Target);unit.BuffAttack=1-unit.BaseAttack;});Register("EX1_355","MINION",c=>{var unit=c.Game.Unit(c.Target);unit.BuffAttack+=c.Game.AttackValue(unit);});
  Register("DS1_233","NONE",c=>c.Game.Damage(new MatchTarget(1-c.Seat),c.Game.SpellDamageAmount(c.Seat,5)));Register("CS2_004","MINION",c=>{c.Game.Buff(c.Target,0,2);c.Game.Draw(c.Seat,1);});Register("CS2_234","LOW_ATTACK",Kill);Register("EX1_622","HIGH_ATTACK",Kill);Register("CS2_236","MINION",c=>c.Game.Buff(c.Target,0,c.Game.Unit(c.Target).Health));Register("CS1_129","MINION",c=>{var unit=c.Game.Unit(c.Target);unit.BuffAttack=unit.Health-unit.BaseAttack-unit.TempAttack-EnrageRules.AttackBonus(unit,c.Game.Card(unit.CardId).BaseId)-c.Game.AuraAttack(unit);});Register("EX1_332","MINION",c=>c.Game.Silence(c.Target));
  Register("CS1_112","NONE",c=>{Area(c,1-c.Seat,2,true);foreach(var target in c.Game.Characters(c.Seat))c.Game.RestoreHealth(c.Seat,target,2,c.Card.Type=="SPELL",c.Card.Type=="SPELL");});Register("EX1_621","NONE",c=>{foreach(var target in c.Game.Characters(0,false).Concat(c.Game.Characters(1,false)))c.Game.RestoreHealth(c.Seat,target,4,c.Card.Type=="SPELL",c.Card.Type=="SPELL");});Register("EX1_624","ANY_CHARACTER",c=>{Hit(c,5);c.Game.RestoreHealth(c.Seat,new MatchTarget(c.Seat),5,c.Card.Type=="SPELL",c.Card.Type=="SPELL");});Register("EX1_626","NONE",c=>{foreach(var target in c.Game.Characters(1-c.Seat,false))c.Game.Silence(target);c.Game.Draw(c.Seat,1);});
  Register("CS2_105","NONE",c=>c.Game.Players[c.Seat].TempAttack+=4);Register("EX1_606","NONE",c=>{c.Game.Players[c.Seat].Armor+=5;c.Game.Draw(c.Seat,1);});Register("EX1_400","NONE",c=>{Area(c,0,1,false);Area(c,1,1,false);});Register("CS2_108","ENEMY_DAMAGED_MINION",Kill,(g,s)=>null);
  Register("EX1_391","MINION",c=>{Hit(c,2);if(c.Game.Unit(c.Target).Health>0)c.Game.Draw(c.Seat,1);});Register("EX1_408","ANY_CHARACTER",c=>Hit(c,c.Game.Players[c.Seat].Health<=12?6:4));Register("EX1_410","MINION",c=>Hit(c,c.Game.Players[c.Seat].Armor));Register("EX1_607","MINION",c=>{Hit(c,1);c.Game.Buff(c.Target,2,0);});Register("CS2_103","FRIENDLY_MINION",c=>{c.Game.Buff(c.Target,2,0);c.Game.Unit(c.Target).Charge=true;});
  Register("EX1_562","NONE",c=>c.Game.SummonAround(c.Seat,c.Summoned,"GAME_WHELP"));
  // Battlecries are explicit and do not inherit spell damage bonuses.
  Register("EX1_506","NONE",c=>c.Game.Summon(c.Seat,"VAN_EX1_506a",c.Game.Players[c.Seat].Board.IndexOf(c.Summoned)+1));
  Register("CS2_196","NONE",c=>c.Game.Summon(c.Seat,"VAN_CS2_boar",c.Game.Players[c.Seat].Board.IndexOf(c.Summoned)+1));
  Register("CS2_151","NONE",c=>c.Game.Summon(c.Seat,"VAN_CS2_152",c.Game.Players[c.Seat].Board.IndexOf(c.Summoned)+1));
  Register("EX1_066","NONE",c=>c.Game.BreakWeapon(1-c.Seat));Register("EX1_015","NONE",c=>c.Game.Draw(c.Seat,1));Register("CS2_147","NONE",c=>c.Game.Draw(c.Seat,1));Register("EX1_284","NONE",c=>c.Game.Draw(c.Seat,1));
  Register("CS2_189","ANY_CHARACTER",c=>c.Game.Damage(c.Target,1));Register("CS2_141","ANY_CHARACTER",c=>c.Game.Damage(c.Target,1));Register("CS2_150","ANY_CHARACTER",c=>c.Game.Damage(c.Target,2));Register("EX1_011","ANY_CHARACTER",c=>c.Game.RestoreHealth(c.Seat,c.Target,2,c.Card.Type=="SPELL",c.Card.Type=="SPELL"));Register("CS2_117","ANY_CHARACTER",c=>c.Game.RestoreHealth(c.Seat,c.Target,3,c.Card.Type=="SPELL",c.Card.Type=="SPELL"));
  Register("EX1_019","FRIENDLY_MINION",c=>c.Game.Buff(c.Target,1,1));Register("CS2_203","MINION",c=>c.Game.Silence(c.Target));Register("EX1_048","MINION",c=>c.Game.Silence(c.Target));Register("EX1_093","NONE",c=>Adjacent(c,1,1,true));Register("EX1_058","NONE",c=>Adjacent(c,0,0,true));
  Register("EX1_046","MINION",c=>c.Game.Unit(c.Target).TempAttack+=2);Register("CS2_188","MINION",c=>c.Game.Unit(c.Target).TempAttack+=2);Register("EX1_603","MINION",c=>{c.Game.Damage(c.Target,1);c.Game.Buff(c.Target,2,0);});Register("EX1_319","NONE",c=>c.Game.Damage(new MatchTarget(c.Seat),3));
  Register("DS1_055","NONE",c=>{foreach(var target in c.Game.Characters(c.Seat))c.Game.RestoreHealth(c.Seat,target,2,c.Card.Type=="SPELL",c.Card.Type=="SPELL");});Register("EX1_583","NONE",c=>c.Game.RestoreHealth(c.Seat,new MatchTarget(c.Seat),4,c.Card.Type=="SPELL",c.Card.Type=="SPELL"));Register("EX1_593","NONE",c=>c.Game.Damage(new MatchTarget(1-c.Seat),3));
  foreach(string id in new[]{"EX1_170","EX1_556","EX1_096","EX1_029","EX1_012","CS2_033","EX1_162","CS2_122","DS1_175"})Register(id,"NONE",c=>{});
  foreach(string id in TurnEndRules.Ids.Where(id=>id!="EX1_316"&&id!="EX1_334"&&id!="EX1_571"))Register(id,"NONE",c=>{});
  Register("EX1_316","FRIENDLY_MINION",c=>{c.Game.Buff(c.Target,4,4);c.Game.DelayDestroy(c.Target,false,c.Seat);});
  Register("EX1_334","ENEMY_LOW_ATTACK",c=>c.Game.TakeTemporaryControl(c.Seat,c.Target),(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"己方战场已满。":null);
  Register("EX1_571","NONE",c=>{for(int n=0;n<3;n++)c.Game.Summon(c.Seat,"VAN_EX1_tk9b");},(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"己方战场已满。":null);
  Register("EX1_tk9b","NONE",c=>{});Register("NEW1_009","NONE",c=>{});
  Register("DREAM_02","NONE",c=>{int amount=c.Game.SpellDamageAmount(c.Seat,5);foreach(var target in c.Game.Characters(0).Concat(c.Game.Characters(1)).Where(t=>t.Hero||c.Game.Card(c.Game.Unit(t).CardId).BaseId!="EX1_572").ToArray())c.Game.Damage(target,amount);});
  Register("DREAM_04","MINION",c=>c.Game.ReturnToHand(c.Target));Register("DREAM_05","MINION",c=>{c.Game.Buff(c.Target,5,5);c.Game.DelayDestroy(c.Target,true,c.Seat);});
  foreach(string id in GiantRules.Ids)Register(id,"NONE",c=>{});
  foreach(string id in AuraRules.Ids)Register(id,"NONE",c=>{});
  foreach(string id in MurlocSummonRules.CardIds)Register(id,"NONE",c=>{});
  Register("EX1_362","FRIENDLY_MINION",c=>c.Game.Unit(c.Target).Shield=true);
  Register("EX1_363","MINION",c=>{var unit=c.Game.Unit(c.Target);if(c.Seat==0)unit.WisdomBlessings0++;else unit.WisdomBlessings1++;});
  Register("EX1_349","NONE",c=>{int difference=c.Game.Players[1-c.Seat].Hand.Count-c.Game.Players[c.Seat].Hand.Count;if(difference>0)c.Game.Draw(c.Seat,difference);});
  Register("EX1_365","ANY_CHARACTER",c=>{var player=c.Game.Players[c.Seat];if(player.Deck.Count==0){c.Game.Draw(c.Seat,1);return;}int cost=c.Game.Card(player.Deck[0]).Cost;c.Game.Draw(c.Seat,1);c.Game.Damage(c.Target,c.Game.SpellDamageAmount(c.Seat,cost));});
  Register("EX1_382","ENEMY_MINION",c=>{var unit=c.Game.Unit(c.Target);unit.BuffAttack=1-unit.BaseAttack;});
  Register("EX1_558","NONE",c=>{var opponent=c.Game.Players[1-c.Seat];int durability=opponent.WeaponDurability;if(opponent.WeaponId!=null&&durability>0){c.Game.BreakWeapon(1-c.Seat);c.Game.Draw(c.Seat,durability);}});
  Register("EX1_116","NONE",c=>{c.Game.Summon(1-c.Seat,"GAME_WHELP");c.Game.Summon(1-c.Seat,"GAME_WHELP");});
  foreach(string id in new[]{"EX1_383","EX1_110","EX1_016"})Register(id,"NONE",c=>{});
  foreach(string id in DeathrattleRules.CardIds)Register(id,"NONE",c=>{});
  foreach(string id in ConditionalTriggerRules.CardIds)Register(id,"NONE",c=>{});
  Register("NEW1_029","NONE",c=>TimedCostRules.GrantFreeSpells(c.Game,1-c.Seat,c.Game.Turn+1));
  foreach(string id in EnrageRules.Ids)Register(id,"NONE",c=>{});
  Register("CS2_063","ENEMY_MINION",WarlockRules.Apply);Register("CS2_064","NONE",WarlockRules.Apply);Register("EX1_301","NONE",WarlockRules.Apply);Register("EX1_303","FRIENDLY_MINION",WarlockRules.Apply);Register("EX1_304","NONE",WarlockRules.Apply);Register("EX1_313","NONE",WarlockRules.Apply);Register("EX1_317","NONE",WarlockRules.Apply);Register("EX1_320","ANY_CHARACTER",WarlockRules.Apply);Register("EX1_596","MINION",WarlockRules.Apply);Register("NEW1_003","DEMON_CHARACTER",WarlockRules.Apply);
  Register("CS2_233","NONE",RogueRules.Apply,(g,s)=>g.Players[s].WeaponDurability<=0?"需要先装备武器。":null);Register("EX1_126","ENEMY_MINION",RogueRules.Apply);Register("EX1_128","NONE",RogueRules.Apply);Register("EX1_144","FRIENDLY_MINION",RogueRules.Apply);Register("EX1_145","NONE",RogueRules.Apply);Register("EX1_522","NONE",RogueRules.Apply);Register("NEW1_004","NONE",RogueRules.Apply);Register("NEW1_014","FRIENDLY_MINION",RogueRules.Apply);Register("NEW1_017","MURLOC_MINION",RogueRules.Apply);
  Register("CS2_104","DAMAGED_MINION",WarriorRules.Apply);Register("CS2_114","NONE",WarriorRules.Apply,(g,s)=>g.Players[1-s].Board.Count<2?"对方至少需要两个随从。":null);
  foreach(string id in new[]{"EX1_084","EX1_392","EX1_398","EX1_409","EX1_411","NEW1_036"})Register(id,"NONE",WarriorRules.Apply);
  Register("EX1_407","NONE",WarriorRules.Apply,(g,s)=>g.Players.Sum(p=>p.Board.Count)<2?"战场至少需要两个随从。":null);
  foreach(string id in SecretRules.Ids)Register(id,"NONE",SecretRules.Apply,(g,s)=>g.SecretRequirement(s,g.Card("VAN_"+id)));
  // Truesilver Champion's heal and Doomhammer's windfury are implemented by the engine.
  Register("CS2_097","NONE",c=>{});
  foreach(string id in new[]{"EX1_154","EX1_155","EX1_160","EX1_164","EX1_165","EX1_166","EX1_178","EX1_573","NEW1_007","NEW1_008"})Register(id,"NONE",ApplyChoice);
  foreach(string id in PriestRules.PassiveIds)Register(id,"NONE",c=>{});
  Register("EX1_345t","NONE",c=>{});
  Register("CS1_113","ENEMY_MINION",PriestRules.Apply,(g,seat)=>g.Players[seat].Board.Count>=g.Mode.BoardSlots?"己方战场已满。":null);
  Register("EX1_091","ENEMY_ATTACK_TWO",PriestRules.Apply,(g,seat)=>g.Players[seat].Board.Count>=g.Mode.BoardSlots-1&&g.HasTargets(seat,"ENEMY_ATTACK_TWO",false)?"需要为夺取的随从保留一个位置。":null);
  foreach(string id in new[]{"CS2_003","EX1_339","EX1_625"})Register(id,"NONE",PriestRules.Apply);
  Register("EX1_345","NONE",PriestRules.Apply,(g,seat)=>g.Players[seat].Board.Count>=g.Mode.BoardSlots?"己方战场已满。":null);
  Register("EX1_623","FRIENDLY_MINION",PriestRules.Apply);
  Register("EX1_275","MINION",MageRules.Apply);Register("EX1_612","NONE",MageRules.Apply);
  Register("CS2_088","NONE",PaladinRules.Apply);Register("EX1_366","NONE",c=>{});
  Register("CS2_038","MINION",ShamanRules.Apply);Register("CS2_042","ANY_CHARACTER",ShamanRules.Apply);Register("CS2_053","NONE",ShamanRules.Apply);Register("EX1_244","NONE",ShamanRules.Apply);Register("EX1_246","MINION",ShamanRules.Apply);Register("EX1_587","FRIENDLY_MINION",ShamanRules.Apply);
  Register("EX1_323","NONE",HeroReplacementRules.Jaraxxus);
    Register("CS2_237","NONE",HunterRules.Apply);Register("DS1_070","FRIENDLY_BEAST",HunterRules.Apply);Register("DS1_183","NONE",HunterRules.Apply,(g,s)=>HunterRules.Requirement(g,s,"DS1_183"));Register("DS1_184","NONE",HunterRules.Apply);Register("DS1_188","NONE",HunterRules.Apply);Register("EX1_531","NONE",HunterRules.Apply);Register("EX1_536","NONE",HunterRules.Apply);Register("EX1_537","ENEMY_MINION",HunterRules.Apply);Register("EX1_538","NONE",HunterRules.Apply);Register("EX1_544","NONE",HunterRules.Apply);Register("EX1_549","FRIENDLY_BEAST",HunterRules.Apply);
  foreach(var trigger in HunterRules.Triggers)RegisterTrigger(trigger);
  foreach(var trigger in LowCostRules.Triggers)RegisterTrigger(trigger);foreach(string id in LowCostRules.Triggers.Select(t=>t.CardId).Distinct())Register(id,"NONE",c=>{});
  Register("CS2_146","NONE",c=>{});Register("EX1_049","FRIENDLY_MINION",LowCostRules.Apply);Register("EX1_059","MINION",LowCostRules.Apply);
  foreach(string id in new[]{"EX1_082","NEW1_016","NEW1_018","NEW1_025"})Register(id,"NONE",LowCostRules.Apply);
  foreach(var trigger in MidCostRules.Triggers)RegisterTrigger(trigger);foreach(string id in MidCostRules.CardIds)Register(id,"NONE",MidCostRules.Apply);
  Register("EX1_005","ATTACK_SEVEN",MidCostRules.Apply);Register("EX1_057","FRIENDLY_MINION",MidCostRules.Apply);Register("GAME_BANANA","MINION",c=>c.Game.Buff(c.Target,1,1));
  foreach(var trigger in HighCostRules.Triggers)RegisterTrigger(trigger);foreach(string id in HighCostRules.CardIds.Concat(HighCostRules.Inventions))Register(id,"NONE",HighCostRules.Apply);
  Register("EX1_002","ENEMY_TAUNT_MINION",HighCostRules.Apply);Register("EX1_283","ANY_CHARACTER",HighCostRules.Apply);Register("EX1_564","MINION",HighCostRules.Apply);
  Register("GAME_I_AM_MURLOC","NONE",HighCostRules.Apply,(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"战场已满。":null);Register("GAME_ROGUES_DO_IT","ANY_CHARACTER",HighCostRules.Apply);Register("PRO_001c","NONE",HighCostRules.Apply,(g,s)=>g.Players[s].Board.Count>=g.Mode.BoardSlots?"战场已满。":null);
  Register("EX1_560","NONE",c=>{});Register("EX1_561","HERO",ClassicDragonRules.Alexstrasza);
  NaxxRules.Register(this);
  GvgRules.Register(this,catalog);
  RuleModules.Configure(this);
 }
 public static string EffectKey(CardRecord card){return !String.IsNullOrEmpty(card.EffectId)?card.EffectId:card.SetId=="classic-2014"||String.IsNullOrEmpty(card.SetId)?card.BaseId:card.Id;}
 public void RegisterExtension(string effectId,string target,Action<EffectContext> apply,Func<MatchEngine,int,string> requirement=null){if(String.IsNullOrWhiteSpace(effectId)||!effectId.Contains(":" )||apply==null||effects.ContainsKey(effectId))throw new ArgumentException("扩展效果必须使用独立命名空间和版本，不能覆盖现有效果。");Register(effectId,target,apply,requirement);}
 public bool IsChoice(CardRecord card,MatchEngine game,int seat){return card!=null&&(card.BaseId==HunterRules.TrackingCardId?HunterRules.TrackingOptions(game,seat).Length>1:IsChoice(card));}
 public bool ValidChoice(CardRecord card,string key,MatchEngine game,int seat){return card!=null&&card.BaseId==HunterRules.TrackingCardId?HunterRules.IsTrackingChoiceValid(game,seat,key):ValidChoice(card,key);}
 public MatchChoiceOption[] Choices(CardRecord card,MatchEngine game,int seat){return card!=null&&card.BaseId==HunterRules.TrackingCardId?HunterRules.TrackingOptions(game,seat):Choices(card);}
 public bool IsChoice(CardRecord card){return card!=null&&Choices(card).Length==2;}
 public bool ValidChoice(CardRecord card,string key){return Choices(card).Any(option=>option.Key==key);}
 public MatchChoiceOption[] Choices(CardRecord card){if(card==null)return new MatchChoiceOption[0];switch(card.BaseId){
  case "EX1_154":return new[]{new MatchChoiceOption("three","造成 3 点伤害","对一个随从造成 3 点伤害。"),new MatchChoiceOption("one-draw","造成 1 点伤害并抽牌","对一个随从造成 1 点伤害，然后抽一张牌。")};
  case "EX1_155":return new[]{new MatchChoiceOption("attack","攻击 +4","使一个随从获得 +4 攻击力。"),new MatchChoiceOption("health-taunt","生命 +4 和嘲讽","使一个随从获得 +4 生命值和嘲讽。")};
  case "EX1_160":return new[]{new MatchChoiceOption("buff","全体 +1/+1","使你的所有随从获得 +1/+1。"),new MatchChoiceOption("panther","召唤猎豹","召唤一个 3/2 猎豹。")};
  case "EX1_164":return new[]{new MatchChoiceOption("mana","获得 2 个法力水晶","获得 2 个法力水晶。"),new MatchChoiceOption("draw","抽 3 张牌","抽三张牌。")};
  case "EX1_165":return new[]{new MatchChoiceOption("charge","冲锋","本随从获得冲锋。"),new MatchChoiceOption("taunt","生命 +2 和嘲讽","本随从获得 +2 生命值和嘲讽。")};
  case "EX1_166":return new[]{new MatchChoiceOption("damage","造成 2 点伤害","对一个随从造成 2 点伤害。"),new MatchChoiceOption("silence","沉默","沉默一个随从。")};
  case "EX1_178":return new[]{new MatchChoiceOption("attack","攻击 +5","本随从获得 +5 攻击力。"),new MatchChoiceOption("health-taunt","生命 +5 和嘲讽","本随从获得 +5 生命值和嘲讽。")};
  case "EX1_573":return new[]{new MatchChoiceOption("buff","其他随从 +2/+2","使你的其他随从获得 +2/+2。"),new MatchChoiceOption("treants","召唤树人","召唤两个 2/2 并具有嘲讽的树人。")};
  case "NEW1_007":return new[]{new MatchChoiceOption("five","造成 5 点伤害","对一个随从造成 5 点伤害。"),new MatchChoiceOption("area","敌方全体 2 点伤害","对所有敌方随从造成 2 点伤害。")};
  case "NEW1_008":return new[]{new MatchChoiceOption("draw","抽 2 张牌","抽两张牌。"),new MatchChoiceOption("heal","恢复 5 点生命值","为你的英雄恢复 5 点生命值。")};
  default:return new MatchChoiceOption[0];}}
 public string Target(CardRecord card,string choice){return Target(card,choice,false);}
 public string Target(CardRecord card,string choice,bool combo){string comboTarget=ComboRules.Target(card,combo);if(comboTarget!=null)return comboTarget;if(!IsChoice(card))return Effect(card).Target;if(!ValidChoice(card,choice))return "NONE";switch(card.BaseId){case "EX1_154":case "EX1_155":case "EX1_166":return "MINION";case "NEW1_007":return choice=="area"?"NONE":"MINION";default:return "NONE";}}
 void ApplyChoice(EffectContext c){switch(c.Card.BaseId){
  case "EX1_154":Hit(c,c.Choice=="three"?3:1);if(c.Choice=="one-draw")c.Game.Draw(c.Seat,1);break;
  case "EX1_155":if(c.Choice=="attack")c.Game.Buff(c.Target,4,0);else{c.Game.Buff(c.Target,0,4);c.Game.Unit(c.Target).Taunt=true;}break;
  case "EX1_160":if(c.Choice=="buff"){foreach(var unit in c.Game.Players[c.Seat].Board.ToArray())c.Game.Buff(new MatchTarget(c.Seat,unit.Id),1,1);}else c.Game.Summon(c.Seat,"GAME_PANTHER");break;
  case "EX1_164":if(c.Choice=="mana"){var player=c.Game.Players[c.Seat];int gained=Math.Min(2,c.Game.Mode.ManaLimit-player.MaxMana);player.MaxMana+=gained;player.Mana=Math.Min(player.MaxMana,player.Mana+gained);}else c.Game.Draw(c.Seat,3);break;
  case "EX1_165":c.Game.Transform(new MatchTarget(c.Seat,c.Summoned.Id),c.Choice=="charge"?"VAN_EX1_165t1":"VAN_EX1_165t2");break;
  case "EX1_166":if(c.Choice=="damage")c.Game.Damage(c.Target,2);else c.Game.Silence(c.Target);break;
  case "EX1_178":if(c.Choice=="attack")c.Summoned.BuffAttack+=5;else{c.Game.Buff(new MatchTarget(c.Seat,c.Summoned.Id),0,5);c.Summoned.Taunt=true;}break;
  case "EX1_573":if(c.Choice=="buff"){foreach(var unit in c.Game.Players[c.Seat].Board.Where(m=>m.Id!=c.Summoned.Id).ToArray())c.Game.Buff(new MatchTarget(c.Seat,unit.Id),2,2);}else{int at=c.Game.Players[c.Seat].Board.IndexOf(c.Summoned)+1;c.Game.Summon(c.Seat,"GAME_TREANT_TAUNT",at);c.Game.Summon(c.Seat,"GAME_TREANT_TAUNT",at+1);}break;
  case "NEW1_007":if(c.Choice=="five")Hit(c,5);else Area(c,1-c.Seat,2,false);break;
  case "NEW1_008":if(c.Choice=="draw")c.Game.Draw(c.Seat,2);else c.Game.RestoreHealth(c.Seat,new MatchTarget(c.Seat),5,c.Card.Type=="SPELL",c.Card.Type=="SPELL");break;
 }}
 internal void Register(string id,string target,Action<EffectContext> action,Func<MatchEngine,int,string> requirement=null){effects[id]=new CardEffect{Target=target,Apply=action,Requirement=requirement};}
 void Damage(string id,int amount,string target="ANY_CHARACTER"){Register(id,target,c=>Hit(c,amount));}
 static void Hit(EffectContext context,int amount){context.Game.Damage(context.Target,context.Game.SpellDamageAmount(context.Seat,amount));}
 static void Kill(EffectContext context){var unit=context.Game.Unit(context.Target);unit.DamageTaken=unit.MaxHealth;}
 static void Area(EffectContext context,int seat,int amount,bool hero){foreach(var target in context.Game.Characters(seat,hero))context.Game.Damage(target,context.Game.SpellDamageAmount(context.Seat,amount));}
 static void Missiles(EffectContext context,int amount){for(int n=0;n<context.Game.SpellDamageAmount(context.Seat,amount);n++){var target=context.Game.RandomTarget(context.Game.Characters(1-context.Seat).Where(t=>t.Hero||context.Game.Unit(t).Health>0));if(target!=null){context.Game.DamageSequenceStep(target,1);if(context.Game.Finished)break;}}}
 static void Adjacent(EffectContext context,int attack,int health,bool taunt){var board=context.Game.Players[context.Seat].Board;int index=board.IndexOf(context.Summoned);foreach(int place in new[]{index-1,index+1})if(place>=0&&place<board.Count){context.Game.Buff(new MatchTarget(context.Seat,board[place].Id),attack,health);if(taunt)board[place].Taunt=true;}}
 public bool Supports(CardRecord card){if(!String.IsNullOrEmpty(card.EffectId))return effects.ContainsKey(card.EffectId);if(card.Id=="GAME_COIN")return true;if(effects.ContainsKey(EffectKey(card)))return true;if(card.Type!="MINION"&&card.Type!="WEAPON")return false;if(card.Mechanics!=null&&card.Mechanics.Any(m=>!SimpleMechanics.Contains(m)))return false;string text=Regex.Replace(card.Text??"",@"法术伤害\+\d+|过载[：:]?\s*[（(]\d+[）)]|嘲讽|冲锋|圣盾|风怒|剧毒|潜行|无法攻击|不能攻击|扰魔|无法成为法术或英雄技能的目标","");return Regex.Replace(text,@"[\s，。；,:;.（）()]+","").Length==0;}
 public CardEffect Effect(CardRecord card){CardEffect effect;if(effects.TryGetValue(EffectKey(card),out effect))return effect;return new CardEffect{Apply=c=>{}};}
 public void Death(MatchEngine game,BattleUnit unit){ConditionalTriggerRules.SourceDied(game,unit);}
 public DeckDocument TrainingDeck(string classId){var pool=catalog.Pool("classic-2014");var deck=new DeckDocument{ClassId=classId,PoolId=pool.Id,Name="基础练习 · "+classId};var all=catalog.Deckable(pool);var selected=all.Where(c=>c.CardClass==classId&&c.Rarity!="LEGENDARY"&&Supports(c)).Take(8).Concat(all.Where(c=>c.CardClass=="NEUTRAL"&&c.Rarity!="LEGENDARY"&&c.Cost>=1&&c.Cost<=5&&Supports(c))).Take(15);foreach(var card in selected)deck.Cards[card.Id]=2;DeckRules.Validate(catalog,deck,true);return deck;}
}
}
