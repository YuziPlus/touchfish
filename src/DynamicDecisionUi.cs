using System.Windows;
using System.Windows.Controls;

namespace Touchfish {
public partial class PanelWindow {
 FrameworkElement DynamicDecisionOverlay(){
  var decision=match.ObserveDecision(matchViewSeat);
  if(decision==null){
   livePrompt.Text="等待决策";
   return T("等待对方选择…",10,Muted);
  }

  var panel=new StackPanel();
  livePrompt.Text=decision.Prompt;
  var options=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};
  foreach(var option in decision.Options){
   var title=T((option.Cost.HasValue?option.Cost.Value+"费  ":"")+option.Title,10,Ink);
   title.TextTrimming=TextTrimming.CharacterEllipsis;
   var description=T(option.Description,9,Muted);
   description.TextWrapping=minimal?TextWrapping.NoWrap:TextWrapping.Wrap;
   if(minimal)description.TextTrimming=TextTrimming.CharacterEllipsis;
   var content=new StackPanel();
   content.Children.Add(title);
   content.Children.Add(description);
   var button=Btn("",100);
   button.Width=double.NaN;
   button.Height=double.NaN;
   button.HorizontalContentAlignment=HorizontalAlignment.Left;
   button.Content=content;
   button.Margin=new Thickness(2);
   button.Padding=new Thickness(3,1,3,1);
   button.ToolTip=option.Description;
   button.Tag=option.Id;
   button.IsEnabled=CanSubmitDecision(decision.Seat,decision.Id);
   var source=match;
   button.Click+=(s,e)=>{if(match==source)SubmitLocalDecision(decision.Seat,decision.Id,option.Id);};
   options.Children.Add(button);
  }
  panel.Children.Add(options);
  return panel;
 }

 bool CanSubmitDecision(int seat,int decisionId){
  if(match==null||match.Finished||match.AnimationPlaying||awaitHandoff||lanMode&&(!lanReady||lanWaiting))return false;
  var view=match.ObserveDecision(seat);
  return view!=null&&view.Id==decisionId;
 }

 void SubmitLocalDecision(int seat,int decisionId,string optionId){
  if(!CanSubmitDecision(seat,decisionId))return;
  if(lanMode){
   if(seat==matchViewSeat)DoMatch("decision",decisionId,choice:optionId);
   return;
  }
  bool aiChoice=agentMatch&&seat==1&&localAgent!=null;
  if(!aiChoice&&seat!=matchViewSeat)return;
  var result=AgentBridge.SubmitDecision(match,seat,decisionId,optionId);
  if(testReplay!=null)RecordTestReplay(result,new ReplayAction{Kind="decision",Seat=seat,DecisionId=decisionId,OptionId=optionId});
  ApplyMatchAction(result);
 }
}
}
