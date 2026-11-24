using System;
using System.Linq;
using System.Diagnostics;
namespace Touchfish {
public static class TurnClockRules {
 public const int DurationMs=15000;
 public static bool Active(MatchEngine game){return game.MulliganComplete&&!game.Finished&&game.Players.SelectMany(p=>p.Board).Any(u=>u.Health>0&&!u.Silenced&&MatchRules.EffectKey(game.Card(u.CardId))=="EX1_560");}
}
public sealed partial class MatchEngine {
 public Func<long> ClockNowMs=()=> (long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency);
 bool clockReplica,clockEnabled;long clockDeadline;int clockTurn=-1,clockSequence;
 public int TurnClockSequence{get{SyncTurnClock();return clockSequence;}}
 public bool TurnClockEnabled{get{SyncTurnClock();return clockEnabled;}}
 public int TurnClockRemainingMs{get{SyncTurnClock();return clockEnabled?(int)Math.Max(0,Math.Min(TurnClockRules.DurationMs,clockDeadline-ClockNowMs())):-1;}}
 public bool TurnClockExpired{get{return TurnClockEnabled&&TurnClockRemainingMs==0;}}
 public void SyncTurnClock(){if(clockReplica)return;bool enabled=TurnClockRules.Active(this);if(enabled){if(!clockEnabled||clockTurn!=Turn){clockSequence++;clockDeadline=ClockNowMs()+TurnClockRules.DurationMs;}}else if(clockEnabled){clockSequence++;clockDeadline=0;}clockEnabled=enabled;clockTurn=Turn;}
 public void ReceiveTurnClock(int sequence,int remainingMs){if(sequence<0||remainingMs< -1||remainingMs>TurnClockRules.DurationMs)throw new System.IO.InvalidDataException("回合计时无效。");clockReplica=true;clockSequence=sequence;clockTurn=Turn;clockEnabled=remainingMs>=0;clockDeadline=clockEnabled?ClockNowMs()+remainingMs:0;}
 public void LimitTurnClock(int remainingMs){if(clockEnabled&&remainingMs>=0)clockDeadline=Math.Min(clockDeadline,ClockNowMs()+remainingMs);}
 public bool TryEndTimedTurn(){SyncTurnClock();if(clockReplica||HasPendingDecision||!TurnClockExpired||AnimationPlaying)return false;int logPosition=Log.Count;var result=EndTurn();if(result.Success)Log.Insert(logPosition,"倒计时结束 · 自动结束回合");return result.Success;}
}
public static class ClassicDragonRules {
 public static void Alexstrasza(EffectContext c){var player=c.Game.Players[c.Target.Seat];player.Health=Math.Min(15,c.Game.HeroHealthLimit(c.Target.Seat));}
}
}
