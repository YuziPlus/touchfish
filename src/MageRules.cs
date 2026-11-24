using System;
using System.Linq;
namespace Touchfish {
public static class MageRules {
 public static void Apply(EffectContext c){switch(c.Card.BaseId){
  case "EX1_275":var g=c.Game;var board=g.Players[c.Target.Seat].Board;int index=board.FindIndex(u=>u.Id==c.Target.UnitId);var targets=board.Skip(Math.Max(0,index-1)).Take(index==0?2:3).Select(u=>new MatchTarget(u.Owner,u.Id)).ToArray();int damage=g.SpellDamageAmount(c.Seat,1);foreach(var target in targets)g.Freeze(target);foreach(var target in targets)g.Damage(target,damage);break;
  case "EX1_612":c.Game.Players[c.Seat].FreeSecretThisTurn=true;break;
 }}
}
public sealed partial class MatchEngine {
 public void GenerateMageFireball(int seat){var p=Players[seat];if(p.Hand.Count>=Mode.HandLimit){Log.Add(PlayerLabel(seat)+" · 手牌已满，未获得生成法术。");return;}p.Hand.Add(new HandCard{Id=nextId++,CardId="VAN_CS2_029",Generated=true});NoteHistory(HistoryKind.Gain,seat,"VAN_CS2_029");Log.Add(PlayerLabel(seat)+" · 获得生成法术 1 张。");}
}
}
