using System;
using System.Linq;
namespace Touchfish {
public static class GiantRules {
 public static readonly string[] Ids={"EX1_105","EX1_586","EX1_620"};
}
public sealed partial class MatchEngine {
 // Intrinsic reductions are evaluated on the pre-play public state, after external cost auras.
 public int GiantDiscount(int seat,CardRecord card){switch(card.BaseId){case "GVG_121":return Players[1-seat].HandCount;case "EX1_105":return Math.Max(0,Players[seat].HandCount-1);case "EX1_586":return Players.Sum(p=>p.Board.Count);case "EX1_620":return Math.Max(0,30-Players[seat].Health);default:return 0;}}
}
}
