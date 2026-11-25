using System.Windows;
using System.Windows.Controls;
namespace Touchfish {
public partial class PanelWindow {
 FrameworkElement TournamentTotemPicker(){
  var panel=new System.Windows.Controls.Primitives.UniformGrid{Columns=4};
  foreach(var id in MatchEngine.TournamentTotems){
   string selected=id;
   var button=Btn(match.Card(id).Name,70);
   button.Width=double.NaN;button.FontSize=9;button.Height=22;button.Padding=new Thickness(1);
   button.Click+=(s,e)=>DoMatch("power",choice:selected);
   panel.Children.Add(button);
  }
  return panel;
 }
}
}
