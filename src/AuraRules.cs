using System;
using System.Linq;
namespace Touchfish {
public static class AuraRules {
public static readonly string[] Ids={"CS2_122","CS2_222","CS2_227","DS1_175","DS1_178","EX1_062","EX1_076","EX1_162","EX1_315","EX1_507","EX1_508","EX1_565","EX1_608","EX1_616","NEW1_027"};
}
public sealed partial class MatchEngine {
 System.Collections.Generic.IEnumerable<BattleUnit> AuraSources(){return Players.SelectMany(p=>p.Board).Where(u=>!u.Silenced&&u.Health>0);}
 bool AdjacentAura(BattleUnit source,BattleUnit target){return source.Owner==target.Owner&&Math.Abs(Players[target.Owner].Board.IndexOf(source)-Players[target.Owner].Board.IndexOf(target))==1;}
 public int AuraAttack(BattleUnit unit){int bonus=0;if(!unit.Silenced&&Card(unit.CardId).BaseId=="EX1_062")bonus+=Players.SelectMany(p=>p.Board).Count(other=>other.Id!=unit.Id&&other.Race=="MURLOC"&&other.Health>0);foreach(var source in AuraSources()){if(source.Id==unit.Id)continue;string id=Card(source.CardId).BaseId;bool own=source.Owner==unit.Owner;if(own&&(id=="CS2_122"||id=="NEW1_033"||id=="CS2_222"))bonus++;if(own&&id=="DS1_175"&&unit.Race=="BEAST")bonus++;if(own&&id=="NEW1_027"&&unit.Race=="PIRATE")bonus++;if(id=="EX1_162"&&AdjacentAura(source,unit))bonus++;if(id=="EX1_565"&&AdjacentAura(source,unit))bonus+=2;if(unit.Race=="MURLOC"){if(id=="EX1_507")bonus+=2;if(id=="EX1_508")bonus++;}}return bonus;}
 public bool HasCharge(BattleUnit unit){return unit.Charge||unit.ReturnTurn==Turn||unit.Race=="BEAST"&&AuraSources().Any(s=>s.Owner==unit.Owner&&Card(s.CardId).BaseId=="DS1_178");}
 // Frozen Classic priority: Portal's floor applies before other cost auras, not as a final clamp.
 public int HandCost(int seat,HandCard hand){return CardCost(seat,Card(hand.CardId),hand.CostAdjustment);}
 public int CardCost(int seat,CardRecord card){return CardCost(seat,card,0);}
 int CardCost(int seat,CardRecord card,int adjustment){var sources=AuraSources().ToArray();int cost=Math.Max(0,card.Cost+adjustment);if(card.Type=="MINION"&&cost>0){int portals=sources.Count(s=>s.Owner==seat&&Card(s.CardId).BaseId=="EX1_315");if(portals>0)cost=Math.Max(1,cost-2*portals);}foreach(var source in sources){string id=Card(source.CardId).BaseId;bool own=source.Owner==seat;if(card.Type=="SPELL"&&own&&id=="EX1_608")cost--;if(card.Type=="MINION"){if(id=="EX1_616")cost++;if(own&&id=="CS2_227")cost+=3;if(own&&id=="EX1_076"&&Players[seat].MinionsPlayedThisTurn==0)cost--;}}return TimedCostRules.Apply(this,seat,card,Math.Max(0,cost-GiantDiscount(seat,card)));}

 public void RefreshAuras(){var all=Players.SelectMany(p=>p.Board).ToArray();var sources=all.Where(u=>!u.Silenced&&u.Health>0).ToArray();var bonuses=all.Select(unit=>sources.Count(source=>source.Id!=unit.Id&&(source.Owner==unit.Owner&&(Card(source.CardId).BaseId=="CS2_222"||Card(source.CardId).BaseId=="NEW1_027"&&unit.Race=="PIRATE")||Card(source.CardId).BaseId=="EX1_507"&&unit.Race=="MURLOC"))).ToArray();for(int i=0;i<all.Length;i++){var unit=all[i];int health=unit.Health,old=unit.AuraHealth;unit.AuraHealth=bonuses[i];if(unit.AuraHealth<old||health<=0)unit.DamageTaken=Math.Max(0,unit.MaxHealth-Math.Min(health,unit.MaxHealth));}}
}
}
