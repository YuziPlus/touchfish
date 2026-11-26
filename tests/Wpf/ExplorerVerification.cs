using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Touchfish {
public partial class PanelWindow {
 public void PreviewExplorers(string folder){
  Directory.CreateDirectory(folder);
  previewRun=true;
  LeaveLan();
  if(ExplorerRules.IsLockedPool(catalog.Pool(ExplorerRules.PoolId))){
   if(VerificationRun.Visual)PreviewLockedExplorers(folder);
   return;
  }
  if(VerificationRun.Visual){
   var original=draft;
   draft=new DeckDocument{ClassId="WARLOCK",PoolId=ExplorerRules.PoolId};
   SyncDeckControls();RefreshDeck();
   versionPicker.SelectedItem=versionPicker.Items.Cast<Choice>().Single(c=>c.Key==ExplorerRules.PoolId);
   classFilterPicker.SelectedItem=classFilterPicker.Items.Cast<Choice>().Single(c=>c.Key=="class");
   Check(filteredCards.Count==3&&filteredCards.All(c=>c.CardClass=="WARLOCK"),"Explorer version filters three Warlock cards");
   showAllCards.IsChecked=true;
   Check(filteredCards.Count==27,"Explorer version can browse all 27 class cards");
   classFilterPicker.SelectedItem=classFilterPicker.Items.Cast<Choice>().Single(c=>c.Key=="neutral");
   Check(filteredCards.Count==18,"Explorer version limits neutral cards to 18 collectibles");
   ApplyTheme(true,true);ApplyMode(false);ShowPage("deck");Render(Path.Combine(folder,"explorers-deck.png"));
   showAllCards.IsChecked=false;versionPicker.SelectedIndex=0;classFilterPicker.SelectedIndex=0;
   draft=original;SyncDeckControls();RefreshDeck();
   foreach(bool compact in new[]{false,true}){
    ApplyMode(compact);
    match=MatchFixture("WARLOCK");match.CardPoolId=ExplorerRules.PoolId;
    match.Summon(0,"LOE_077");
    int peddler=NaxxUiHand(match,"LOE_023");
    localTest=true;agentMatch=false;matchViewSeat=0;awaitHandoff=false;
    ShowPage("match");BeginCard(peddler);ClickEmptySlot(0,1);
    var decision=match.ObserveDecision(0);
    Check(decision!=null&&!CanMatchInput(),"Discover takes over only the action panel");
    Render(Path.Combine(folder,compact?"explorers-discover-minimal.png":"explorers-discover-normal.png"));
    var buttons=DecisionButtons(livePanel,decision.Options.Select(o=>o.Id).ToArray()).ToArray();
    Check(buttons.Length==3&&buttons.All(b=>b.IsEnabled&&b.ActualWidth>0),"All options displayed at original window size");
    buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var second=match.ObserveDecision(0);
    Check(second!=null&&second.Id!=decision.Id,"Second battlecry opens a new prompt");
    DecisionButtons(livePanel,second.Options.Select(o=>o.Id).ToArray()).First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(!match.HasPendingDecision&&match.Players[0].Hand.Count==2,"Both choices resolve in normal and compact modes");
   }
  }
  if(VerificationRun.Network)VerifyExplorerNetwork();
 }

 void PreviewLockedExplorers(string folder){
  var original=draft;
  draft=new DeckDocument{ClassId="WARLOCK",PoolId=TournamentClassRules.PoolId};
  SyncDeckControls();RefreshDeck();
  Check(poolPicker.Items.Cast<Choice>().All(c=>c.Key!=ExplorerRules.PoolId),"Locked pool absent from normal draft selector");
  versionPicker.SelectedItem=versionPicker.Items.Cast<Choice>().Single(c=>c.Key==ExplorerRules.PoolId);
  var reno=filteredCards.Single(c=>c.Id=="LOE_011");
  Check(!((Grid)CardLine(reno,0,false).Child).Children.OfType<Button>().Single().IsEnabled,"Locked card cannot be added");
  ApplyTheme(true,true);ApplyMode(false);ShowPage("deck");ShowDeckCard(reno);
  Check(cardInfo.Text.Contains("【暂不可用】")&&cardInfo.Text.Contains("尚未开放"),"Details distinguish the availability lock");
  Render(Path.Combine(folder,"explorers-locked.png"));
  draft=new DeckDocument{ClassId="WARLOCK",PoolId=ExplorerRules.PoolId};
  draft.Cards.Add("LOE_011",1);
  SyncDeckControls();RefreshDeck();
  Check(draft.PoolId==ExplorerRules.PoolId&&draft.Cards.ContainsKey("LOE_011")&&
   ((Choice)poolPicker.SelectedItem).Label.Contains("锁定"),"Existing Explorer draft remains readable without pruning");
  versionPicker.SelectedIndex=0;draft=original;SyncDeckControls();RefreshDeck();
 }

 void VerifyExplorerNetwork(){
  ApplyMode(false);
  var guest=new PanelWindow(true){ShowInTaskbar=false,Opacity=0};
  guest.Show();
  try{
   lanPort.Text=FreeLanPort().ToString();lanClass.SelectedItem=Classes.First(c=>c.Key=="WARLOCK");lanDeck.SelectedIndex=0;BeginLan(true);
   guest.lanClass.SelectedItem=Classes.First(c=>c.Key=="WARLOCK");guest.lanDeck.SelectedIndex=0;
   guest.lanAddress.Text="127.0.0.1:"+lanPort.Text;guest.BeginLan(false);
   PumpLan(()=>lanReady&&guest.lanReady,"Explorer peers connect");
   DoMatch("mulligan",selectedIds:new int[0]);
   PumpLan(()=>guest.match.Players[0].MulliganDone,"Host mulligan synchronized");
   guest.DoMatch("mulligan",selectedIds:new int[0]);
   PumpLan(()=>match.MulliganComplete&&!guest.lanWaiting,"Both mulligans finish");
   var g=lanAuthority.Game;
   Check(g.CardPoolId==ExplorerRules.PoolId,"Use the Explorer pool over TCP");
   g.Active=1;g.Players[1].Board.Clear();g.Players[1].Hand.Clear();g.Players[1].Mana=10;
   g.Summon(1,"LOE_077");
   int peddler=NaxxUiHand(g,"LOE_023",1);
   PublishLan();
   PumpLan(()=>guest.match.Players[1].Hand.Any(h=>h.Id==peddler),"Guest receives Peddler");
   guest.DoMatch("play",peddler,position:1);
   PumpLan(()=>!guest.lanWaiting&&guest.match.ObserveDecision(1)!=null,"Guest receives its own Discover choices");
   Check(LanProtocol.View(g,0,lanSession,lanRevision).OwnDecision==null&&g.ObserveDecision(0)==null,"Host-seat UI receives no guest options");
   for(int i=0;i<2;i++){
    var current=guest.match.ObserveDecision(1);
    var button=DecisionButtons(guest.livePanel,current.Options.Select(o=>o.Id).ToArray()).First();
    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    int expected=i+1;
    PumpLan(()=>!guest.lanWaiting&&guest.match.Players[1].Hand.Count==expected,"Choice committed once by host");
   }
   Check(!g.HasPendingDecision&&!guest.match.HasPendingDecision&&g.Players[1].Hand.Count==2,"Both peers resume after the second decision");
   Check(LanProtocol.View(g,0,lanSession,lanRevision).Players[1].Hand.Length==0,"Chosen card identities remain private");
  }finally{guest.LeaveLan();guest.Close();LeaveLan();}
 }
}
}
