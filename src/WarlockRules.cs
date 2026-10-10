using System;
using System.Linq;
namespace Touchfish {
public static class WarlockRules {
 // Frozen 2014 pool; the broader collectible-demon pool was introduced in 2.4.0.
 public static readonly string[] BaneDemons={"VAN_CS2_059","VAN_CS2_065","VAN_EX1_319","VAN_CS2_064","VAN_EX1_306","VAN_EX1_301"};
 public static void Apply(EffectContext c){var g=c.Game;switch(c.Card.BaseId){
  case "CS2_063":g.DelayDestroy(c.Target,true,c.Seat);break;
  case "CS2_064":foreach(var target in g.Characters(0).Concat(g.Characters(1)).Where(t=>t.Hero||t.UnitId!=c.Summoned.Id).ToArray())g.Damage(target,1);break;
  case "EX1_301":var p=g.Players[c.Seat];if(p.MaxMana>0){bool filled=p.Mana>=p.MaxMana-p.LockedMana;p.MaxMana--;p.LockedMana=Math.Min(p.LockedMana,p.MaxMana);if(filled)p.Mana=Math.Max(0,p.Mana-1);}break;
  case "EX1_303":var sacrifice=g.Unit(c.Target);int damage=g.AttackValue(sacrifice)+g.SpellPower(c.Seat);sacrifice.DamageTaken=sacrifice.MaxHealth;foreach(var target in g.Characters(1-c.Seat,false).ToArray())g.Damage(target,damage);break;
  case "EX1_304":g.ConsumeNeighbors(c.Seat,c.Summoned);break;
  case "EX1_313":g.Damage(new MatchTarget(c.Seat),5);break;
  case "EX1_317":g.SenseDemons(c.Seat);break;
  case "EX1_320":int dealt=g.Damage(c.Target,3+g.SpellPower(c.Seat));bool killed=c.Target.Hero?g.Players[c.Target.Seat].Health<=0:g.Unit(c.Target).Health<=0;if(dealt>0&&killed){g.Cleanup();g.Summon(c.Seat,BaneDemons[g.RandomIndex(BaneDemons.Length)]);}break;
  case "EX1_596":var unit=g.Unit(c.Target);if(unit.Owner==c.Seat&&unit.Race=="DEMON")g.Buff(c.Target,2,2);else g.Damage(c.Target,2+g.SpellPower(c.Seat));break;
  case "NEW1_003":var demon=g.Unit(c.Target);demon.DamageTaken=demon.MaxHealth;g.Heal(new MatchTarget(c.Seat),5);break;
 }}
}
public sealed partial class MatchEngine {
 internal int RandomIndex(int count){return random.Next(count);}
 public void ConsumeNeighbors(int seat,BattleUnit source){var board=Players[seat].Board;int at=board.IndexOf(source);var adjacent=board.Where((u,i)=>Math.Abs(i-at)==1&&u.Health>0).ToArray();int attack=adjacent.Sum(u=>AttackValue(u)),health=adjacent.Sum(u=>u.Health);foreach(var unit in adjacent)unit.DamageTaken=unit.MaxHealth;source.BuffAttack+=attack;source.BuffHealth+=health;}
 public void SenseDemons(int seat){var p=Players[seat];int drawn=0,generated=0;for(int n=0;n<2;n++){var candidates=p.Deck.Select((deckCard,i)=>new{Id=deckCard,Index=i}).Where(c=>Card(c.Id).Race=="DEMON").ToArray();string id;if(candidates.Length==0){id="GAME_WORTHLESS_IMP";generated++;}else{var pick=candidates[random.Next(candidates.Length)];id=pick.Id;p.Deck.RemoveAt(pick.Index);drawn++;}if(p.Hand.Count<10)p.Hand.Add(new HandCard{Id=nextId++,CardId=id,Generated=candidates.Length==0});else{RecordGraveyard(seat,seat,id,nextId++,GraveyardReason.Burned);Log.Add("玩家 "+(seat+1)+" · 感知恶魔：手牌已满，失去一张牌");}}if(drawn>0)Log.Add("玩家 "+(seat+1)+" · 感知恶魔抽牌 "+drawn+" 张");if(generated>0)Log.Add("玩家 "+(seat+1)+" · 感知恶魔生成 "+generated+" 张小鬼");}
 public void EdwinEntrance(BattleUnit unit){EmitVisual("entrance",unit.Owner,unit.Id,Players[unit.Owner].Board.IndexOf(unit),amount:AttackValue(unit),name:"EX1_613");StartEntranceWait();}
}
}
