using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace Touchfish {
public partial class PanelWindow {
 DispatcherTimer turnClockTimer;TextBlock turnClockLabel;
 void BuildTurnClock(){turnClockTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};turnClockTimer.Tick+=(s,e)=>TickTurnClock();turnClockTimer.Start();Closed+=(s,e)=>turnClockTimer.Stop();}
 void TickTurnClock(){if(TickSealed())return;if(match==null)return;bool changed=false,nextArenaStarted=false,spellHit=false;if(lanMode){if(lanIsHost&&lanReady&&lanAuthority!=null&&lanPeer!=null&&lanPeer.IsConnected){changed=lanAuthority.PollTimeout();if(changed){spellHit=lanAuthority.LastPollResolvedSpell;nextArenaStarted=lanAuthority.LastPollStartedArenaGame;if(nextArenaStarted){match=lanAuthority.Game;lanArenaGameNumber=lanAuthority.ArenaGameNumber;lanArenaWins0=lanAuthority.ArenaWins0;lanArenaWins1=lanAuthority.ArenaWins1;lanArenaSeriesWinner=lanAuthority.ArenaSeriesWinner;ClearPending();inspectMatch=false;selectedMulligan.Clear();matchHandScroll=0;awaitHandoff=false;}lanRevision=lanAuthority.Revision;PublishLan(new ActionResult{Success=true,Message=spellHit?"炎爆术命中":nextArenaStarted?"竞技赛 · 第 "+lanArenaGameNumber+" 局开始":match.Finished&&lanArenaMode?"竞技赛 · "+ArenaSeriesLabel():"倒计时结束 · 自动换边"});}}}else if(!match.Finished){int actingSeat=match.Active;spellHit=match.PollSpellImpact();changed=spellHit||match.TryEndTimedTurn();if(changed&&testReplay!=null)RecordTestReplay(new ActionResult{Success=true},new ReplayAction{Kind=spellHit?"spell-impact":"timeout",Seat=actingSeat});if(changed&&!spellHit){matchViewSeat=agentMatch?0:match.Active;handoffSeat=match.Active;awaitHandoff=false;}}
  if(changed){ClearPending();inspectMatch=testTakeOpen=emoteOpen=false;RenderMatch();status.Text=spellHit?"炎爆术命中":nextArenaStarted?"竞技赛 · 第 "+lanArenaGameNumber+" 局开始":lanMode&&lanArenaMode&&match.Finished?"竞技赛 · "+ArenaSeriesLabel():"倒计时结束 · 自动换边";}UpdateTurnClockLabel();}
 void UpdateTurnClockLabel(){if(turnClockLabel==null||match==null)return;int remaining=match.TurnClockRemainingMs;turnClockLabel.Text=remaining<0?"":Math.Ceiling(remaining/1000.0)+"s";turnClockLabel.Visibility=remaining<0?Visibility.Collapsed:Visibility.Visible;}
}
}
