using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace Touchfish {
public partial class PanelWindow {
 ComboBox localOpponentMode;DispatcherTimer agentTimer;IMatchAgent localAgent;bool agentMatch,agentLobbyMode;
 int agentTurn=-1,agentActions;string agentSession;
 void BuildAgentPicker(Grid heading){
  localOpponentMode=Picker(MatchAgents.All.Select(d=>new Choice(d.Id,d.Name)).ToArray());localOpponentMode.SelectedIndex=0;localOpponentMode.Height=22;localOpponentMode.HorizontalAlignment=HorizontalAlignment.Stretch;localOpponentMode.FontSize=10;heading.Children.Add(LobbyField("对手",localOpponentMode));localOpponentMode.SelectionChanged+=(s,e)=>{RefreshAgentLobby();UpdateLanModeFields();};
  agentTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};agentTimer.Tick+=(s,e)=>TickAgent();agentTimer.Start();Closed+=(s,e)=>agentTimer.Stop();
 }
 LocalAgentDefinition ChosenAgent(){return localOpponentMode!=null&&localOpponentMode.SelectedItem is Choice?MatchAgents.Find(((Choice)localOpponentMode.SelectedItem).Key):null;}
 bool AgentSelected(){return agentLobbyMode&&ChosenAgent()!=null;}
 bool HumanAiSelected(){var selected=lanModePicker==null?null:lanModePicker.SelectedItem as Choice;return selected!=null&&selected.Key=="ai";}
 void StartHumanAiMatch(){
  if(lanPeer!=null||lanMode){status.Text="请先离开局域网房间。";return;}
  try{var definition=ChosenAgent();if(definition==null)throw new InvalidOperationException("请选择人机对手。");
   var first=LobbyDeck(lanClass,lanDeck,true);agentLobbyMode=true;myName.Text=lanName.Text;
   ConfigureLocalOpponent(first,definition.Deck(catalog));CloseTestReplay();localTest=true;testTakeOpen=emoteOpen=false;selectedMulligan.Clear();matchViewSeat=0;matchHandScroll=0;awaitHandoff=false;ClearPending();inspectMatch=false;RenderMatch();ShowPage("match");Record("开始"+AgentName()+"人机对局");
  }catch(Exception ex){if(!(ex is InvalidOperationException||ex is System.IO.InvalidDataException))throw;lanNotice.Text=ex.Message;}
 }
 string AgentName(){return match==null?"人机":match.PlayerLabel(1);}
 void RefreshAgentLobby(){if(opponentClass==null||opponentDeck==null||opponentName==null||lobbyNotice==null)return;var definition=AgentSelected()?ChosenAgent():null;bool ai=definition!=null;opponentDeck.ItemsSource=ai?new[]{new Choice(definition.Id+"-ai",definition.Name.Replace(" AI","")+" · 固定30张")}:myDeck.ItemsSource;opponentDeck.SelectedIndex=0;if(ai){opponentClass.SelectedItem=Classes.First(c=>c.Key==definition.ClassId);opponentName.Text=definition.Name;}opponentDeck.IsEnabled=opponentName.IsEnabled=!ai;SyncChosenDeckClass(opponentClass,opponentDeck);lobbyNotice.Text=ai?"对手使用"+definition.Name+"。双方按正常规则开局，随机先后手，需要完整 30 张卡组。AI 只读取己方手牌与公开场面，自动换牌和操作。":strictTest.IsChecked==true?"本地双人轮流操作。按标准法力开局，禁用指定取牌。无需满 30 张。":"本地双人轮流操作。无需满 30 张；双方开局 10 个满水晶，可从剩余牌库指定取牌。仅用于 test。";}
 void ConfigureLocalOpponent(DeckDocument first,DeckDocument second,GameModeDefinition testMode=null){
  var definition=AgentSelected()?ChosenAgent():null;bool ai=definition!=null;IMatchAgent nextAgent=ai?definition.Create():null;int seed=Environment.TickCount;
  var next=new MatchEngine(catalog,first,second,seed,!ai,mode:testMode,startingSeat:ai?new Random(seed).Next(2):0,firstName:myName.Text,secondName:ai?definition.Name:opponentName.Text);
  if(ai){var result=next.Mulligan(1,nextAgent.Mulligan(AgentBridge.Observe(next,1)));if(!result.Success)throw new InvalidOperationException(result.Message);}match=next;agentMatch=ai;localAgent=nextAgent;agentTurn=-1;agentActions=0;agentSession=match.VisualSession;
 }
 void TickAgent(){
  if(!agentMatch||localAgent==null||lanMode||!localTest||match==null||currentPage!="match"||match.Finished||!match.MulliganComplete||match.Active!=1||match.AnimationPlaying)return;
  if(match.HasPendingDecision){
   var decision=match.ObserveDecision(1);
   if(decision==null)return;
   var decisionAgent=localAgent as IDecisionMatchAgent;
   string optionId=decisionAgent==null?decision.Options[0].Id:decisionAgent.ChooseDecision(decision);
   SubmitLocalDecision(1,decision.Id,optionId);
   return;
  }
  if(match.TurnClockExpired){TickTurnClock();return;}
  if(agentSession!=match.VisualSession||agentTurn!=match.Turn){agentSession=match.VisualSession;agentTurn=match.Turn;agentActions=0;}
  var move=agentActions>=50?new AgentMove{Kind="end"}:localAgent.Choose(AgentBridge.Observe(match,1));if(move==null)return;
  var result=AgentBridge.Execute(match,1,move);agentActions++;if(!result.Success){if(match.AnimationPlaying||match.TurnClockExpired)return;result=match.EndTurn();}
  matchViewSeat=0;awaitHandoff=false;ClearPending();inspectMatch=testTakeOpen=emoteOpen=false;RenderMatch();Record(match.Log.LastOrDefault()??"人机操作");status.Text=match.Finished?result.Message:match.Active==1?AgentName()+" 正在操作":"轮到你操作";
 }
}
}
