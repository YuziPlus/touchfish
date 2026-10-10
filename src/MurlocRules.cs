using System.Linq;

namespace Touchfish {
public static class MurlocSummonRules {
 public static readonly string[] CardIds={"EX1_509"};
 public static void Resolve(MatchEngine game,BattleUnit summoned,BattleUnit[] listeners){
  if(summoned==null||summoned.Race!="MURLOC")return;
  foreach(var source in listeners){
   if(source.Silenced||source.Health<=0||!game.Players[source.Owner].Board.Contains(source))continue;
   game.Buff(new MatchTarget(source.Owner,source.Id),1,0);
   game.Log.Add("玩家 "+(source.Owner+1)+" · 鱼人招潮者获得 +1 攻击力");
  }
 }
}
}
