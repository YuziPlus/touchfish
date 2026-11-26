using System;
using System.Linq;

namespace Touchfish {
public static class OverloadRules {
 public static void Queue(MatchEngine game,int seat,int amount){if(amount<=0)return;var player=game.Players[seat];player.Overload+=amount;game.Log.Add(game.PlayerLabel(seat)+" · 过载 +"+amount+"（下回合累计 "+player.Overload+"）");ConditionalTriggerRules.Resolve(game,MatchTriggerTiming.OverloadQueued,seat,amount:amount,cleanup:false);}
 public static void StartTurn(MatchEngine game,int seat){var player=game.Players[seat];player.LockedMana=Math.Min(player.MaxMana,Math.Max(0,player.Overload));player.Overload=0;player.Mana=Math.Max(0,player.MaxMana-player.LockedMana);}
 public static void GainTemporaryMana(MatchEngine game,int seat,int amount){var player=game.Players[seat];player.Mana=Math.Min(Math.Max(0,game.Mode.ManaLimit-player.LockedMana),player.Mana+amount);}
 public static string ResourceLabel(MatchPlayer player,bool minimal){return player.Mana+" / "+player.MaxMana+(player.LockedMana>0?" · 锁"+player.LockedMana:"")+(player.Overload>0?(minimal?" · 下锁":" · 下回合锁")+player.Overload:"");}
 public static bool IsLocked(MatchPlayer player,int slot){return slot<player.MaxMana&&slot>=player.MaxMana-player.LockedMana;}
 public static bool IsFilled(MatchPlayer player,int slot){if(IsLocked(player,slot))return false;int before=slot>=player.MaxMana?player.LockedMana:0;return slot-before<player.Mana;}
}
public sealed partial class MatchEngine {
 public void ForkedLightning(int seat){DamageRandomMinions(1-seat,2,SpellDamageAmount(seat,2));}
}
}
