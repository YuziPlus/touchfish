using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Touchfish {
public partial class PanelWindow {
 CardCatalog indexedCatalog;
 PoolDefinition indexedPool;
 string indexedVersion;
 List<CardRecord> browseCards;
 readonly Dictionary<CardRecord,string> searchText=new Dictionary<CardRecord,string>();

 static readonly Dictionary<string,string> mechanicLabels=new Dictionary<string,string>{
  {"BATTLECRY","战吼"},{"DEATHRATTLE","亡语"},{"TAUNT","嘲讽"},{"AURA","光环"},
  {"SECRET","奥秘"},{"INSPIRE","激励"},{"CHARGE","冲锋"},{"OVERLOAD","过载"},
  {"CHOOSE_ONE","抉择"},{"COMBO","连击"},{"DIVINE_SHIELD","圣盾"},{"STEALTH","潜行"},
  {"SPELLPOWER","法术伤害 法强"},{"FREEZE","冻结 冰冻"},{"WINDFURY","风怒"},
  {"DISCOVER","发现"},{"FORGETFUL","攻击错误的敌人"},{"CANT_ATTACK","无法攻击"},
  {"ADJACENT_BUFF","相邻"},{"SILENCE","沉默"},{"ELUSIVE","无法成为法术或英雄技能的目标"},
  {"POISONOUS","剧毒"},{"ENRAGED","激怒"},{"HEROPOWER_DAMAGE","英雄技能伤害"}
 };

 List<CardRecord> BrowseCards(PoolDefinition pool,string version){
  if(indexedCatalog!=catalog){
   indexedCatalog=catalog;
   searchText.Clear();
   browseCards=null;
  }
  if(browseCards!=null&&pool==indexedPool&&version==indexedVersion)return browseCards;
  indexedPool=pool;
  indexedVersion=version;
  browseCards=version.Length==0?catalog.BrowseForPool(pool):catalog.Sets
   .Where(set=>!set.HiddenInBrowser&&CollectionVersionKey(set.Id)==version)
   .SelectMany(set=>catalog.BrowseForPool(pool,set.Id)).Distinct()
   .OrderBy(card=>card.Cost).ThenBy(card=>card.Name,StringComparer.Ordinal).ToList();
  return browseCards;
 }

 string SearchText(CardRecord card){
  string text;
  if(searchText.TryGetValue(card,out text))return text;
  // Line breaks in historical card text mark visual wrapping, even inside a word.
  string effect=(card.Text??"").Replace("\r","").Replace("\n","");
  string type=card.Type=="MINION"?"随从":card.Type=="WEAPON"?"武器":"法术";
  var fields=new List<string>{card.Name,effect,card.Id,card.BaseId,card.Race,
   RaceLabel(card.Race??""),card.Type,type,card.CardClass,ClassLabel(card.CardClass),
   card.Rarity,RarityLabel(card.Rarity),card.Cost+"费"};
  foreach(var tag in card.Mechanics??new string[0]){
   fields.Add(tag);
   string label;
   if(mechanicLabels.TryGetValue(tag,out label))fields.Add(label);
  }
  if(card.SpellDamage>0)fields.Add("法术伤害 法强");
  if(card.Overload>0)fields.Add("过载");
  if(effect.Contains("揭示双方牌库里的一张随从牌")&&effect.Contains("法力值消耗较大"))fields.Add("拼点 Joust");
  text=String.Join(" ",fields).Normalize(NormalizationForm.FormKC);
  searchText.Add(card,text);
  return text;
 }

 bool CardSearchMatches(CardRecord card,string[] words){
  foreach(string word in words){
   if(word=="传说"||word=="史诗"||word=="稀有"||word=="普通"){
    if(RarityLabel(card.Rarity)!=word)return false;
   }else if(SearchText(card).IndexOf(word,StringComparison.OrdinalIgnoreCase)<0)return false;
  }
  return true;
 }
}

// Let WPF request rows as they enter the viewport, including when using the scrollbar.
sealed class CollectionCardList : ListBox {
 readonly Func<CardRecord,Border> buildRow;
 ScrollViewer viewport;
 public CollectionCardList(Func<CardRecord,Border> buildRow){
  this.buildRow=buildRow;
  BorderThickness=new Thickness(0);
  Padding=new Thickness(0);
  Background=System.Windows.Media.Brushes.Transparent;
  ScrollViewer.SetCanContentScroll(this,true);
  ScrollViewer.SetHorizontalScrollBarVisibility(this,ScrollBarVisibility.Disabled);
  VirtualizingPanel.SetIsVirtualizing(this,true);
  VirtualizingPanel.SetVirtualizationMode(this,VirtualizationMode.Recycling);
  VirtualizingPanel.SetCacheLength(this,new VirtualizationCacheLength(0));
  AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((sender,e)=>{
   viewport=e.OriginalSource as ScrollViewer;
  }));
  var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
  var style=new Style(typeof(ListBoxItem));
  style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=presenter}));
  style.Setters.Add(new Setter(FocusableProperty,false));
  ItemContainerStyle=style;
 }
 public void ScrollResultsToTop(){
  if(viewport!=null&&viewport.VerticalOffset>0)viewport.ScrollToTop();
 }
 protected override void PrepareContainerForItemOverride(DependencyObject element,object item){
  base.PrepareContainerForItemOverride(element,item);
  ((ListBoxItem)element).Content=buildRow((CardRecord)item);
 }
}
}
