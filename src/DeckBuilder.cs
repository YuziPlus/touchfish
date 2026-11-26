using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;

namespace Touchfish {
public sealed class Choice {public string Key,Label;public Choice(string key,string label){Key=key;Label=label;}public override string ToString(){return Label;}}
public sealed class SavedDeck {public string Id,Name,Code,PoolId;}
public partial class PanelWindow {
 CardCatalog catalog;DeckDocument draft;Grid deckPanel,codePanel;Button deckTab,exportButton,saveDeckButton;
 bool syncingSavedDeckPicker;ComboBox savedDeckPicker;ComboBox versionPicker,classPicker,poolPicker,typePicker,costPicker,classFilterPicker;TextBox searchBox,deckNameBox,codeBox;
 CheckBox showAllCards;StackPanel collectionRows,deckRows,savedRows;TextBlock collectionCount,deckCount,cardInfo,cardEffect,deckNotice,codeNotice;
 bool libraryWritable=true,draftWritable=true;bool refreshingDeck;List<CardRecord> filteredCards=new List<CardRecord>();List<SavedDeck> savedDecks=new List<SavedDeck>();
 Stack<DeckDocument> deckUndo=new Stack<DeckDocument>();
 static readonly Choice[] Classes={new Choice("MAGE","法师"),new Choice("WARRIOR","战士"),new Choice("WARLOCK","术士"),new Choice("ROGUE","潜行者"),new Choice("PRIEST","牧师"),new Choice("PALADIN","圣骑士"),new Choice("HUNTER","猎人"),new Choice("SHAMAN","萨满"),new Choice("DRUID","德鲁伊")};
 string DeckDirectory{get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"decks");}}string LegacyDeckDirectory{get{return DeckStorage.UserDirectory();}}
 void BuildDeckUi(){
  catalog=CardCatalog.Load(AppDomain.CurrentDomain.BaseDirectory);matchRules=new MatchRules(catalog);draft=new DeckDocument{PoolId=catalog.Pools.First(p=>!p.Archived).Id};
  string draftPath=null;
  try{savedDecks=DeckStorage.LoadLibrary(DeckDirectory,LegacyDeckDirectory);}catch(Exception ex){if(!DeckStorage.IsReadError(ex))throw;libraryWritable=false;status.Text="卡组库未加载，原文件受保护。";}
  try{draftPath=DeckStorage.ExistingDraftPath(DeckDirectory,LegacyDeckDirectory);if(draftPath!=null){var loaded=JsonData.Read<DeckDocument>(draftPath);DeckRules.Validate(catalog,loaded,false);draft=loaded;}}catch(Exception ex){if(!DeckStorage.IsReadError(ex))throw;draftWritable=false;status.Text="旧草稿未加载，原文件受保护。";}
  Loaded+=(sender,eventArgs)=>{if(previewRun)return;try{if(libraryWritable&&!File.Exists(Path.Combine(DeckDirectory,"library.json")))DeckStorage.SaveLibrary(DeckDirectory,savedDecks);if(draftWritable&&draftPath!=null&&!File.Exists(Path.Combine(DeckDirectory,"draft.json")))JsonData.Write(Path.Combine(DeckDirectory,"draft.json"),draft);}catch(Exception ex){if(!DeckStorage.IsReadError(ex))throw;Notice("数据迁移暂未保存，旧目录原文件仍保留。");}};
  Resources["FieldBg"]=B("#FFFFFF");Resources["FieldLine"]=Line;Resources["FieldInk"]=Ink;Resources["FieldSelect"]=B("#EDF4F0");
  Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Grid Background='{DynamicResource FieldBg}'><Track Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' ViewportSize='{TemplateBinding ViewportSize}' Value='{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate><Border Margin='1' Background='{DynamicResource FieldLine}' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid><ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='8'/><Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
  Resources[typeof(CheckBox)]=XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type CheckBox}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'><StackPanel Orientation='Horizontal' VerticalAlignment='Center'><Border Width='10' Height='10' BorderBrush='{DynamicResource FieldLine}' BorderThickness='1' Background='{DynamicResource FieldBg}' CornerRadius='1'><TextBlock Name='CheckMark' Text='✓' FontSize='9' Foreground='{DynamicResource FieldInk}' Visibility='Collapsed' HorizontalAlignment='Center'/></Border><ContentPresenter Margin='4,0,0,0' VerticalAlignment='Center'/></StackPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='CheckMark' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
  deckPanel=new Grid{Margin=new Thickness(6,4,6,4)};foreach(var height in new double[]{26,25,-1,42,25})deckPanel.RowDefinitions.Add(new RowDefinition{Height=height<0?new GridLength(1,GridUnitType.Star):new GridLength(height)});Put(shell,deckPanel,4);
  var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(82)});heading.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(78)});heading.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(96)});
  deckNameBox=Input("新卡组");deckNameBox.Margin=new Thickness(0,0,5,2);heading.Children.Add(deckNameBox);deckNameBox.TextChanged+=(s,e)=>{if(!refreshingDeck){draft.Name=deckNameBox.Text;PersistDraft();}};
  classPicker=Picker(Classes);classPicker.Margin=new Thickness(0,0,5,2);Grid.SetColumn(classPicker,1);heading.Children.Add(classPicker);classPicker.SelectionChanged+=(s,e)=>{if(refreshingDeck||classPicker.SelectedItem==null)return;RememberDeck();bool preserved=ChangeDraftClass(((Choice)classPicker.SelectedItem).Key);int removed=PruneDeck();RefreshDeck();PersistDraft();Notice(preserved?"已另存为新职业卡组，原卡组代码已保留。":removed>0?"已移出 "+removed+" 张其他职业牌。":"已切换职业。");};
  poolPicker=Picker(DeckPoolChoices());poolPicker.Margin=new Thickness(0,0,0,2);Grid.SetColumn(poolPicker,2);heading.Children.Add(poolPicker);poolPicker.SelectionChanged+=(s,e)=>{if(refreshingDeck||poolPicker.SelectedItem==null)return;RememberDeck();SwitchDeckPool(((Choice)poolPicker.SelectedItem).Key);PruneDeck();SyncDeckControls();RefreshDeck();PersistDraft();};versionPicker=Picker(new Choice[0]);versionPicker.ToolTip="按卡牌版本查看；不改变当前卡组或卡池。";versionPicker.Margin=new Thickness(4,0,0,2);Grid.SetColumn(versionPicker,3);heading.Children.Add(versionPicker);RefreshVersionPicker();versionPicker.SelectionChanged+=(s,e)=>{if(!refreshingDeck)RefreshCollection();};Put(deckPanel,heading,0);
  var filters=new Grid();filters.ColumnDefinitions.Add(new ColumnDefinition());filters.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(42)});filters.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(50)});filters.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(74)});filters.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});
  searchBox=Input("");searchBox.ToolTip="搜索全库名称、效果或卡牌 ID；其他职业仅可查看";searchBox.Margin=new Thickness(0,0,4,2);searchBox.TextChanged+=(s,e)=>{if(!refreshingDeck)RefreshCollection();};filters.Children.Add(searchBox);
  costPicker=Picker(new Choice[]{new Choice("all","费用"),new Choice("0","0"),new Choice("1","1"),new Choice("2","2"),new Choice("3","3"),new Choice("4","4"),new Choice("5","5"),new Choice("6","6"),new Choice("7","7+")});costPicker.ToolTip="费用筛选";costPicker.SelectedIndex=0;costPicker.Margin=new Thickness(0,0,4,2);Grid.SetColumn(costPicker,1);filters.Children.Add(costPicker);costPicker.SelectionChanged+=(s,e)=>{if(!refreshingDeck)RefreshCollection();};
  typePicker=Picker(new Choice[]{new Choice("all","全部"),new Choice("MINION","随从"),new Choice("SPELL","法术"),new Choice("WEAPON","武器")});typePicker.SelectedIndex=0;typePicker.Margin=new Thickness(0,0,4,2);Grid.SetColumn(typePicker,2);filters.Children.Add(typePicker);typePicker.SelectionChanged+=(s,e)=>{if(!refreshingDeck)RefreshCollection();};
  classFilterPicker=Picker(new Choice[]{new Choice("available","本职+中立"),new Choice("class","职业卡"),new Choice("neutral","中立卡")});classFilterPicker.SelectedIndex=0;classFilterPicker.ToolTip="筛选职业卡或中立卡；勾选全库后可查看其他职业卡";classFilterPicker.Margin=new Thickness(0,0,4,2);Grid.SetColumn(classFilterPicker,3);filters.Children.Add(classFilterPicker);classFilterPicker.SelectionChanged+=(s,e)=>{if(!refreshingDeck)RefreshCollection();};
  showAllCards=new CheckBox{Content="全库",FontSize=10,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center};showAllCards.ToolTip="包含其他职业卡；可与职业卡/中立卡筛选组合使用";showAllCards.Checked+=(s,e)=>RefreshCollection();showAllCards.Unchecked+=(s,e)=>RefreshCollection();Grid.SetColumn(showAllCards,4);filters.Children.Add(showAllCards);Put(deckPanel,filters,1);
  var lists=new Grid();lists.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.4,GridUnitType.Star)});lists.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(5)});lists.ColumnDefinitions.Add(new ColumnDefinition());
  collectionRows=new StackPanel();deckRows=new StackPanel();collectionCount=T("牌库",10,Muted);deckCount=T("卡组 0/30",10,Muted);lists.Children.Add(ListPane(collectionCount,collectionRows));var deckPane=new Grid();deckPane.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});deckPane.RowDefinitions.Add(new RowDefinition());savedDeckPicker=Picker(new Choice[0]);savedDeckPicker.ToolTip="直接选择已保存卡组进行编辑";savedDeckPicker.Margin=new Thickness(0,0,0,3);deckPane.Children.Add(savedDeckPicker);savedDeckPicker.SelectionChanged+=(s,e)=>{if(syncingSavedDeckPicker)return;var choice=savedDeckPicker.SelectedItem as Choice;if(choice==null||String.IsNullOrEmpty(choice.Key))return;var saved=savedDecks.FirstOrDefault(d=>d.Id==choice.Key);if(saved!=null)LoadSavedDeckForEditing(saved);};Put(deckPane,ListPane(deckCount,deckRows),1);Grid.SetColumn(deckPane,2);lists.Children.Add(deckPane);Put(deckPanel,lists,2);
  var description=new StackPanel{Margin=new Thickness(5,1,5,1)};cardInfo=T("点名称查看效果，点 + 加入卡组。",10,Ink);cardEffect=T("",10,Muted);cardEffect.TextWrapping=TextWrapping.Wrap;description.Children.Add(cardInfo);description.Children.Add(cardEffect);
  var inspector=new Border{Child=new ScrollViewer{Content=description,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Background=B("#F7F8FA"),BorderBrush=Line,BorderThickness=new Thickness(1),Margin=new Thickness(0,1,0,1)};Put(deckPanel,inspector,3);
  var actions=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(buttons,Dock.Right);actions.Children.Add(buttons);
  var fresh=Btn("新建",37);fresh.Click+=(s,e)=>{RememberDeck();draft=new DeckDocument{ClassId=draft.ClassId,PoolId=ExplorerRules.IsLockedPool(catalog.Pool(draft.PoolId))?catalog.Pools.First(p=>!p.Archived).Id:draft.PoolId};SyncDeckControls();RefreshDeck();PersistDraft();Notice("已新建草稿；已保存的卡组不会被覆盖。");};buttons.Children.Add(fresh);
  var undo=Btn("撤销",37);undo.Click+=(s,e)=>{if(deckUndo.Count==0)return;draft=deckUndo.Pop();SyncDeckControls();RefreshDeck();PersistDraft();Notice("已撤销。");};buttons.Children.Add(undo);
  saveDeckButton=Btn("保存",37);saveDeckButton.Click+=(s,e)=>SaveFinishedDeck();buttons.Children.Add(saveDeckButton);var open=Btn("打开",37);open.Click+=(s,e)=>ShowSavedDecks();buttons.Children.Add(open);var delete=Btn("删除",37);delete.ToolTip="删除已保存的卡组";delete.Click+=(s,e)=>ShowSavedDecks(true);buttons.Children.Add(delete);
  exportButton=Btn("导出代码",65);exportButton.Click+=(s,e)=>ExportCode();buttons.Children.Add(exportButton);var import=Btn("导入代码",65);import.Click+=(s,e)=>ShowCodeUi(true,null);buttons.Children.Add(import);
  deckNotice=T("",10,Muted);deckNotice.TextTrimming=TextTrimming.CharacterEllipsis;actions.Children.Add(deckNotice);Put(deckPanel,actions,4);
  SyncDeckControls();RefreshDeck();deckPanel.Visibility=Visibility.Collapsed;
 }
 TextBox Input(string text){return new TextBox{Text=text,Background=B("#FFFFFF"),Foreground=Ink,CaretBrush=Ink,BorderBrush=Line,BorderThickness=new Thickness(1),Padding=new Thickness(3,0,3,0),FontSize=11,VerticalContentAlignment=VerticalAlignment.Center,FontWeight=FontWeights.Normal};}
 ComboBox Picker(Choice[] choices){
  var picker=new ComboBox{ItemsSource=choices,Background=B("#FFFFFF"),Foreground=Ink,BorderBrush=Line,BorderThickness=new Thickness(1),FontSize=10,FontWeight=FontWeights.Normal};
  picker.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='{DynamicResource FieldBg}' BorderBrush='{DynamicResource FieldLine}' BorderThickness='1' CornerRadius='2'><Grid><TextBlock Text='⌄' HorizontalAlignment='Right' Margin='0,0,4,0' Foreground='{DynamicResource FieldInk}' VerticalAlignment='Center'/></Grid></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' Margin='4,0,17,0' VerticalAlignment='Center'/><Popup Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'><Border Background='{DynamicResource FieldBg}' BorderBrush='{DynamicResource FieldLine}' BorderThickness='1' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'><ScrollViewer MaxHeight='170' CanContentScroll='True'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate>");
  var style=new Style(typeof(ComboBoxItem));style.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension("FieldInk")));style.Setters.Add(new Setter(Control.BackgroundProperty,new DynamicResourceExtension("FieldBg")));style.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.Normal));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(4,3,4,3)));
  var template=new ControlTemplate(typeof(ComboBoxItem));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.MarginProperty,new Thickness(4,3,4,3));border.AppendChild(content);template.VisualTree=border;style.Setters.Add(new Setter(Control.TemplateProperty,template));var hover=new Trigger{Property=ComboBoxItem.IsHighlightedProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,new DynamicResourceExtension("FieldSelect")));style.Triggers.Add(hover);picker.ItemContainerStyle=style;return picker;
 }
 FrameworkElement ListPane(TextBlock title,StackPanel content){var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(15)});grid.RowDefinitions.Add(new RowDefinition());grid.Children.Add(title);Put(grid,new Border{BorderBrush=Line,BorderThickness=new Thickness(1),Child=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}},1);return grid;}
 Choice[] DeckPoolChoices(){return catalog.Pools.Where(p=>!p.Archived||p.Id==draft.PoolId&&ExplorerRules.IsLockedPool(p))
  .Select(p=>new Choice(p.Id,ExplorerRules.IsLockedPool(p)?p.Name+"（锁定）":p.Name)).ToArray();}
 void SyncDeckControls(){
  var pool=catalog.Pool(draft.PoolId);
  if(pool.Archived&&!ExplorerRules.IsLockedPool(pool))SwitchDeckPool(catalog.Pools.First(p=>!p.Archived).Id);
  refreshingDeck=true;
  deckNameBox.Text=draft.Name;
  classPicker.SelectedItem=Classes.First(c=>c.Key==draft.ClassId);
  var choices=DeckPoolChoices();
  poolPicker.ItemsSource=choices;
  poolPicker.SelectedItem=choices.First(c=>c.Key==draft.PoolId);
  refreshingDeck=false;
 }
 void RememberDeck(){deckUndo.Push(draft.Copy());if(deckUndo.Count>50)deckUndo=new Stack<DeckDocument>(deckUndo.Reverse().Skip(1));}
 void SwitchDeckPool(string id){var old=catalog.Pool(draft.PoolId);var next=catalog.Pool(id);var cards=new Dictionary<string,int>();foreach(var pair in draft.Cards){string key=pair.Key,value;if(next.CardReplacements!=null&&next.CardReplacements.TryGetValue(key,out value))key=value;else if(old.CardReplacements!=null){var original=old.CardReplacements.FirstOrDefault(p=>p.Value==key);if(original.Key!=null&&next.Contains(catalog.Card(original.Key)))key=original.Key;}int count;cards.TryGetValue(key,out count);cards[key]=count+pair.Value;}draft.PoolId=id;draft.Cards=cards;}
 int PruneDeck(){var pool=catalog.Pool(draft.PoolId);int removed=0;foreach(var id in draft.Cards.Keys.ToArray()){var card=catalog.Card(id);if(!pool.Contains(card)||card.CardClass!="NEUTRAL"&&card.CardClass!=draft.ClassId){removed+=draft.Cards[id];draft.Cards.Remove(id);}}return removed;}
 void PersistDraft(){if(previewRun)return;try{if(!libraryWritable){Notice("卡组库读取失败，暂停写入以保护原文件。");return;}if(!File.Exists(Path.Combine(DeckDirectory,"library.json")))DeckStorage.SaveLibrary(DeckDirectory,savedDecks);if(draftWritable)JsonData.Write(Path.Combine(DeckDirectory,"draft.json"),draft);}catch(IOException){Notice("卡组保存失败，原有卡组文件已保留。");}catch(UnauthorizedAccessException){Notice("用户数据目录不可写，原有卡组文件已保留。");}}
 void Notice(string text){deckNotice.Text=text;deckNotice.ToolTip=text;status.Text=text;}
 void RefreshDeck(){
  if(deckRows==null)return;RefreshSavedDeckPicker();deckRows.Children.Clear();foreach(var pair in draft.Cards.OrderBy(p=>catalog.Card(p.Key).Cost).ThenBy(p=>catalog.Card(p.Key).Name,StringComparer.Ordinal)){var card=catalog.Card(pair.Key);deckRows.Children.Add(CardLine(card,pair.Value,true));}
  int size=catalog.Pool(draft.PoolId).DeckSize;deckCount.Text="卡组  "+draft.Count+" / "+size;exportButton.IsEnabled=saveDeckButton.IsEnabled=draft.Count==size;exportButton.Opacity=saveDeckButton.Opacity=draft.Count==size?1:.4;RefreshCollection();ApplyFonts(deckPanel);
 }
 void RefreshVersionPicker(){
  if(versionPicker==null)return;var previous=versionPicker.SelectedItem as Choice;
  var versions=catalog.Sets.Where(set=>!set.HiddenInBrowser).GroupBy(set=>CollectionVersionKey(set.Id));
  var choices=new[]{new Choice("","全部版本")}.Concat(versions.Select(group=>new Choice(group.Key,CollectionVersionLabel(group.Key,group.First().Name)))).ToArray();
  bool old=refreshingDeck;refreshingDeck=true;try{versionPicker.ItemsSource=choices;versionPicker.SelectedItem=choices.FirstOrDefault(c=>previous!=null&&c.Key==previous.Key)??choices[0];}finally{refreshingDeck=old;}
 }
 static string CollectionVersionKey(string setId){
  if(setId==GvgClassRules.SetId)return GvgRules.SetId;
  return setId==TournamentClassRules.SetId?TournamentRules.SetId:setId;
 }
 static string CollectionVersionLabel(string key,string fallback){
  if(key==GvgRules.SetId)return "地精大战侏儒";
  return key==TournamentRules.SetId?"冠军的试炼":fallback;
 }
 void RefreshCollection(){
  if(collectionRows==null||costPicker==null||typePicker==null||classFilterPicker==null||showAllCards==null)return;var pool=catalog.Pool(draft.PoolId);string query=searchBox.Text.Trim();string cost=costPicker.SelectedItem==null?"all":((Choice)costPicker.SelectedItem).Key;string type=typePicker.SelectedItem==null?"all":((Choice)typePicker.SelectedItem).Key;
  string ownership=classFilterPicker.SelectedItem==null?"available":((Choice)classFilterPicker.SelectedItem).Key;
  string version=versionPicker==null||versionPicker.SelectedItem==null?"":((Choice)versionPicker.SelectedItem).Key;
  var all=version.Length==0?catalog.BrowseForPool(pool):catalog.Sets
   .Where(set=>!set.HiddenInBrowser&&CollectionVersionKey(set.Id)==version)
   .SelectMany(set=>catalog.BrowseForPool(pool,set.Id)).Distinct()
   .OrderBy(card=>card.Cost).ThenBy(card=>card.Name,StringComparer.Ordinal).ToList();
  bool browseAll=showAllCards.IsChecked==true;
  filteredCards=all.Where(card=>{
   bool neutral=card.CardClass=="NEUTRAL";
   bool ownClass=card.CardClass==draft.ClassId;
   bool classMatches=ownership=="class"?!neutral&&(ownClass||browseAll)
    :ownership=="neutral"?neutral:ownClass||neutral||browseAll||query.Length>0;
   return classMatches&&(type=="all"||card.Type==type)
    &&(cost=="all"||(cost=="7"?card.Cost>=7:card.Cost==Int32.Parse(cost)))&&CardSearchMatches(card,query);
  }).ToList();
  collectionRows.Children.Clear();foreach(var card in filteredCards)collectionRows.Children.Add(CardLine(card,0,false));if(filteredCards.Count==0){var empty=T(query.Length==0?"当前筛选无卡牌。":"没有匹配的卡牌。可检查费用、类型筛选，或缩短关键词。",10,Muted);empty.TextWrapping=TextWrapping.Wrap;empty.Margin=new Thickness(5);collectionRows.Children.Add(empty);}collectionCount.Text="牌库  "+filteredCards.Count+" / "+all.Count;ApplyFonts(deckPanel);
 }
 static bool CardSearchMatches(CardRecord card,string query){string text=(card.Name+" "+card.Text+" "+card.Id).Normalize(NormalizationForm.FormKC);var words=query.Normalize(NormalizationForm.FormKC).Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);return words.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0);}
 Border CardLine(CardRecord card,int quantity,bool inDeck){
  bool unavailable=!matchRules.Supports(card);bool hasRace=card.Type=="MINION"&&!String.IsNullOrEmpty(card.Race);bool hasSubline=unavailable||hasRace;var grid=new Grid{Height=hasSubline?33:22};grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(22)});if(hasSubline)grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(11)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(35)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(inDeck?18:32)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(19)});
  var cost=T(card.Cost.ToString(),10,Muted);cost.TextAlignment=TextAlignment.Center;grid.Children.Add(cost);var name=T(card.Name,10,Ink);name.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(name,1);grid.Children.Add(name);
  var rarity=T(RarityLabel(card.Rarity),8,RarityColor(card.Rarity));rarity.TextAlignment=TextAlignment.Center;rarity.VerticalAlignment=VerticalAlignment.Center;rarity.ToolTip="卡牌品质："+RarityLabel(card.Rarity);Grid.SetColumn(rarity,2);grid.Children.Add(rarity);
  if(hasSubline){var labels=new List<string>();if(unavailable)labels.Add("【暂不可用】");if(hasRace&&!unavailable)labels.Add("种族："+RaceLabel(card.Race));var marker=T(String.Join(" · ",labels),8,Muted);Grid.SetColumn(marker,1);Grid.SetColumnSpan(marker,2);Put(grid,marker,1);}grid.ToolTip=card.Name+" · "+DisplayedCardSetName(card)+(!catalog.Pool(draft.PoolId).Contains(card)?" · 当前卡池外，仅供查看":"")+" · "+RarityLabel(card.Rarity)+(hasRace?" · 种族："+RaceLabel(card.Race):"")+(unavailable?" · 效果尚未实现，暂不能用于对战。":"");
  var values=T(inDeck?"×"+quantity:card.Type=="MINION"?card.Attack+"/"+card.Health:card.Type=="WEAPON"?card.Attack+"/"+card.Durability:"法",10,Muted);values.TextAlignment=TextAlignment.Center;Grid.SetColumn(values,3);grid.Children.Add(values);
  var action=Btn(inDeck?"−":"+",19);action.Height=21;action.FontSize=12;action.ToolTip=inDeck?"移除一张":!catalog.Pool(draft.PoolId).Contains(card)?"当前卡池外，仅供查看":"加入一张";action.IsEnabled=inDeck||DeckRules.CanAdd(catalog,draft,card)==null;action.Opacity=action.IsEnabled?1:.3;action.Click+=(s,e)=>{if(inDeck)RemoveCard(card);else AddCard(card);};Grid.SetColumn(action,4);grid.Children.Add(action);
  var row=new Border{Child=grid,Tag=card,ToolTip=grid.ToolTip,Background=B("#FFFFFF"),BorderBrush=Line,BorderThickness=new Thickness(0,0,0,1),Cursor=Cursors.Hand,Focusable=true};row.MouseLeftButtonDown+=(s,e)=>{ShowDeckCard(card);if(e.ClickCount==2&&!inDeck)AddCard(card);};row.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){ShowDeckCard(card);e.Handled=true;}};row.MouseEnter+=(s,e)=>row.Background=B("#F3F6F5");row.MouseLeave+=(s,e)=>row.Background=B("#FFFFFF");return row;
 }
 string DisplayedCardSetName(CardRecord card){
  var pool=catalog.Pool(draft.PoolId);
  string setId=card.SetId;
  if(pool.CardReplacements!=null){
   var original=pool.CardReplacements.FirstOrDefault(p=>p.Value==card.Id);
   if(original.Key!=null)setId=catalog.Card(original.Key).SetId;
  }
  var set=catalog.Sets.First(item=>item.Id==setId);
  return CollectionVersionLabel(CollectionVersionKey(setId),set.Name);
 }
 void ShowDeckCard(CardRecord card){cardInfo.Text=card.Name+" · "+DisplayedCardSetName(card)+(!catalog.Pool(draft.PoolId).Contains(card)?" · 当前卡池外，仅供查看":"")+(matchRules.Supports(card)?"":"【暂不可用】")+"  ·  "+card.Cost+" 费  ·  "+ClassLabel(card.CardClass)+"  ·  "+RarityLabel(card.Rarity)+(card.Type=="MINION"&&!String.IsNullOrEmpty(card.Race)?"  ·  种族："+RaceLabel(card.Race):"")+(matchRules.Supports(card)?" · 可对战":ExplorerRules.IsLockedCard(catalog,card)?" · 尚未开放":" · 效果未实现");cardEffect.Text=card.Text.Length>0?card.Text:"无特殊效果。";}
 void AddCard(CardRecord card){string error=DeckRules.CanAdd(catalog,draft,card);if(error!=null){Notice(error);return;}RememberDeck();int count;draft.Cards.TryGetValue(card.Id,out count);draft.Cards[card.Id]=count+1;RefreshDeck();PersistDraft();ShowDeckCard(card);Notice("已加入 "+card.Name);}
 void RemoveCard(CardRecord card){if(!draft.Cards.ContainsKey(card.Id))return;RememberDeck();if(--draft.Cards[card.Id]==0)draft.Cards.Remove(card.Id);RefreshDeck();PersistDraft();Notice("已移除一张 "+card.Name);}
 static string ClassLabel(string value){var item=Classes.FirstOrDefault(c=>c.Key==value);return item==null?"中立":item.Label;}
 static string RarityLabel(string value){return value=="LEGENDARY"?"传说":value=="EPIC"?"史诗":value=="RARE"?"稀有":"普通";}
 Brush RarityColor(string value){if(!fullEffects)return Muted;return value=="LEGENDARY"?B("#B45B13"):value=="EPIC"?B("#8756A4"):value=="RARE"?B("#3478B8"):Muted;}
 bool ChangeDraftClass(string classId){if(draft.ClassId==classId)return false;bool detached=savedDecks.Any(saved=>saved.Id==draft.Id);if(detached){draft.Id=Guid.NewGuid().ToString("N");draft.Name="新卡组";}draft.ClassId=classId;return detached;}
 void SaveFinishedDeck(){if(!libraryWritable){Notice("卡组库读取失败，暂停保存以保护原文件。");return;}try{string code=DeckCode.Encode(catalog,draft);var saved=savedDecks.FirstOrDefault(d=>d.Id==draft.Id);if(saved==null){saved=new SavedDeck{Id=draft.Id};savedDecks.Add(saved);}saved.Name=String.IsNullOrWhiteSpace(draft.Name)?"未命名卡组":draft.Name;saved.Code=code;saved.PoolId=draft.PoolId;if(!previewRun){DeckStorage.SaveLibrary(DeckDirectory,savedDecks);if(draftWritable)JsonData.Write(Path.Combine(DeckDirectory,"draft.json"),draft);}RefreshSavedDeckPicker();Notice("卡组已按代码保存；其他已保存卡组不会被覆盖。");}catch(Exception ex){if(!(ex is InvalidDataException||ex is IOException||ex is UnauthorizedAccessException))throw;Notice(ex.Message);}}
 void ExportCode(){try{string code=DeckCode.Encode(catalog,draft);if(!previewRun)Clipboard.SetText(code);ShowCodeUi(false,code);codeNotice.Text="代码已复制，可直接分享。";}catch(Exception ex){if(!(ex is InvalidDataException||ex is System.Runtime.InteropServices.ExternalException))throw;Notice(ex.Message);}}
 void ShowCodeUi(bool importing,string value){
  ClearCodePanel();codePanel=new Grid{Margin=new Thickness(10,9,10,8),Background=B("#FFFFFF")};foreach(var h in new double[]{24,-1,30,28})codePanel.RowDefinitions.Add(new RowDefinition{Height=h<0?new GridLength(1,GridUnitType.Star):new GridLength(h)});Put(shell,codePanel,4);deckPanel.Visibility=Visibility.Collapsed;
  codePanel.Children.Add(T(importing?"导入卡组代码":"卡组代码",12,Ink));codeBox=Input(value??"");codeBox.AcceptsReturn=true;codeBox.TextWrapping=TextWrapping.Wrap;codeBox.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;codeBox.VerticalContentAlignment=VerticalAlignment.Top;codeBox.Padding=new Thickness(5);codeBox.IsReadOnly=!importing;Put(codePanel,codeBox,1);
  codeNotice=T(importing?"粘贴代码后导入；确认成功才替换当前卡组。":"",10,Muted);codeNotice.TextWrapping=TextWrapping.Wrap;Put(codePanel,codeNotice,2);
  if(importing)codeBox.TextChanged+=(s,e)=>codeNotice.Text="粘贴代码后导入；确认成功才替换当前卡组。";
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var back=Btn("返回",48);back.Click+=(s,e)=>{ClearCodePanel();ShowPage("deck");};actions.Children.Add(back);
  if(importing){var paste=Btn("粘贴并导入",85);paste.Click+=(s,e)=>{try{codeBox.Text=Clipboard.GetText();ImportCodeText(codeBox.Text);}catch(System.Runtime.InteropServices.ExternalException){codeNotice.Text="剪贴板暂不可用，请手动粘贴。";}};actions.Children.Add(paste);var import=Btn("导入",48);import.Click+=(s,e)=>ImportCodeText(codeBox.Text);actions.Children.Add(import);}else{var copy=Btn("复制代码",70);copy.Click+=(s,e)=>{try{Clipboard.SetText(codeBox.Text);codeNotice.Text="已复制代码。";}catch(System.Runtime.InteropServices.ExternalException){codeNotice.Text="请选中代码手动复制。";}};actions.Children.Add(copy);}Put(codePanel,actions,3);ApplyFonts(codePanel);
 }
 void ImportCodeText(string code){try{DeckRules.RequirePlayablePool(catalog,draft.PoolId);var imported=DeckCode.Decode(catalog,code,draft.PoolId);RememberDeck();draft=imported;SyncDeckControls();RefreshDeck();PersistDraft();ClearCodePanel();ShowPage("deck");Notice("已导入 "+draft.Count+" 张 "+ClassLabel(draft.ClassId)+" 卡组。");}catch(InvalidDataException ex){codeNotice.Text=ex.Message;}}
 void ClearCodePanel(){if(codePanel!=null){shell.Children.Remove(codePanel);codePanel=null;}}
 void RefreshSavedDeckPicker(){if(savedDeckPicker==null)return;syncingSavedDeckPicker=true;try{var choices=new List<Choice>{new Choice("","当前草稿")};choices.AddRange(savedDecks.Select(d=>new Choice(d.Id,d.Name)));savedDeckPicker.ItemsSource=choices.ToArray();savedDeckPicker.SelectedItem=choices.FirstOrDefault(c=>c.Key==draft.Id)??choices[0];}finally{syncingSavedDeckPicker=false;}}
 void LoadSavedDeckForEditing(SavedDeck saved){try{var deck=DeckCode.Decode(catalog,saved.Code,String.IsNullOrWhiteSpace(saved.PoolId)?draft.PoolId:saved.PoolId);deck.Id=saved.Id;deck.Name=saved.Name;RememberDeck();draft=deck;SyncDeckControls();RefreshDeck();PersistDraft();ClearCodePanel();ShowPage("deck");Notice("正在编辑 "+saved.Name);}catch(InvalidDataException ex){RefreshSavedDeckPicker();Notice(ex.Message);}}
 void ShowSavedDecks(){ShowSavedDecks(false);}
 void ShowSavedDecks(bool deleting){
  ClearCodePanel();codePanel=new Grid{Margin=new Thickness(10,9,10,8),Background=B("#FFFFFF")};codePanel.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});codePanel.RowDefinitions.Add(new RowDefinition());codePanel.RowDefinitions.Add(new RowDefinition{Height=new GridLength(28)});Put(shell,codePanel,4);deckPanel.Visibility=Visibility.Collapsed;
  codePanel.Children.Add(T(deleting?"选择要删除的卡组":"已保存的卡组",12,Ink));savedRows=new StackPanel();if(savedDecks.Count==0)savedRows.Children.Add(T(deleting?"尚无可删除的已保存卡组。":"尚未保存卡组。组满后点“保存”。",10,Muted));
  foreach(var saved in savedDecks){var button=Btn(deleting?"删除  ·  "+saved.Name:saved.Name,240);button.HorizontalAlignment=HorizontalAlignment.Left;button.Click+=(s,e)=>{
   if(deleting){var answer=MessageBox.Show(this,"确定删除卡组“"+saved.Name+"”吗？此操作无法撤销。","确认删除卡组",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No);if(answer!=MessageBoxResult.Yes)return;if(RemoveSavedDeck(saved.Id)){ShowSavedDecks(true);Notice("已删除卡组："+saved.Name);}return;}
   LoadSavedDeckForEditing(saved);
  };savedRows.Children.Add(button);}
  Put(codePanel,new ScrollViewer{Content=savedRows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},1);var back=Btn(deleting?"取消":"返回",48);back.HorizontalAlignment=HorizontalAlignment.Right;back.Click+=(s,e)=>{ClearCodePanel();ShowPage("deck");};Put(codePanel,back,2);ApplyFonts(codePanel);
 }
 bool RemoveSavedDeck(string id){if(!libraryWritable){Notice("卡组库读取失败，暂停删除以保护原文件。");return false;}int index=savedDecks.FindIndex(saved=>saved.Id==id);if(index<0)return false;var removed=savedDecks[index];savedDecks.RemoveAt(index);string oldId=draft.Id;if(oldId==id)draft.Id=Guid.NewGuid().ToString("N");try{if(!previewRun)DeckStorage.SaveLibrary(DeckDirectory,savedDecks);RefreshSavedDeckPicker();return true;}catch(Exception ex){if(!(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidDataException))throw;savedDecks.Insert(index,removed);draft.Id=oldId;Notice("删除失败，卡组已保留："+ex.Message);return false;}}
 void ApplyDeckMode(){if(deckPanel==null)return;deckPanel.RowDefinitions[3].Height=new GridLength(minimal?34:42);ApplyFonts(deckPanel);}
}
}
