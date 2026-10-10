using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Documents;
using System.ComponentModel;
using System.Windows.Data;
using Polygon = System.Windows.Shapes.Polygon;

namespace Touchfish {
public sealed class PaletteColor : INotifyPropertyChanged {Color value;public Color Value{get{return value;}set{this.value=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Value"));}}public event PropertyChangedEventHandler PropertyChanged;}
public class Entry {
 public string Name, Keywords, Text, State, Kind; public int Cost, Attack, Health;
 public Entry(string name, string keywords, int cost, int attack, int health, string text, string state, string kind="随从") {
  Name=name; Keywords=keywords; Cost=cost; Attack=attack; Health=health; Text=text; State=state; Kind=kind;
 }
}
public partial class PanelWindow : Window {
 static readonly Dictionary<string,SolidColorBrush> Palette=new Dictionary<string,SolidColorBrush>();
 static readonly Dictionary<string,PaletteColor> PaletteColors=new Dictionary<string,PaletteColor>();
 static readonly Brush Ink=B("#34383D"), Muted=B("#83888F"), Line=B("#E7E9EC"), Green=B("#387C60");
 Grid shell, main; StackPanel detail; TextBlock detailTitle, detailText, detailMeta, status;
 Border detailBorder; Button compactButton, detailButton, pinButton, gameTab, logTab;
 bool fullEffects;CheckBox fullEffectsOption;
 Grid logPanel; bool fullBoard=true, details=false, log=false; Entry selected;
 List<Border> rows=new List<Border>(); int checks;
 List<FrameworkElement> heroSlots=new List<FrameworkElement>(); List<Polygon> crystals=new List<Polygon>(); List<ScrollViewer> lanes=new List<ScrollViewer>();
 List<Grid> battleLanes=new List<Grid>(); List<Border> battleCards=new List<Border>(); FrameworkElement handLane;
 List<Grid> heroLayouts=new List<Grid>(); Grid settingsPanel; Button settingsTab, wpsOption, codexOption, lightOption, darkOption, normalOption, minimalOption;
 bool minimal, codex, dark, previewRun; string currentPage="game";
 Dictionary<Border,string[]> slotLabels=new Dictionary<Border,string[]>(); List<Border> handCards=new List<Border>();
 Grid detailHeader; Button detailBack;
 public PanelWindow(bool isPreview=false) {
  previewRun=isPreview;
  Title="协作面板 · "+AppRelease.Version; Width=548; Height=474; MinWidth=548; MinHeight=474; FontWeight=FontWeights.Normal;
  WindowStartupLocation=WindowStartupLocation.CenterScreen; WindowStyle=WindowStyle.None;
  ResizeMode=ResizeMode.NoResize; Background=B("#FFFFFF"); FontFamily=new FontFamily("Microsoft YaHei UI");
  FontSize=12; Foreground=Ink; UseLayoutRounding=true; SnapsToDevicePixels=true;
  TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
  var outer=new Border {BorderThickness=new Thickness(0),Background=B("#FFFFFF")}; Content=outer;
  shell=new Grid(); outer.Child=shell;
  foreach(var h in new double[]{28,26,28,1,-1,20}) shell.RowDefinitions.Add(new RowDefinition{Height=h<0?new GridLength(1,GridUnitType.Star):new GridLength(h)});
  Titlebar(); Tabs(); Toolbar(); Put(shell,new Border{Background=Line},3);
  main=new Grid{Margin=new Thickness(8,0,8,0)};
  foreach(var h in new double[]{16,54,-1,20,-1,54,64}) main.RowDefinitions.Add(new RowDefinition{Height=h==-1?new GridLength(1,GridUnitType.Star):new GridLength(h)});
  main.RowDefinitions[2].MinHeight=72;main.RowDefinitions[4].MinHeight=72;
  Put(shell,main,4);
  var enemyHand=new Grid();enemyHand.ColumnDefinitions.Add(new ColumnDefinition());enemyHand.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});enemyHand.Children.Add(T("对方手牌  5",10,Muted));var deck=T("牌库  18",10,Muted);Grid.SetColumn(deck,1);enemyHand.Children.Add(deck);Put(main,enemyHand,0);
  Put(main,HeroArea(false),1);
  Put(main,BattleLane(EnemyEntries(),"对方随从"),2);
  var divider=new Grid();divider.ColumnDefinitions.Add(new ColumnDefinition());divider.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});divider.Children.Add(new Border{Background=Line,Height=1,VerticalAlignment=VerticalAlignment.Center});var turn=T("我的回合   ·   结束回合",10,Muted);turn.Background=B("#FFFFFF");turn.Padding=new Thickness(9,0,0,0);Grid.SetColumn(turn,1);divider.Children.Add(turn);Put(main,divider,3);
  Put(main,BattleLane(FriendlyEntries(),"我方随从"),4);
  Put(main,HeroArea(true),5);
  handLane=Lane(new Entry[]{
   new Entry("火球术","法术",4,0,0,"造成6点伤害。", "","法术"),
   new Entry("寒冰箭","法术",2,0,0,"造成3点伤害，并冻结目标。", "","法术"),
   new Entry("酸性沼泽软泥怪","战吼",2,3,2,"战吼：摧毁对手的武器。", ""),
   new Entry("石拳食人魔","—",6,6,7,"", "")},"手牌  4    /    牌库  16",true);Put(main,handLane,6);
  detail=new StackPanel{Margin=new Thickness(8,2,8,2)};
  detailTitle=T("卡牌详情",11,Ink);
  detailMeta=T("点击名称查看完整效果",10,Muted); detailMeta.Margin=new Thickness(0,1,0,0);
  detailText=T("",11,Muted); detailText.TextWrapping=TextWrapping.Wrap; detailText.Margin=new Thickness(0,2,0,0);
  detailHeader=new Grid{Height=15};detailHeader.Children.Add(detailTitle);detailBack=Btn("返回手牌",60);detailBack.HorizontalAlignment=HorizontalAlignment.Right;detailBack.Height=15;detailBack.Click+=(s,e)=>{if(details)ToggleDetails();};detailHeader.Children.Add(detailBack);
  detail.Children.Add(detailHeader); detail.Children.Add(detailMeta); detail.Children.Add(detailText);
  detailBorder=new Border{Background=B("#F7F8FA"),BorderBrush=Line,BorderThickness=new Thickness(1),Margin=new Thickness(0,2,0,4),Visibility=Visibility.Collapsed,Child=new ScrollViewer{Content=detail,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}}; Put(main,detailBorder,6);
  logPanel=new Grid{Margin=new Thickness(16,15,16,10),Visibility=Visibility.Collapsed};
  logPanel.Children.Add(T("操作记录",13,Ink));
  var hint=T("记录组牌与本地对战操作。",11,Muted); hint.Margin=new Thickness(0,6,0,14); logPanel.Children.Add(hint); Put(shell,logPanel,4);
  var footer=new Grid{Background=B("#F7F8FA")}; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  status=T("视觉预览  ·  未连接",10,Muted); status.Margin=new Thickness(12,0,0,0); footer.Children.Add(status);
  var right=T("静音    100%",10,Muted); right.Margin=new Thickness(0,0,18,0); Grid.SetColumn(right,1); footer.Children.Add(right); Put(shell,footer,5);
  BuildSettings();BuildDeckUi();BuildMatchUi();BuildLanUi();BuildReleaseNotes();BuildRecords();BuildUpdates();ApplyTheme(false,false);LoadPreferences();ShowPage("lan");
  PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){WindowState=WindowState.Minimized;e.Handled=true;} if(e.Key==Key.F2){ToggleBoard();e.Handled=true;}};
 }
 static Brush B(string color){SolidColorBrush brush;if(!Palette.TryGetValue(color,out brush)){var source=new PaletteColor{Value=(Color)ColorConverter.ConvertFromString(color)};PaletteColors[color]=source;brush=new SolidColorBrush();BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Value"){Source=source});Palette[color]=brush;}return brush;}
 static TextBlock T(string value,double size,Brush brush){return new TextBlock{Text=value,FontSize=size,Tag=size,Foreground=brush,VerticalAlignment=VerticalAlignment.Center};}
 static void Put(Grid parent,UIElement child,int row){Grid.SetRow(child,row);parent.Children.Add(child);}
 Button Btn(string text, double width) {
  var b=new Button{Content=text,Width=width,Height=25,Foreground=Muted,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(5,0,5,0),Cursor=Cursors.Hand,FontSize=11};
  b.Template=ButtonTemplate();return b;
 }
 ControlTemplate ButtonTemplate(){
  var template=new ControlTemplate(typeof(Button)); var border=new FrameworkElementFactory(typeof(Border)); border.Name="Surface"; border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Button.BackgroundProperty)); border.SetValue(Border.CornerRadiusProperty,new CornerRadius(2));
  var content=new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center); content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center); border.AppendChild(content); template.VisualTree=border;
  var hover=new Trigger{Property=Button.IsMouseOverProperty,Value=true}; hover.Setters.Add(new Setter(Border.BackgroundProperty,B("#E8ECEF").Clone(),"Surface")); template.Triggers.Add(hover);return template;
 }
 void Titlebar(){
  var grid=new Grid{Background=B("#F2F3F5")}; grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)}); grid.ColumnDefinitions.Add(new ColumnDefinition()); for(int i=0;i<2;i++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});
  var icon=new Border{Width=13,Height=15,BorderBrush=B("#9AA1A9"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(1),Child=T("≡",10,Muted)}; grid.Children.Add(icon);
  var title=T("协作面板 · "+AppRelease.Version,12,Ink); Grid.SetColumn(title,1); grid.Children.Add(title);
  grid.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==1&&(e.OriginalSource==grid||e.OriginalSource==title||e.OriginalSource==icon))DragMove();};
  var min=Btn("—",34); min.Click+=(s,e)=>WindowState=WindowState.Minimized; Grid.SetColumn(min,2);grid.Children.Add(min);
  var close=Btn("×",34);close.FontSize=16;close.Click+=(s,e)=>Close();Grid.SetColumn(close,3);grid.Children.Add(close);Put(shell,grid,0);
 }
 void Tabs(){
  var container=new Border{BorderBrush=Line,BorderThickness=new Thickness(0,0,0,1)};var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(9,0,0,0)};container.Child=row;
  gameTab=Btn("对战",48); logTab=Btn("记录",48); gameTab.Foreground=Green;
  gameTab.Click+=(s,e)=>OpenMatch();logTab.Click+=(s,e)=>SwitchTab(true);deckTab=Btn("组牌",48);deckTab.Click+=(s,e)=>{ClearCodePanel();ShowPage("deck");};row.Children.Add(deckTab);row.Children.Add(gameTab);row.Children.Add(logTab);settingsTab=Btn("设置",48);settingsTab.Click+=(s,e)=>ShowPage("settings");row.Children.Add(settingsTab);releaseTab=Btn("更新日志",64);releaseTab.FontSize=10;releaseTab.Click+=(s,e)=>OpenReleaseNotes();row.Children.Add(releaseTab);testButton=Btn("test",28);testButton.FontSize=8;testButton.ToolTip="本地自对战测试";testButton.Click+=(s,e)=>OpenLocalTest();row.Children.Add(testButton);Put(shell,container,1);
 }
 void SwitchTab(bool value){ShowPage(value?"log":"game");}
 void ShowPage(string page){currentPage=page;SyncMatchEffectsVisibility();log=page=="log";if(log)RefreshRecords();main.Visibility=page=="game"?Visibility.Visible:Visibility.Collapsed;logPanel.Visibility=page=="log"?Visibility.Visible:Visibility.Collapsed;settingsPanel.Visibility=page=="settings"?Visibility.Visible:Visibility.Collapsed;if(deckPanel!=null)deckPanel.Visibility=page=="deck"&&codePanel==null?Visibility.Visible:Visibility.Collapsed;if(codePanel!=null)codePanel.Visibility=page=="deck"?Visibility.Visible:Visibility.Collapsed;if(updatesPanel!=null)updatesPanel.Visibility=page=="updates"?Visibility.Visible:Visibility.Collapsed;if(releasePanel!=null)releasePanel.Visibility=page=="release"?Visibility.Visible:Visibility.Collapsed;if(releaseTab!=null)releaseTab.Foreground=page=="release"||page=="updates"?Green:Muted;if(lanPanel!=null)lanPanel.Visibility=page=="lan"?Visibility.Visible:Visibility.Collapsed;if(lobbyPanel!=null)lobbyPanel.Visibility=page=="lobby"?Visibility.Visible:Visibility.Collapsed;if(livePanel!=null)livePanel.Visibility=page=="match"?Visibility.Visible:Visibility.Collapsed;gameTab.Foreground=page=="game"||page=="match"||page=="lobby"||page=="lan"?Green:Muted;logTab.Foreground=page=="log"?Green:Muted;settingsTab.Foreground=page=="settings"?Green:Muted;deckTab.Foreground=page=="deck"?Green:Muted;shell.Children[2].Visibility=page=="game"?Visibility.Visible:Visibility.Collapsed;shell.RowDefinitions[2].Height=new GridLength(page=="game"?(minimal?20:28):0);}
 void Toolbar(){
  var grid=new Grid{Margin=new Thickness(9,0,9,0)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var label=T("第 7 回合  /  我的回合",11,Muted);grid.Children.Add(label);var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(row,1);grid.Children.Add(row);
  compactButton=Btn("满场",42);compactButton.Foreground=Green;compactButton.Click+=(s,e)=>ToggleBoard();row.Children.Add(compactButton);
  detailButton=Btn("详情",42);detailButton.Click+=(s,e)=>ToggleDetails();row.Children.Add(detailButton);
  pinButton=Btn("置顶",42);pinButton.Click+=(s,e)=>{Topmost=!Topmost;pinButton.Foreground=Topmost?Green:Muted;Record(Topmost?"已开启窗口置顶":"已取消窗口置顶");};row.Children.Add(pinButton);Put(shell,grid,2);
 }
 Grid HeroArea(bool own){
  var grid=new Grid{Margin=new Thickness(0,2,0,2)};grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(18)});
  var slots=new Grid();slots.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});slots.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.45,GridUnitType.Star)});slots.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  var weapon=new Entry(own?"武器槽 · 空":"炽炎战斧","",own?0:2,own?0:3,own?0:2,own?"当前没有装备武器。":"示例武器：3点攻击力，2点耐久。","","武器");
  var hero=new Entry(own?"我方 · 法师":"对方 · 战士","",0,0,own?30:26,"英雄区域：生命、护甲与状态。","","英雄");
  var power=new Entry(own?"火焰冲击":"全副武装","",2,0,0,own?"造成1点伤害。":"获得2点护甲。","","英雄技能");
  var w=Slot(weapon.Name,own?"攻击 —   耐久 —":"攻击 3   耐久 2",weapon,false);slots.Children.Add(w);
  var h=Slot(hero.Name,"生命 "+hero.Health+"   护甲 "+(own?"0":"2"),hero,true);Grid.SetColumn(h,1);slots.Children.Add(h);
  var p=Slot("英雄技能 · "+power.Name,"2 费   /   "+(own?"可用":"已使用"),power,false);Grid.SetColumn(p,2);slots.Children.Add(p);grid.Children.Add(slots);
  var resources=new Grid{Margin=new Thickness(0,3,0,0)};resources.ColumnDefinitions.Add(new ColumnDefinition());resources.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});resources.Children.Add(T("法力  "+(own?"5 / 7":"4 / 6"),10,Muted));
  var mana=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};int available=own?5:4,total=own?7:6;
  for(int i=0;i<10;i++){
   var crystal=new Polygon{Points=new PointCollection{new Point(5,0),new Point(10,5),new Point(5,10),new Point(0,5)},Width=10,Height=10,Stretch=Stretch.Fill,Stroke=i<total?B("#A5B9AC"):B("#E0E4E8"),StrokeThickness=1,Fill=i<available?B("#B5C8BB"):B("#FFFFFF"),Margin=new Thickness(2,0,0,0)};
   crystal.ToolTip=i<available?"可用法力":i<total?"已消耗法力":"未获得法力";mana.Children.Add(crystal);crystals.Add(crystal);
  }Grid.SetColumn(mana,1);resources.Children.Add(mana);Put(grid,resources,1);heroLayouts.Add(grid);return grid;
 }
 Border Slot(string name,string sub,Entry entry,bool center){
  var stack=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var title=T(name,11,Ink);title.TextTrimming=TextTrimming.CharacterEllipsis;title.TextAlignment=center?TextAlignment.Center:TextAlignment.Left;
  var meta=T(sub,10,Muted);meta.TextAlignment=title.TextAlignment;meta.Margin=new Thickness(0,2,0,0);stack.Children.Add(title);stack.Children.Add(meta);
  var border=new Border{Child=stack,Background=B("#F7F8FA"),BorderBrush=Line,BorderThickness=new Thickness(1),Padding=new Thickness(7,0,7,0),Margin=new Thickness(center?4:0,0,center?4:0,0)};Wire(border,entry);heroSlots.Add(border);slotLabels[border]=new string[]{name,sub};return border;
 }
 Entry[] EnemyEntries(){return new Entry[]{
  new Entry("森金持盾卫士","嘲讽",4,3,5,"嘲讽",""),new Entry("帝王眼镜蛇","剧毒",3,2,3,"剧毒",""),new Entry("麦田傀儡","亡语",3,2,3,"亡语：召唤一个2/1的损坏的傀儡。",""),
  new Entry("血色十字军战士","圣盾",3,3,1,"圣盾",""),new Entry("暴风城骑士","冲锋",4,2,5,"冲锋",""),new Entry("恐狼前锋","光环",2,2,2,"相邻的随从获得+1攻击力。",""),new Entry("石拳食人魔","",6,6,7,"","")};}
 Entry[] FriendlyEntries(){return new Entry[]{
  new Entry("风怒鹰身人","风怒",6,4,5,"风怒",""),new Entry("银色侍从","圣盾",1,1,1,"圣盾",""),new Entry("冰风雪人","",4,4,5,"",""),
  new Entry("阿古斯防御者","战吼",4,2,3,"战吼：使相邻的随从获得+1/+1和嘲讽。",""),new Entry("碧蓝幼龙","法强",5,4,4,"法术伤害+1。战吼：抽一张牌。",""),new Entry("荆棘谷猛虎","潜行",5,5,5,"潜行",""),new Entry("银色指挥官","冲锋 圣盾",6,4,2,"冲锋，圣盾。","")};}
 Grid BattleLane(Entry[] entries,string label){
  var grid=new Grid{Margin=new Thickness(0,1,0,1)};grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(minimal?0:14)});grid.RowDefinitions.Add(new RowDefinition());var caption=T(label,10,Muted);caption.Visibility=minimal?Visibility.Collapsed:Visibility.Visible;grid.Children.Add(caption);
  var strip=new Grid();for(int i=0;i<7;i++)strip.ColumnDefinitions.Add(new ColumnDefinition());Put(grid,strip,1);
  for(int i=0;i<7;i++){
   var border=new Border{BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(codex?3:0),Margin=new Thickness(0,0,i==6?0:2,0),Background=B("#FFFFFF"),VerticalAlignment=VerticalAlignment.Top,Height=minimal?43:57};
   if(fullBoard||i<3){
    Entry entry=entries[i];var cell=new Grid{Margin=new Thickness(1,2,1,2)};cell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(minimal?9:11)});cell.ColumnDefinitions.Add(new ColumnDefinition());cell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(minimal?9:11)});
    var attack=T(entry.Attack.ToString(),10,Ink);attack.TextAlignment=TextAlignment.Center;cell.Children.Add(attack);
    var cardLabel=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
    if(minimal){var effects=T(entry.Keywords.Length==0?"—":entry.Keywords.Replace(" ","\n"),10,Muted);effects.TextAlignment=TextAlignment.Center;effects.TextWrapping=TextWrapping.Wrap;effects.LineHeight=11;cardLabel.Children.Add(effects);var dot=Btn("⋯",13);dot.Height=10;dot.FontSize=10;dot.Padding=new Thickness(0);dot.ToolTip="查看详情";dot.Click+=(s,e)=>Select(entry);cardLabel.Children.Add(dot);}
    else{var name=T(entry.Name,10,Ink);name.TextWrapping=TextWrapping.Wrap;name.TextAlignment=TextAlignment.Center;name.LineHeight=12;name.TextTrimming=TextTrimming.None;cardLabel.Children.Add(name);if(entry.Keywords.Length>0){var keywords=T("["+entry.Keywords.Replace(" ","]\n[")+"]",10,Muted);keywords.TextAlignment=TextAlignment.Center;keywords.TextWrapping=TextWrapping.Wrap;keywords.LineHeight=12;cardLabel.Children.Add(keywords);}}Grid.SetColumn(cardLabel,1);cell.Children.Add(cardLabel);
    var health=T(entry.Health.ToString(),10,Ink);health.TextAlignment=TextAlignment.Center;Grid.SetColumn(health,2);cell.Children.Add(health);border.Child=cell;Wire(border,entry);battleCards.Add(border);
   }else{border.Background=B("#FAFBFC");}
   Grid.SetColumn(border,i);strip.Children.Add(border);
  }battleLanes.Add(grid);return grid;
 }
 void ToggleBoard(){
  fullBoard=!fullBoard;RebuildBattle();compactButton.Foreground=fullBoard?Green:Muted;Record(fullBoard?"已显示双方七随从":"已显示双方三随从");
 }
 void RebuildBattle(){foreach(var card in battleCards)rows.Remove(card);battleCards.Clear();foreach(var lane in battleLanes)main.Children.Remove(lane);battleLanes.Clear();Put(main,BattleLane(EnemyEntries(),"对方随从"),2);Put(main,BattleLane(FriendlyEntries(),"我方随从"),4);ApplyFonts(shell);RefreshSelection();}
 FrameworkElement Lane(Entry[] entries,string label,bool inHand){
  var grid=new Grid{Margin=new Thickness(0,2,0,2)};grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(minimal?10:16)});grid.RowDefinitions.Add(new RowDefinition());grid.Children.Add(T(minimal?"手牌  4":label,10,Muted));
  var list=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top};var scroll=new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,Padding=new Thickness(0),Background=B("#FFFFFF")};lanes.Add(scroll);Put(grid,scroll,1);
  foreach(var entry in entries){
   var cell=new Grid{Height=minimal?21:27};cell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(minimal?16:23)});cell.ColumnDefinitions.Add(new ColumnDefinition());cell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(inHand&&entry.Kind=="法术"?0:minimal?16:23)});
   var left=T(inHand?entry.Cost.ToString():entry.Attack.ToString(),11,inHand?Muted:Ink);left.TextAlignment=TextAlignment.Center;cell.Children.Add(left);
   var name=T("",11,Ink);name.TextTrimming=TextTrimming.CharacterEllipsis;name.Inlines.Add(new Run(minimal?(entry.Kind=="法术"?"法 ⋯":(entry.Keywords=="—"||entry.Keywords.Length==0?"随":entry.Keywords)+" ⋯"):entry.Name));Grid.SetColumn(name,1);cell.Children.Add(name);
   var hp=T(entry.Health.ToString(),11,Ink);hp.TextAlignment=TextAlignment.Center;Grid.SetColumn(hp,2);cell.Children.Add(hp);
   var border=new Border{Child=cell,Width=170,BorderBrush=Line,BorderThickness=new Thickness(1),Background=B("#FFFFFF"),Margin=new Thickness(0,0,3,0)};Wire(border,entry);handCards.Add(border);list.Children.Add(border);
  }
  var caption=(TextBlock)grid.Children[0];scroll.SizeChanged+=(s,e)=>{double size=Math.Max(minimal?58:142,(scroll.ActualWidth-entries.Length*3)/entries.Length);foreach(Border child in list.Children)child.Width=size;caption.Text=(minimal?"手牌  4":label)+(size*entries.Length+entries.Length*3>scroll.ActualWidth+1?"    ·    滚轮横向查看":"");};
  scroll.PreviewMouseWheel+=(s,e)=>{if(scroll.ScrollableWidth>0){scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-e.Delta);e.Handled=true;}};return grid;
 }
 void Wire(Border border,Entry entry){
  border.Tag=entry;border.Cursor=Cursors.Hand;border.Focusable=true;rows.Add(border);border.MouseLeftButtonDown+=(s,e)=>{border.Focus();Select(entry);};border.KeyDown+=(s,e)=>{if(e.Key==Key.Enter||e.Key==Key.Space){Select(entry);e.Handled=true;}};
  border.MouseEnter+=(s,e)=>{if(selected!=entry)border.Background=B("#F3F6F5");};border.MouseLeave+=(s,e)=>{if(selected!=entry)border.Background=heroSlots.Contains(border)?B("#F7F8FA"):B("#FFFFFF");};
 }
 void Select(Entry entry){
  selected=entry;foreach(var row in rows)row.Background=row.Tag==entry?B("#EDF4F0"):heroSlots.Contains(row)?B("#F7F8FA"):B("#FFFFFF");
  detailTitle.Text=entry.Name;detailMeta.Text=entry.Kind=="英雄"?"英雄 · 生命 "+entry.Health:entry.Kind=="武器"?"武器 · 攻击 "+entry.Attack+" / 耐久 "+entry.Health:entry.Cost+" 费 · "+entry.Kind+(entry.Kind=="随从"?" · "+entry.Attack+" 攻 / "+entry.Health+" 血":"");detailText.Text=entry.Text.Length==0?"无特殊效果。":entry.Text;detailText.Foreground=Ink;
  if(!details)ToggleDetails();status.Text=minimal?"详情已展开":"已选择  ·  "+entry.Name;Record("查看 "+entry.Name);
 }
 void ToggleDetails(){details=!details;detailBorder.Visibility=details?Visibility.Visible:Visibility.Collapsed;handLane.Visibility=details?Visibility.Collapsed:Visibility.Visible;detailButton.Foreground=details?Green:Muted;Record(details?"已展开详情":"已返回手牌");}
 void BuildSettings(){
  settingsPanel=new Grid{Margin=new Thickness(14,10,14,8),Visibility=Visibility.Collapsed};var stack=new StackPanel();settingsPanel.Children.Add(new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Put(shell,settingsPanel,4);
  var title=T("外观设置",12,Ink);title.Margin=new Thickness(0,0,0,14);stack.Children.Add(title);
  stack.Children.Add(T("界面风格",11,Muted));var themes=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,12)};
  wpsOption=Option("WPS",120);codexOption=Option("Codex",120);themes.Children.Add(wpsOption);themes.Children.Add(codexOption);stack.Children.Add(themes);
  wpsOption.Click+=(s,e)=>{ApplyTheme(false,dark);SavePreferences();};codexOption.Click+=(s,e)=>{ApplyTheme(true,dark);SavePreferences();};
  stack.Children.Add(T("显示模式",11,Muted));var modes=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,3)};
  normalOption=Option("普通  548 × 474",144);minimalOption=Option("极简  392 × 300",144);modes.Children.Add(normalOption);modes.Children.Add(minimalOption);stack.Children.Add(modes);
  normalOption.Click+=(s,e)=>{ApplyMode(false);SavePreferences();};minimalOption.Click+=(s,e)=>{ApplyMode(true);SavePreferences();};
  var hint=T("极简只显示数值和效果，点小圆点查看详情。",10,Muted);hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(0,4,0,12);stack.Children.Add(hint);
  stack.Children.Add(T("Codex 明暗",11,Muted));var tones=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,12)};
  lightOption=Option("浅色",120);darkOption=Option("深色",120);tones.Children.Add(lightOption);tones.Children.Add(darkOption);stack.Children.Add(tones);
  lightOption.Click+=(s,e)=>{ApplyTheme(codex,false);SavePreferences();};darkOption.Click+=(s,e)=>{ApplyTheme(codex,true);SavePreferences();};
  fullEffectsOption=new CheckBox{Content="特效全开",FontSize=11,FontWeight=FontWeights.Normal,Foreground=Ink,Margin=new Thickness(0,0,0,5),IsChecked=fullEffects};fullEffectsOption.Click+=(s,e)=>{SetFullEffects(fullEffectsOption.IsChecked==true);SavePreferences();};stack.Children.Add(fullEffectsOption);
  var note=T("即时生效，自动记住选择。所有文字保持正常字重。",10,Muted);note.TextWrapping=TextWrapping.Wrap;stack.Children.Add(note);UpdateSettingButtons();
 }
 Button Option(string text,double width){var button=Btn(text,width);button.Height=29;button.Margin=new Thickness(0,0,6,0);return button;}
 void UpdateSettingButtons(){if(fullEffectsOption!=null)fullEffectsOption.IsChecked=fullEffects;if(wpsOption==null)return;Mark(wpsOption,!codex);Mark(codexOption,codex);Mark(normalOption,!minimal);Mark(minimalOption,minimal);Mark(lightOption,!dark);Mark(darkOption,dark);lightOption.IsEnabled=codex;darkOption.IsEnabled=codex;lightOption.Opacity=darkOption.Opacity=codex?1:.45;}
 void Mark(Button button,bool active){button.Background=active?B("#EDF4F0"):B("#F7F8FA");button.Foreground=active?Ink:Muted;}
 void ApplyTheme(bool useCodex,bool useDark){
  codex=useCodex;dark=useDark;
  string[] keys={"#FFFFFF","#34383D","#83888F","#E7E9EC","#387C60","#F2F3F5","#CFD3D8","#9AA1A9","#E8ECEF","#F7F8FA","#A5B9AC","#E0E4E8","#B5C8BB","#FAFBFC","#F3F6F5","#EDF4F0"};
  string[] light={"#FAFAFA","#333333","#808080","#E4E4E4","#4B4B4B","#F0F0F0","#D4D4D4","#9A9A9A","#E8E8E8","#F3F3F3","#A8A8A8","#DFDFDF","#BABABA","#F8F8F8","#EEEEEE","#E4E4E4"};
  string[] night={"#0D1117","#E6EDF3","#E6EDF3","#30363D","#E6EDF3","#161B22","#30363D","#8B949E","#21262D","#161B22","#8B949E","#30363D","#8B949E","#0D1117","#161B22","#21262D"};
  for(int i=0;i<keys.Length;i++){B(keys[i]);PaletteColors[keys[i]].Value=(Color)ColorConverter.ConvertFromString(codex?(dark?night[i]:light[i]):keys[i]);}
  Walk(shell,node=>{var border=node as Border;if(border!=null&&border.Tag is Entry)border.CornerRadius=new CornerRadius(codex?3:0);var button=node as Button;if(button!=null)button.Template=ButtonTemplate();});detailBorder.CornerRadius=new CornerRadius(codex?4:0);UpdateSettingButtons();if(match!=null)RenderMatch();
 }
 void ApplyMode(bool useMinimal){
  minimal=useMinimal;NotifyPresentation();MinWidth=minimal?392:548;MinHeight=minimal?300:474;Width=minimal?392:548;Height=minimal?300:474;
  double[] chrome=minimal?new double[]{22,20,20,1,-1,14}:new double[]{28,26,28,1,-1,20};for(int i=0;i<chrome.Length;i++)if(chrome[i]>=0)shell.RowDefinitions[i].Height=new GridLength(chrome[i]);
  main.Margin=new Thickness(minimal?4:8,0,minimal?4:8,0);double[] body=minimal?new double[]{0,30,-1,10,-1,30,34}:new double[]{16,54,-1,20,-1,54,64};for(int i=0;i<body.Length;i++)if(body[i]>=0)main.RowDefinitions[i].Height=new GridLength(body[i]);main.RowDefinitions[2].MinHeight=main.RowDefinitions[4].MinHeight=minimal?46:72;
  foreach(UIElement child in main.Children)if(Grid.GetRow(child)==0)child.Visibility=minimal?Visibility.Collapsed:Visibility.Visible;
  foreach(var layout in heroLayouts){layout.RowDefinitions[0].Height=new GridLength(minimal?16:32);layout.RowDefinitions[1].Height=new GridLength(minimal?10:18);((Grid)layout.Children[1]).Margin=new Thickness(0,minimal?0:3,0,0);}
  foreach(Border slot in heroSlots){var stack=(StackPanel)slot.Child;var title=(TextBlock)stack.Children[0];var meta=(TextBlock)stack.Children[1];var entry=(Entry)slot.Tag;title.Text=minimal?(entry.Kind=="英雄"?entry.Health.ToString():entry.Kind=="武器"?"武":"技"):slotLabels[slot][0];title.TextAlignment=minimal?TextAlignment.Center:entry.Kind=="英雄"?TextAlignment.Center:TextAlignment.Left;meta.Visibility=minimal?Visibility.Collapsed:Visibility.Visible;meta.Margin=new Thickness(0,2,0,0);}
  foreach(var crystal in crystals){crystal.Width=crystal.Height=minimal?8:10;crystal.Margin=new Thickness(minimal?1:2,0,0,0);}
  detailHeader.Height=detailBack.Height=minimal?12:15;detailBack.FontSize=minimal?9:11;detailMeta.Visibility=minimal?Visibility.Collapsed:Visibility.Visible;detail.Margin=minimal?new Thickness(4,0,4,0):new Thickness(8,2,8,2);detailText.Margin=new Thickness(0,minimal?0:2,0,0);detailText.LineHeight=minimal?11:double.NaN;detailBorder.Margin=minimal?new Thickness(0,0,0,0):new Thickness(0,2,0,4);
  RebuildBattle();RebuildHand();ApplyFonts(shell);UpdateSettingButtons();ApplyDeckMode();if(match!=null)RenderMatch();ShowPage(currentPage);status.Text=minimal?"极简 · 未连接":"视觉预览 · 未连接";
 }
 void RebuildHand(){foreach(var card in handCards)rows.Remove(card);handCards.Clear();main.Children.Remove(handLane);lanes.Clear();handLane=Lane(new Entry[]{new Entry("火球术","法术",4,0,0,"造成6点伤害。","","法术"),new Entry("寒冰箭","法术",2,0,0,"造成3点伤害，并冻结目标。","","法术"),new Entry("酸性沼泽软泥怪","战吼",2,3,2,"战吼：摧毁对手的武器。",""),new Entry("石拳食人魔","—",6,6,7,"","")},"手牌  4    /    牌库  16",true);Put(main,handLane,6);handLane.Visibility=details?Visibility.Collapsed:Visibility.Visible;Panel.SetZIndex(detailBorder,1);RefreshSelection();}
 void ApplyFonts(DependencyObject root){Walk(root,node=>{var text=node as TextBlock;if(text!=null&&text.Tag is double){text.FontSize=Math.Max(9,(double)text.Tag-(minimal?.5:0));text.FontWeight=FontWeights.Normal;}});}
 static void Walk(DependencyObject root,Action<DependencyObject> action){action(root);foreach(var child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)Walk(node,action);}}
 void RefreshSelection(){foreach(var row in rows)row.Background=row.Tag==selected?B("#EDF4F0"):heroSlots.Contains(row)?B("#F7F8FA"):B("#FFFFFF");}
 void SetFullEffects(bool enabled){fullEffects=enabled;NotifyPresentation();if(!enabled&&matchEffects!=null)matchEffects.Clear();RefreshDeck();UpdateSettingButtons();SyncMatchEffectsVisibility();if(match!=null)RenderMatch();}
 string PreferencesPath{get{return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preferences.ini");}}
 void SavePreferences(){if(previewRun)return;try{File.WriteAllText(PreferencesPath,"theme="+(codex?"codex":"wps")+"\r\nappearance="+(dark?"dark":"light")+"\r\nmode="+(minimal?"minimal":"normal")+"\r\neffects="+(fullEffects?"all":"off")+"\r\n");status.Text="外观设置已保存";}catch(IOException){status.Text="设置已生效，保存失败";}catch(UnauthorizedAccessException){status.Text="设置已生效，目录不可写";}}
 void LoadPreferences(){try{if(!File.Exists(PreferencesPath))return;string value=File.ReadAllText(PreferencesPath);fullEffects=value.Contains("effects=all");ApplyTheme(value.Contains("theme=codex"),value.Contains("appearance=dark"));ApplyMode(value.Contains("mode=minimal"));}catch(IOException){}catch(UnauthorizedAccessException){}}
 public void ShowCodexMinimal(){ApplyTheme(true,true);ApplyMode(true);}
 
 public void Render(string path){
  UpdateLayout(); var target=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);target.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));using(var file=File.Create(path))encoder.Save(file);
 }
 public void PreviewSuite(string folder){
  previewRun=true;ApplyTheme(false,false);ApplyMode(false);ShowPage("game");if(details)ToggleDetails();Directory.CreateDirectory(folder);Render(Path.Combine(folder,"01-standard.png"));Render(Path.Combine(folder,"03-compact.png"));
  Check(Width==548&&Height==474&&!details,"Default compact size");Check(battleCards.Count==14,"Seven minions per side");
  foreach(var card in battleCards){var bounds=card.TransformToAncestor(main).TransformBounds(new Rect(0,0,card.ActualWidth,card.ActualHeight));Check(bounds.Left>=-1&&bounds.Right<=main.ActualWidth+1,"Battle card fits width");var label=(StackPanel)((Grid)card.Child).Children[1];var name=(TextBlock)label.Children[0];Check(name.TextWrapping==TextWrapping.Wrap,"Name wraps");Check(label.DesiredSize.Height<=53,"Name and keywords fit vertically");}
  Select((Entry)battleCards[3].Tag);UpdateLayout();Render(Path.Combine(folder,"02-details.png"));Render(Path.Combine(folder,"04-compact-details.png"));Check(Height==474&&Width==548,"Inspection preserves size");Check(handLane.Visibility==Visibility.Collapsed,"Inspector replaces hand area");
  foreach(var row in rows){Select((Entry)row.Tag);Check(detailTitle.Text==((Entry)row.Tag).Name,"Selection");}
  ToggleDetails();Check(handLane.Visibility==Visibility.Visible,"Hand restored");ToggleBoard();UpdateLayout();Render(Path.Combine(folder,"06-three-minions.png"));Check(battleCards.Count==6,"Three minions with reserved slots");ToggleBoard();
  SwitchTab(true);UpdateLayout();Render(Path.Combine(folder,"05-log.png"));SwitchTab(false);Check(main.Visibility==Visibility.Visible,"Tab restoration");
  Check(heroSlots.Count==6&&crystals.Count==20,"Hero and mana slots");Check(Grid.GetRow(battleLanes[0])==2&&Grid.GetRow(battleLanes[1])==4,"Battlefield order");CheckNormalWeights(this);
  ApplyTheme(true,false);Render(Path.Combine(folder,"08-codex-normal-light.png"));Check(!dark&&codex,"Codex light theme");
  ApplyTheme(true,true);Render(Path.Combine(folder,"09-codex-normal-dark.png"));Check(dark&&codex,"Codex dark theme");
  ApplyMode(true);Render(Path.Combine(folder,"10-codex-minimal-dark.png"));Check(Width==392&&Height==300,"Minimal dimensions");CheckBoardFit();foreach(var card in battleCards){var label=(StackPanel)((Grid)card.Child).Children[1];Check(((TextBlock)label.Children[0]).Text!=((Entry)card.Tag).Name,"Minimal hides names");Check(label.Children[1] is Button,"Small detail entry");}foreach(Border slot in heroSlots){var entry=(Entry)slot.Tag;Check(((TextBlock)((StackPanel)slot.Child).Children[0]).Text==(entry.Kind=="英雄"?entry.Health.ToString():entry.Kind=="武器"?"武":"技"),"Minimal hero text");}
  ShowPage("settings");Render(Path.Combine(folder,"11-settings-dark.png"));Check(settingsPanel.Visibility==Visibility.Visible&&main.Visibility==Visibility.Collapsed,"Settings page");
  ShowPage("game");Select((Entry)battleCards[6].Tag);Render(Path.Combine(folder,"12-codex-minimal-details.png"));Check(Width==392&&Height==300,"Minimal inspector size");if(details)ToggleDetails();
  ApplyTheme(true,false);Render(Path.Combine(folder,"13-codex-minimal-light.png"));ApplyTheme(false,false);Render(Path.Combine(folder,"07-wps-minimal.png"));CheckBoardFit();
  ShowPage("settings");Render(Path.Combine(folder,"14-settings-light.png"));Check(!lightOption.IsEnabled&&!darkOption.IsEnabled,"WPS appearance controls disabled");ShowPage("game");
  ApplyMode(false);Check(Width==548&&Height==474,"Normal restoration");CheckBoardFit();CheckNormalWeights(this);
  VerifyLibrary(folder);
  VerifyMatch(folder);VerifyLocalTestTools(folder);
  VerifyLan(folder);VerifyUpdates(folder);VerifyVisualEffects(folder);ApplyTheme(true,true);ApplyMode(true);OpenReleaseNotes();Render(Path.Combine(folder,"47-release-notes-codex.png"));ApplyTheme(false,false);ApplyMode(false);OpenReleaseNotes();Render(Path.Combine(folder,"48-release-notes-wps.png"));RenderExpandedRelease(folder);ShowPage("lan");
  File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS: "+checks+" assertions. Classic collection and deckcodes; local test engine, LAN host authority and two WPF peers over real TCP; attacks, spells, powers, placement, privacy, revision rejection, disconnects; themes and layouts.\r\nActual WPF visual-tree renders at 96 DPI. Rule coverage is partial. Automated network checks use loopback sockets. User reported a working LAN session with a friend; original game client comparisons were not performed.\r\n");
 }
 void CheckBoardFit(){UpdateLayout();Check(battleCards.Count==14,"Fourteen visible minions");foreach(var card in battleCards){var bounds=card.TransformToAncestor(main).TransformBounds(new Rect(0,0,card.ActualWidth,card.ActualHeight));Check(bounds.Left>=-1&&bounds.Right<=main.ActualWidth+1&&bounds.Bottom<=main.ActualHeight+1,"Card in viewport");var label=(StackPanel)((Grid)card.Child).Children[1];Check(label.DesiredSize.Height<=card.ActualHeight-4,"Wrapping fits card");}foreach(var slot in heroSlots){var bounds=slot.TransformToAncestor(main).TransformBounds(new Rect(0,0,slot.ActualWidth,slot.ActualHeight));Check(bounds.Bottom<=main.ActualHeight+1,"Hero component in viewport");}Check(crystals.Count==20,"Twenty crystals");CheckNormalWeights(this);}
 void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
 void CheckNormalWeights(DependencyObject node){var text=node as TextBlock;if(text!=null)Check(text.FontWeight==FontWeights.Normal,"Text weight");var control=node as Control;if(control!=null)Check(control.FontWeight==FontWeights.Normal,"Control weight");for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)CheckNormalWeights(VisualTreeHelper.GetChild(node,i));}
}
public static class Program {
 [STAThread]public static void Main(string[] args){try{
  string updateRoot=AppDomain.CurrentDomain.BaseDirectory;if(args.Length==2&&args[0]=="--check-updates"){var releases=UpdateCore.Releases(System.Threading.CancellationToken.None);UpdateCore.Write(args[1],releases);return;}bool updateLaunch=args.Length==2&&args[0]=="--update-token";if(!updateLaunch&&UpdateCore.RecoverBeforeStart(updateRoot))return;UpdateCore.CleanupRunners(updateRoot);var app=new Application();var window=new PanelWindow(args.Length>0&&args[0]=="--preview");if(updateLaunch)window.Loaded+=(s,e)=>UpdateCore.Health(updateRoot,args[1]);if(args.Length==1&&args[0]=="--showcase")window.ShowCodexMinimal();
  if((args.Length==2||args.Length==4&&args[2]=="--scope"&&(args[3]=="summon"||args[3]=="turn-end"||args[3]=="hero-visuals"||args[3]=="triggers"||args[3]=="deathrattle"||args[3]=="druid-spells"||args[3]=="emote-selection"||args[3]=="overload"||args[3]=="spell-cards"||args[3]=="opening-turn"||args[3]=="deck-picker"||args[3]=="hero-freeze"||args[3]=="combo"||args[3]=="warlock"||args[3]=="visual-polish"))&&args[0]=="--preview")window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{try{if(args.Length==4){if(args[3]=="visual-polish")window.PreviewVisualPolishScope(args[1]);else if(args[3]=="warlock")window.PreviewWarlockScope(args[1]);else if(args[3]=="combo")window.PreviewComboScope(args[1]);else if(args[3]=="hero-freeze")window.PreviewHeroFreezeScope(args[1]);else if(args[3]=="deck-picker")window.PreviewDeckPickerScope(args[1]);else if(args[3]=="opening-turn")window.PreviewOpeningTurnScope(args[1]);else if(args[3]=="spell-cards")window.PreviewSpellCardsScope(args[1]);else if(args[3]=="overload")window.PreviewOverloadScope(args[1]);else if(args[3]=="emote-selection")window.PreviewEmoteSelectionScope(args[1]);else if(args[3]=="druid-spells")window.PreviewDruidSpellScope(args[1]);else if(args[3]=="deathrattle")window.PreviewDeathrattleScope(args[1]);else if(args[3]=="triggers")window.PreviewTriggerScope(args[1]);else if(args[3]=="hero-visuals")window.PreviewHeroVisualScope(args[1]);else if(args[3]=="turn-end")window.PreviewTurnEndScope(args[1]);else window.PreviewSummonScope(args[1]);}else window.PreviewSuite(args[1]);}catch(Exception ex){File.WriteAllText(Path.Combine(args[1],"error.txt"),ex.ToString());Environment.ExitCode=1;}window.Close();}));
  app.Run(window);
 }catch(Exception ex){File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.txt"),ex.ToString());Environment.ExitCode=1;}
 }
}
}
