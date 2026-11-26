using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace Touchfish {
public partial class PanelWindow {
 void VerifyCollectionSearch(string folder){
  var all=catalog.BrowseForPool(catalog.Pool(draft.PoolId));
  searchBox.Text="海盗";
  var pirates=all.Where(c=>c.Race=="PIRATE"||c.Name.Contains("海盗")||c.Text.Contains("海盗")).ToArray();
  Check(filteredCards.SequenceEqual(pirates)&&pirates.Any(c=>c.Id=="VAN_NEW1_018")&&pirates.Any(c=>c.Id=="GVG_075"),"Pirate search includes all tribe members and synergy effects");
  searchBox.Text="传说";
  Check(filteredCards.SequenceEqual(all.Where(c=>c.Rarity=="LEGENDARY")),"Legendary search matches catalog rarity exactly");
  searchBox.Text="海盗　传说";
  Check(filteredCards.SequenceEqual(pirates.Where(c=>c.Rarity=="LEGENDARY"))&&filteredCards.Any(c=>c.Id=="AT_070"),"Mixed tribe and rarity includes Skycap'n Kragg");
  Render(Path.Combine(folder,"collection-search-pirates.png"));
  searchBox.Text="拥有冲锋";
  Check(filteredCards.Any(c=>c.Id=="VAN_CS2_146"),"Effect phrase spans a historical line break");
  searchBox.Text="法强";
  Check(filteredCards.Any(c=>c.Id=="VAN_EX1_563"),"Mechanic alias finds spell damage");
  searchBox.Text="拼点";
  Check(filteredCards.Count==8&&filteredCards.Any(c=>c.Id=="AT_058"),"Joust keyword finds the historical reveal effects");
  searchBox.Text="战吼";
  Check(all.Where(c=>c.Mechanics!=null&&c.Mechanics.Contains("BATTLECRY")).All(filteredCards.Contains),"Battlecry search includes all tagged cards");
  searchBox.Text="ａｔ＿０７０";
  Check(filteredCards.Count==1&&filteredCards[0].Id=="AT_070","Rapid consecutive queries show the latest fullwidth card ID");
  searchBox.Text="";showAllCards.IsChecked=true;UpdateLayout();
  Check(filteredCards.Count==all.Count&&collectionRows.ItemContainerGenerator.ContainerFromIndex(0)!=null&&collectionRows.ItemContainerGenerator.ContainerFromIndex(100)==null,"Full library creates controls only near the viewport");
  var last=filteredCards.Last();collectionRows.ScrollIntoView(last);UpdateLayout();
  var container=(ListBoxItem)collectionRows.ItemContainerGenerator.ContainerFromItem(last);
  Check(container!=null&&((Border)container.Content).Tag==last,"Scrolling to the end realizes the correct card");
  searchBox.Text="海盗";ApplyMode(true);UpdateLayout();
  Check(collectionRows.ItemContainerGenerator.ContainerFromIndex(0)!=null,"Changing the query after scrolling returns to the first match");
  Render(Path.Combine(folder,"collection-search-minimal.png"));
  searchBox.Text="无匹配结果xyz";UpdateLayout();
  Check(filteredCards.Count==0&&collectionEmpty.IsVisible,"Empty result hint stays visible");
  searchBox.Text="";showAllCards.IsChecked=false;ApplyMode(false);
 }
 public void PreviewTournament(string folder){
  Directory.CreateDirectory(folder);previewRun=true;
  LeaveLan();ApplyTheme(true,true);
  if(VerificationRun.Visual){
   var original=draft;
   draft=new DeckDocument{ClassId="DRUID",PoolId=TournamentClassRules.PoolId};
   draft.Cards["VAN_CS2_231"]=1;
   SyncDeckControls();RefreshDeck();showAllCards.IsChecked=false;
   var beforeFilters=draft.Copy();
   var versions=versionPicker.Items.Cast<Choice>().ToArray();
   versionPicker.SelectedItem=versions.Single(c=>c.Key==TournamentRules.SetId);
   classFilterPicker.SelectedItem=classFilterPicker.Items.Cast<Choice>().Single(c=>c.Key=="class");
   Check(filteredCards.Count==9&&filteredCards.All(c=>c.CardClass=="DRUID"&&matchRules.Supports(c)),"Tournament class filter exposes all nine Druid cards");
   Check(((Choice)versionPicker.SelectedItem).Label=="冠军的试炼"&&versions.All(c=>c.Key!=TournamentClassRules.SetId),"Both Tournament packs share a single version selector entry");
   Check(SameDeck(draft,beforeFilters),"Changing version and class filters preserves the deck");
   showAllCards.IsChecked=true;
   Check(filteredCards.Count==82&&filteredCards.All(c=>c.CardClass!="NEUTRAL"&&matchRules.Supports(c)),"All-library class filter shows all 82 Tournament class cards");
   var foreign=filteredCards.Single(c=>c.Id=="AT_008");
   Check(!((Grid)CardLine(foreign,0,false).Child).Children.OfType<Button>().Single().IsEnabled,"Other class cards can be browsed without being added to the draft");
   classFilterPicker.SelectedItem=classFilterPicker.Items.Cast<Choice>().Single(c=>c.Key=="neutral");
   Check(filteredCards.Count==50&&filteredCards.All(c=>c.CardClass=="NEUTRAL"&&matchRules.Supports(c)),"Tournament neutral filter shows all 50 neutral cards");
   classFilterPicker.SelectedIndex=0;
   Check(filteredCards.Count==132,"All-library view shows both Tournament packs");
   showAllCards.IsChecked=false;
   classFilterPicker.SelectedItem=classFilterPicker.Items.Cast<Choice>().Single(c=>c.Key=="class");
   classPicker.SelectedItem=Classes.Single(c=>c.Key=="HUNTER");
   Check(filteredCards.Count==10&&filteredCards.All(c=>c.CardClass=="HUNTER"),"Changing the draft profession refreshes the Tournament class filter");
   classPicker.SelectedItem=Classes.Single(c=>c.Key=="DRUID");
   ApplyMode(false);ShowPage("deck");Render(Path.Combine(folder,"tournament-profession-normal.png"));
   ApplyMode(true);Render(Path.Combine(folder,"tournament-profession-minimal.png"));
   searchBox.Text="AT_045";var aviana=filteredCards.Single();
   Check(aviana.Id=="AT_045"&&aviana.DbfId==2796,"Deck builder finds the historical Aviana record");
   ApplyMode(false);ShowPage("deck");ShowDeckCard(aviana);Render(Path.Combine(folder,"tournament-aviana.png"));
   searchBox.Text="";classFilterPicker.SelectedIndex=0;versionPicker.SelectedIndex=0;
   VerifyCollectionSearch(folder);
   draft=original;SyncDeckControls();RefreshDeck();
  }
  if(VerificationRun.Network){
   var guest=new PanelWindow(true){ShowInTaskbar=false,Opacity=0};guest.Show();
   try{
    lanPort.Text=FreeLanPort().ToString();lanClass.SelectedItem=Classes.First(c=>c.Key=="SHAMAN");lanDeck.SelectedIndex=0;BeginLan(true);
    guest.lanClass.SelectedItem=Classes.First(c=>c.Key=="SHAMAN");guest.lanDeck.SelectedIndex=0;guest.lanAddress.Text="127.0.0.1:"+lanPort.Text;guest.BeginLan(false);
    PumpLan(()=>lanReady&&guest.lanReady,"Tournament peers connect");
    DoMatch("mulligan",selectedIds:new int[0]);PumpLan(()=>guest.match.Players[0].MulliganDone,"Host mulligan synchronized");guest.DoMatch("mulligan",selectedIds:new int[0]);
    PumpLan(()=>match.MulliganComplete&&!guest.lanWaiting,"Mulligans finish");
    var g=lanAuthority.Game;
    Check(g.CardPoolId==TournamentClassRules.PoolId&&guest.match.CardPoolId==g.CardPoolId,"Both peers use the open Tournament pool");
    g.Active=1;g.Players[1].HeroAbilityId="tgt:upgraded-SHAMAN";g.Players[1].PowerUsed=false;g.Players[1].PowersThisTurn=0;g.Players[1].Mana=10;g.Players[1].Board.Clear();
    PublishLan();
    PumpLan(()=>guest.match.Players[1].HeroAbilityId=="tgt:upgraded-SHAMAN","Upgrade synchronized");
    guest.DoMatch("power",choice:"VAN_CS2_050");
    PumpLan(()=>!guest.lanWaiting&&guest.match.Players[1].Board.Count==1,"Chosen totem synchronized through command");
    Check(g.Players[1].PowersThisGame==1&&guest.match.Players[1].PowersThisGame==1,"Public counters synchronized");
    g.Players[1].ClassId="DRUID";g.Players[1].Mana=10;g.Players[1].Board.Clear();g.Players[1].Hand.Clear();
    int aviana=NaxxUiHand(g,"AT_045",1),minion=NaxxUiHand(g,"VAN_CS2_182",1);PublishLan();
    PumpLan(()=>guest.match.Players[1].Hand.Any(h=>h.Id==aviana),"Aviana reaches the owning guest");
    guest.DoMatch("play",aviana,null,0);
    PumpLan(()=>!guest.lanWaiting&&guest.match.Players[1].Board.Any(u=>u.CardId=="AT_045"),"Aviana resolves through a guest command");
    Check(guest.match.HandCost(1,guest.match.Players[1].Hand.Single(h=>h.Id==minion))==1,"Guest sees the aura-adjusted cost");
    Check(LanProtocol.View(g,0,"tournament-privacy",1).Players[1].Hand.Length==0,"The public projection does not expose the remaining hand");
    guest.DoMatch("play",minion,null,1);
    PumpLan(()=>!guest.lanWaiting&&guest.match.Players[1].Board.Count==2,"Guest spends the final mana to summon the discounted minion");
    Check(g.Players[1].Mana==0&&guest.match.Players[1].Mana==0,"Authority and guest agree on the discounted payment");
    var hostOrder=new[]{"VAN_CS2_029","AT_082","VAN_CS2_024"};
    var guestOrder=new[]{"VAN_CS2_023","AT_125","VAN_CS2_027"};
    g.Players[0].Deck.Clear();g.Players[0].Deck.AddRange(hostOrder);
    g.Players[1].Deck.Clear();g.Players[1].Deck.AddRange(guestOrder);
    g.Players[1].Mana=10;
    int jouster=NaxxUiHand(g,"AT_133",1);PublishLan();
    PumpLan(()=>guest.match.Players[1].Hand.Any(card=>card.Id==jouster),"Jouster ready in guest hand");
    guest.DoMatch("play",jouster,null,2);
    PumpLan(()=>!guest.lanWaiting&&guest.match.VisualEvents.Any(item=>item.Kind=="joust"),"Guest receives the joust visual event");
    var comparison=guest.match.VisualEvents.Single(item=>item.Kind=="joust");
    Check(comparison.JoustCards.SequenceEqual(new[]{"AT_082","AT_125"})&&comparison.JoustWinner==1
     &&g.VisualEvents.Any(item=>item.Kind=="joust"&&item.JoustWinner==1),"Both ends get the same public comparison");
    Check(guest.match.Players[0].Deck.All(card=>card=="HIDDEN")&&g.Players[0].Deck.SequenceEqual(hostOrder)
     &&g.Players[1].Deck.SequenceEqual(guestOrder),"LAN reveal preserves authoritative deck order and hides the rest of the opposing deck");
   }finally{guest.LeaveLan();guest.Close();LeaveLan();}
  }
  if(!VerificationRun.Visual)return;
  localTest=true;agentMatch=false;awaitHandoff=false;matchViewSeat=0;
  match=new MatchEngine(catalog,matchRules.TrainingDeck("SHAMAN"),matchRules.TrainingDeck("MAGE"),57,true);
  match.Mulligan(0,new int[0]);match.Mulligan(1,new int[0]);match.Players[0].HeroAbilityId="tgt:upgraded-SHAMAN";
  foreach(bool compact in new[]{false,true}){
   ApplyMode(compact);BeginPower();ShowPage("match");
   Check(pending=="totem","Totem choice available in both modes");
   Render(Path.Combine(folder,compact?"tournament-totem-minimal.png":"tournament-totem-normal.png"));
   ClearPending();
  }
 }
}
}
