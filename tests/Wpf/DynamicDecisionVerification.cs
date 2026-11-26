using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Touchfish {
public partial class PanelWindow {
 public void PreviewDynamicDecisions(string folder){
  previewRun=true;
  LeaveLan();
  Directory.CreateDirectory(folder);
  int before=checks;
  var fixture=new DynamicDecisionFixture(catalog);
  ApplyTheme(true,true);
  for(int mode=0;mode<2;mode++){
   bool compact=mode==1;
   ApplyMode(compact);
   match=fixture.NewGame(31);
   localTest=true;
   agentMatch=false;
   matchViewSeat=0;
   awaitHandoff=false;
   ShowPage("match");
   BeginCard(9000);
   var first=match.ObserveDecision(0);
   var buttons=DecisionButtons(livePanel).ToArray();
   Check(first!=null&&buttons.Length==3&&!CanMatchInput(),"Decision locks ordinary controls in both modes");
   Render(Path.Combine(folder,compact?"decision-minimal.png":"decision-normal.png"));
   Check(Width==(compact?392:548)&&Height==(compact?300:474)&&buttons.All(b=>b.ActualWidth>0&&b.ActualHeight>0),"Fixed dimensions and laid-out options");
   buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   var second=match.ObserveDecision(0);
   Check(second!=null&&second.Id!=first.Id&&match.Players[0].Hand.Count==1,"First click resumes to the next decision");
   buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(match.ObserveDecision(0).Id==second.Id&&match.Players[0].Hand.Count==1,"Stale UI click cannot execute twice");
   DecisionButtons(livePanel).First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(!match.HasPendingDecision&&match.Players[0].Hand.Count==2&&CanMatchInput(),"Second click completes and restores hand");
  }

  match=fixture.NewGame(32,seat:1);
  match.Play(1,9000);
  matchViewSeat=0;
  agentMatch=true;
  localAgent=MatchAgents.Find("freeze").Create();
  RenderMatch();
  Check(!DecisionButtons(livePanel).Any(),"Other seat receives no private option controls");
  TickAgent();
  Check(match.HasPendingDecision&&match.Players[1].Hand.Count==1&&matchViewSeat==0,"Old agent fallback answers only the owning seat's choice");
  TickAgent();
  Check(!match.HasPendingDecision&&match.Players[1].Hand.Count==2&&matchViewSeat==0,"Old agent fallback completes sequential choice");
  File.WriteAllText(Path.Combine(folder,"dynamic-decisions-verification.txt"),"PASS: "+(checks-before)+" WPF checks. Routed Click, no physical mouse.");
 }

 static System.Collections.Generic.IEnumerable<Button> DecisionButtons(DependencyObject root,string[] optionIds=null){
  var button=root as Button;
  if(button!=null&&button.Tag is string&&(optionIds==null?((string)button.Tag).StartsWith("pick:",StringComparison.Ordinal):optionIds.Contains((string)button.Tag)))yield return button;
  var scroll=root as ScrollViewer;
  if(scroll!=null&&scroll.Content is DependencyObject){
   foreach(var result in DecisionButtons((DependencyObject)scroll.Content,optionIds))yield return result;
   yield break;
  }
  var panel=root as Panel;
  if(panel!=null){
   foreach(UIElement child in panel.Children)foreach(var result in DecisionButtons(child,optionIds))yield return result;
   yield break;
  }
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
   foreach(var result in DecisionButtons(VisualTreeHelper.GetChild(root,i),optionIds))yield return result;
 }
}
}
