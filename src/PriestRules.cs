using System;
using System.Linq;
namespace Touchfish {
public static class PriestRules {
 public static readonly string[] PassiveIds={"CS2_235","EX1_335","EX1_341","EX1_350","EX1_591"};
 public static void Apply(EffectContext c){var g=c.Game;switch(c.Card.BaseId){
  case "CS1_113":case "EX1_091":g.TakePermanentControl(c.Seat,c.Target);break;
  case "CS2_003":g.CopyPriestCards(c.Seat,g.Players[1-c.Seat].Hand.Select(h=>h.CardId).ToArray(),1);break;
  case "EX1_339":g.CopyPriestCards(c.Seat,g.Players[1-c.Seat].Deck.ToArray(),2);break;
  case "EX1_345":var candidates=g.Players[1-c.Seat].Deck.Where(id=>g.Card(id).Type=="MINION").ToArray();g.Summon(c.Seat,candidates.Length==0?"VAN_EX1_345t":candidates[g.RandomIndex(candidates.Length)]);break;
  case "EX1_623":g.Buff(c.Target,0,3);break;
  case "EX1_625":var p=g.Players[c.Seat];if(p.Shadowform<2){p.HeroAbilityId=null;p.Shadowform++;p.PowerUsed=false;p.PowersThisTurn=0;}break;
 }}
 public static void Lightwell(MatchTriggerContext c){var g=c.Game;var targets=g.Characters(c.Source.Owner).Where(t=>t.Hero?g.Players[t.Seat].Health<g.HeroHealthLimit(t.Seat):g.Unit(t).DamageTaken>0).ToArray();var target=g.RandomTarget(targets);if(target!=null)g.RestoreHealth(c.Source.Owner,target,3);}
}
public sealed partial class MatchEngine {
 int spellSource=-1,spellMultiplier,spellBonus;bool spellConverts;
 public void ApplyCardEffect(EffectContext c){if(c.Target!=null&&!c.Target.Hero){var target=LivingUnit(c.Target);if(target==null){Log.Add(PlayerLabel(c.Seat)+" · 目标已离开战场，卡牌效果失效");return;}}int oldSource=spellSource,oldMultiplier=spellMultiplier,oldBonus=spellBonus;bool oldConverts=spellConverts;try{if(c.Card.Type=="SPELL"){spellSource=-1;spellMultiplier=AbilityMultiplier(c.Seat);spellBonus=SpellPower(c.Seat);spellConverts=ConvertsHealing(c.Seat);spellSource=c.Seat;}if(c.Card.Type=="MINION")deferSummonDepth++;try{c.Game.DamageBatch(()=>Rules.Effect(c.Card).Apply(c));}finally{if(c.Card.Type=="MINION")deferSummonDepth--;}}finally{spellSource=oldSource;spellMultiplier=oldMultiplier;spellBonus=oldBonus;spellConverts=oldConverts;}}
 public int AbilityMultiplier(int seat){if(spellSource==seat)return spellMultiplier;int count=Players[seat].Board.Count(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="EX1_350");return 1<<count;}
 public int SpellDamageAmount(int seat,int amount){return Math.Max(0,amount+(spellSource==seat?spellBonus:SpellPower(seat)))*AbilityMultiplier(seat);}
 public bool ConvertsHealing(int seat,bool useSpellSnapshot=true){if(useSpellSnapshot&&spellSource==seat)return spellConverts;return Players[seat].Board.Any(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="EX1_591");}
 public void RestoreHealth(int seat,MatchTarget target,int amount,bool enhanced=false,bool spell=false){if(amount<=0)return;if(ConvertsHealing(seat,spell)){Damage(target,spell?SpellDamageAmount(seat,amount):amount*(enhanced?AbilityMultiplier(seat):1));}else Heal(target,amount*(enhanced?AbilityMultiplier(seat):1));}
 public void CopyPriestCards(int seat,string[] candidates,int count){var available=candidates.ToList();int copied=0;for(int n=0;n<count&&available.Count>0;n++){int pick=random.Next(available.Count);string id=available[pick];available.RemoveAt(pick);var p=Players[seat];if(p.Hand.Count<Mode.HandLimit){NoteHistory(HistoryKind.Unmodeled,seat);p.Hand.Add(new HandCard{Id=nextId++,CardId=id,Generated=true});copied++;}else RecordGraveyard(seat,seat,id,nextId++,GraveyardReason.Burned);}Log.Add(PlayerLabel(seat)+" · 获得卡牌复制 "+copied+" 张。");}
 public void TakePermanentControl(int seat,MatchTarget target){var unit=Unit(target);if(unit==null||Players[seat].Board.Count>=Mode.BoardSlots)return;Players[unit.Owner].Board.Remove(unit);unit.Owner=unit.OriginalOwner=seat;unit.ReturnSeat=unit.ReturnTurn=-1;unit.SummonTurn=Turn;unit.AttacksUsed=0;Players[seat].Board.Add(unit);RefreshAuras();}
 public string PowerLabel(int seat){var p=Players[seat];var ability=HeroReplacementRules.Ability(p.HeroAbilityId);if(ability!=null)return ability.Name;return p.Shadowform>0?(p.Shadowform==1?"心灵尖刺":"心灵碎裂"):"英雄技能";}
}
}
