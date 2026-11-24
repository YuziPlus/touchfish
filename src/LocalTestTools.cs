using System;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Touchfish {
public partial class PanelWindow {
 ComboBox testTakePicker;Button testTakeButton;
 FrameworkElement LiveTestTake(){
  var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(13)});grid.RowDefinitions.Add(new RowDefinition());grid.Children.Add(T("test · 从剩余牌库取到手牌（最多十张）",9,Muted));
  var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(38)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});
  var p=match.Players[matchViewSeat];var choices=p.Deck.GroupBy(id=>id).OrderBy(g=>match.Card(g.Key).Cost).ThenBy(g=>match.Card(g.Key).Name).Select(g=>new Choice(g.Key,match.Card(g.Key).Cost+"费 · "+match.Card(g.Key).Name+" ×"+g.Count())).ToArray();
  testTakePicker=Picker(choices);testTakePicker.Height=21;testTakePicker.SelectedIndex=choices.Length>0?0:-1;row.Children.Add(testTakePicker);
  testTakeButton=Btn("取牌",38);testTakeButton.Height=21;testTakeButton.FontSize=9;testTakeButton.IsEnabled=CanMatchInput()&&choices.Length>0&&p.Hand.Count<10;testTakeButton.Click+=(s,e)=>TakeTestCard();Grid.SetColumn(testTakeButton,1);row.Children.Add(testTakeButton);
  var back=Btn("返回",34);back.Height=21;back.FontSize=9;back.Click+=(s,e)=>{testTakeOpen=false;RenderMatch();};Grid.SetColumn(back,2);row.Children.Add(back);Put(grid,row,1);return grid;
 }
 void TakeTestCard(){if(!localTest||lanMode||match==null||!match.TestMode||!CanMatchInput())return;var choice=testTakePicker.SelectedItem as Choice;if(choice==null)return;
  var result=match.TestTakeCard(matchViewSeat,choice.Key);
  if(testReplay!=null)RecordTestReplay(result,new ReplayAction{Kind="take",Seat=matchViewSeat,CardId=choice.Key});
  ApplyMatchAction(result);}
}
}
