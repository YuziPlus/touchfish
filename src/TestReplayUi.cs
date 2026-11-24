using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Touchfish {
public partial class PanelWindow {
 CheckBox recordNextTest;
 TestReplay testReplay;
 MatchEngine recordedTest;
 string replayFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YuziPlus","Touchfish","replays");}}

 void AddTestReplayControls(Grid heading){
  var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
  strictTest=new CheckBox{Content="严格 test",Foreground=Ink,FontSize=10,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};
  strictTest.Checked+=(s,e)=>RefreshAgentLobby();
  strictTest.Unchecked+=(s,e)=>RefreshAgentLobby();
  controls.Children.Add(strictTest);
  recordNextTest=new CheckBox{Content="录制新局",Foreground=Ink,FontSize=10,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)};
  controls.Children.Add(recordNextTest);
  var open=Btn("录像",46);
  open.Height=20;
  open.Click+=(s,e)=>OpenTestReplay();
  controls.Children.Add(open);
  heading.Children.Add(controls);
  Closed+=(s,e)=>CloseTestReplay();
 }
 string StartTestReplay(DeckDocument first,DeckDocument second){
  CloseTestReplay();
  if(previewRun||!localTest||lanMode||agentMatch||!TestReplay.Supports(match.Mode)||recordNextTest.IsChecked!=true)return null;
  try{
   Directory.CreateDirectory(replayFolder);
   var path=Path.Combine(replayFolder,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+match.VisualSession.Substring(0,8)+".jsonl");
   testReplay=new TestReplay(path,match,AppRelease.Version,first,second);
   recordedTest=match;
   return "正在录制 · "+Path.GetFileName(path);
  }catch(IOException){return "录像创建失败。";}
   catch(UnauthorizedAccessException){return "录像目录不可写。";}
 }
 void CloseTestReplay(){
  var previous=testReplay;
  testReplay=null;
  recordedTest=null;
  if(previous!=null)try{previous.Dispose();}catch(IOException){}
 }
 void RecordTestReplay(ActionResult result,ReplayAction action){
  if(testReplay==null||match!=recordedTest||lanMode||!localTest||!result.Success)return;
  try{
   testReplay.Append(match,action);
   if(match.Finished)CloseTestReplay();
  }catch(IOException){
   CloseTestReplay();
   Dispatcher.BeginInvoke(new Action(()=>status.Text="录像写入失败，已停止录制。"));
  }
 }
 void OpenTestReplay(){
  if(!localTest||lanMode||agentLobbyMode)return;
  var picker=new Microsoft.Win32.OpenFileDialog{Title="打开 test 录像",Filter="Touchfish 录像 (*.jsonl)|*.jsonl"};
  if(Directory.Exists(replayFolder))picker.InitialDirectory=replayFolder;
  if(picker.ShowDialog(this)!=true)return;
  try{
   var frames=TestReplay.Load(picker.FileName);
   var panel=new DockPanel{Margin=new Thickness(10)};
   var nav=new StackPanel{Orientation=Orientation.Horizontal};
   var previous=Btn("上一步",55);
   var next=Btn("下一步",55);
   var caption=T("",10,Muted);
   nav.Children.Add(previous);
   nav.Children.Add(next);
   nav.Children.Add(caption);
   DockPanel.SetDock(nav,Dock.Top);
   panel.Children.Add(nav);
   var body=T("",11,Ink);
   body.TextWrapping=TextWrapping.Wrap;
   body.VerticalAlignment=VerticalAlignment.Top;
   panel.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   var viewer=new Window{Owner=this,Title="test · 录像",Width=Width,Height=Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel,Background=B("#FFFFFF"),FontFamily=FontFamily};
   int index=0;
   Action render=()=>{
    var frame=frames[index];
    previous.IsEnabled=index>0;
    next.IsEnabled=index+1<frames.Count;
    caption.Text="  "+index+" / "+(frames.Count-1);
    var action=frame.Action;
    string detail=action==null?"起手":DescribeReplayAction(frames[index-1],frame);
    string result=frame.Winner==-2?"":frame.Winner==-1?"\n平局":"\n"+frame.Players[frame.Winner].Public.DisplayName+" 获胜";
    body.Text=detail+" · 第 "+((frame.Turn+1)/2)+" 回合"+result+"\n行动方："+frame.Players[frame.Active].Public.DisplayName+" · 离线复盘"+"\n\n"+String.Join("\n\n",frame.Players.Select(DescribeReplayPlayer))+"\n\n"+String.Join("\n",frame.Log);
   };
   previous.Click+=(s,e)=>{index--;render();};
   next.Click+=(s,e)=>{index++;render();};
   render();
   ApplyFonts(panel);
   viewer.ShowDialog();
  }catch(Exception ex){
   if(!(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException||ex is InvalidOperationException||ex is System.Runtime.Serialization.SerializationException))throw;
   lobbyNotice.Text="无法打开录像："+ex.Message;
  }
 }
 string DescribeReplayAction(ReplayFrame before,ReplayFrame after){
  var action=after.Action;
  var player=before.Players[action.Seat];
  string label=player.Public.DisplayName+" · "+ReplayActionLabel(action.Kind);
  if(action.Kind=="play"){
   var hand=player.Hand.FirstOrDefault(h=>h.Id==action.Source);
   if(hand!=null)label+=" · "+hand.Name;
   if(action.Position>=0)label+=" · 位置 "+(action.Position+1);
  }
  if(action.Kind=="attack"){
   var attacker=player.Board.FirstOrDefault(u=>u.Id==action.Source);
   label+=" · "+(attacker==null?"英雄":attacker.Name);
  }
  if(action.Target!=null&&action.Target.Seat>=0&&action.Target.Seat<before.Players.Length){
   var target=before.Players[action.Target.Seat];
   var unit=target.Board.FirstOrDefault(u=>u.Id==action.Target.UnitId);
   label+=" → "+(unit==null?target.Public.DisplayName:unit.Name);
  }
  return label;
 }
 static string DescribeReplayPlayer(ReplayPlayer player){
  var hero=player.Public;
  return hero.DisplayName+" · "+hero.Health+" 血 · "+hero.Armor+" 甲 · "+hero.Mana+"/"+hero.MaxMana+" 费 · 牌库 "+player.DeckCount+"\n"
   +"战场  "+(player.Board.Length==0?"空":String.Join("  ",player.Board.Select(u=>u.Name+" "+u.Attack+"/"+u.Health+(u.Taunt?" [嘲讽]":"")+(u.Shield?" [圣盾]":"")+(u.Frozen?" [冻结]":""))))+"\n"
   +"手牌  "+(player.Hand.Length==0?"空":String.Join("  ",player.Hand.Select(h=>h.Name+" "+h.Cost+"费")));
 }
 static string ReplayActionLabel(string kind){
  switch(kind){
   case "play":return "出牌";case "attack":return "攻击";case "power":return "英雄技能";case "end":return "结束回合";
   case "mulligan":return "起手换牌";case "take":return "指定取牌";case "decision":return "确认选择";
   case "spell-impact":return "法术命中";case "timeout":return "倒计时结束";case "emote":return "对话";
   default:return kind;
  }
 }
}
}

