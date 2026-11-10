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
  case "EX1_303":var sacrifice=g.Unit(c.Target);int damage=g.SpellDamageAmount(c.Seat,g.AttackValue(sacrifice));sacrifice.DamageTaken=sacrifice.MaxHealth;foreach(var target in g.Characters(1-c.Seat,false).ToArray())g.Damage(target,damage);break;
  case "EX1_304":g.ConsumeNeighbors(c.Seat,c.Summoned);break;
  case "EX1_313":g.Damage(new MatchTarget(c.Seat),5);break;
  case "EX1_317":g.SenseDemons(c.Seat);break;
  case "EX1_320":int dealt=g.Damage(c.Target,g.SpellDamageAmount(c.Seat,3));bool killed=c.Target.Hero?g.Players[c.Target.Seat].Health<=0:g.Unit(c.Target).Health<=0;if(dealt>0&&killed){g.Cleanup();g.Summon(c.Seat,BaneDemons[g.RandomIndex(BaneDemons.Length)]);}break;
  case "EX1_596":var unit=g.Unit(c.Target);if(unit.Owner==c.Seat&&unit.Race=="DEMON")g.Buff(c.Target,2,2);else g.Damage(c.Target,g.SpellDamageAmount(c.Seat,2));break;
  case "NEW1_003":if(c.Target.Hero)g.DestroyHero(c.Target.Seat);else{var demon=g.Unit(c.Target);demon.DamageTaken=demon.MaxHealth;}g.RestoreHealth(c.Seat,new MatchTarget(c.Seat),5,true,true);break;
 }}
}
public sealed partial class MatchEngine {
 internal int RandomIndex(int count){return random.Next(count);}
 public void ConsumeNeighbors(int seat,BattleUnit source){var board=Players[seat].Board;int at=board.IndexOf(source);var adjacent=board.Where((u,i)=>Math.Abs(i-at)==1&&u.Health>0).ToArray();int attack=adjacent.Sum(u=>AttackValue(u)),health=adjacent.Sum(u=>u.Health);foreach(var unit in adjacent)unit.DamageTaken=unit.MaxHealth;source.BuffAttack+=attack;source.BuffHealth+=health;}
 public void SenseDemons(int seat){for(int n=0;n<2;n++){var p=Players[seat];var choices=p.Deck.Select((id,i)=>new{Id=id,Index=i}).Where(c=>Card(c.Id).Race=="DEMON").ToArray();if(choices.Length==0){GiveNaxxCard(seat,"GAME_WORTHLESS_IMP");continue;}var chosen=choices[random.Next(choices.Length)];p.Deck.RemoveAt(chosen.Index);p.Deck.Insert(0,chosen.Id);Draw(seat,1);}}
 public void EdwinEntrance(BattleUnit unit){EmitVisual("entrance",unit.Owner,unit.Id,Players[unit.Owner].Board.IndexOf(unit),amount:AttackValue(unit),name:"EX1_613");StartEntranceWait();}
}
}
