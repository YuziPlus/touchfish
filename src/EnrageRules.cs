using System.Collections.Generic;
using System.Linq;
namespace Touchfish {
// Frozen Classic card effects. Enrage is derived from current damage, never a permanent buff.
public static class EnrageRules {
 static readonly Dictionary<string,int> attack=new Dictionary<string,int>{{"CS2_221",0},{"EX1_009",5},{"EX1_390",3},{"EX1_393",3},{"EX1_412",1},{"EX1_414",6}};
 public static IEnumerable<string> Ids{get{return attack.Keys;}}
 public static bool Supports(string id){return attack.ContainsKey(id);}
 public static bool Active(BattleUnit unit){return unit!=null&&!unit.Silenced&&unit.DamageTaken>0&&unit.Health>0;}
 public static int AttackBonus(BattleUnit unit,string id){int bonus;return Active(unit)&&attack.TryGetValue(id,out bonus)?bonus:0;}
}
public sealed partial class MatchEngine {
 public bool IsEnraged(BattleUnit unit){return EnrageRules.Active(unit)&&EnrageRules.Supports(Card(unit.CardId).BaseId);}
 public bool HasWindfury(BattleUnit unit){return !unit.Silenced&&Card(unit.CardId).BaseId=="GVG_111t"||unit.Windfury||IsEnraged(unit)&&Card(unit.CardId).BaseId=="EX1_412";}
 public int WeaponAttackValue(int seat){var p=Players[seat];if(p.WeaponDurability<=0)return 0;return System.Math.Max(0,p.WeaponAttack+2*p.Board.Count(u=>IsEnraged(u)&&Card(u.CardId).BaseId=="CS2_221"));}
}
}
