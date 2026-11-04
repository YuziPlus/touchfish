using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace Touchfish {
public partial class PanelWindow {
 readonly MatchVisualCursor secretNoticeCursor=new MatchVisualCursor();
 DispatcherTimer secretNoticeTimer;string secretNoticeText,secretNoticePrevious,secretNoticeSession;int secretNoticeViewer=-1;
 string SecretToolTipText(int seat,int index=-1){int count=match.Players[seat].SecretCount;string header="奥秘数量:"+count;if(seat!=matchViewSeat)return header;var ids=match.SecretCards(seat);if(index>=0)ids=index<ids.Length?new[]{ids[index]}:new string[0];return header+String.Concat(ids.Select(id=>"\n"+match.Card(id).Name+"\n"+match.Card(id).Text));}
 ToolTip SecretToolTip(string text){return new ToolTip{Content=new TextBlock{Text=text,FontSize=10,FontWeight=FontWeights.Normal,TextWrapping=TextWrapping.Wrap,MaxWidth=260},Padding=new Thickness(6)};}
 void AddSecretMarkerToolTips(TextBlock text,int seat){int count=Math.Min(5,match.Players[seat].SecretCount);if(count==0)return;string label=text.Text;if(label.Length<count)return;text.Inlines.Clear();text.Inlines.Add(new Run(label.Substring(0,label.Length-count)));for(int i=0;i<count;i++){var mark=new Run("?");ToolTipService.SetToolTip(mark,SecretToolTip(SecretToolTipText(seat,i)));text.Inlines.Add(mark);}}
 void ClearOwnSecretNotice(){if(secretNoticeTimer!=null){secretNoticeTimer.Stop();secretNoticeTimer=null;}if(secretNoticeText!=null&&status.Text==secretNoticeText)status.Text=secretNoticePrevious??"";secretNoticeText=null;}
 void SetMatchStatus(string text){if(secretNoticeText!=null){secretNoticePrevious=text;status.Text=secretNoticeText;}else status.Text=text;}
 void PresentOwnSecretNotices(){if(match==null)return;if(secretNoticeSession!=match.VisualSession||secretNoticeViewer!=matchViewSeat||awaitHandoff){ClearOwnSecretNotice();secretNoticeSession=match.VisualSession;secretNoticeViewer=matchViewSeat;}var events=secretNoticeCursor.Take(match.VisualSession,match.VisualEvents,currentPage=="match"&&!awaitHandoff);var names=events.Where(e=>(e.Kind=="secret-armed"||e.Kind=="secret-stolen")&&e.RecipientSeat==matchViewSeat&&e.Seat==matchViewSeat).Select(e=>(e.Kind=="secret-stolen"?"科赞秘术师夺取奥秘：":"疯狂的科学家置入奥秘：")+e.CardName).ToArray();if(names.Length==0)return;ClearOwnSecretNotice();secretNoticePrevious=status.Text;secretNoticeText=String.Join("、",names);status.Text=secretNoticeText;secretNoticeTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};secretNoticeTimer.Tick+=(s,e)=>ClearOwnSecretNotice();secretNoticeTimer.Start();}
}
}
