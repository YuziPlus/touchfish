using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace Touchfish {
public partial class PanelWindow {
 public void PreviewTournament(string folder){
  Directory.CreateDirectory(folder);previewRun=true;
  LeaveLan();ApplyTheme(true,true);
  if(VerificationRun.Visual){
   var original=draft;
   draft=new DeckDocument{ClassId="DRUID",PoolId=TournamentClassRules.PoolId};
   SyncDeckControls();RefreshDeck();showAllCards.IsChecked=true;
   versionPicker.SelectedItem=versionPicker.Items.Cast<Choice>().First(c=>c.Key==TournamentRules.SetId);RefreshCollection();
   Check(filteredCards.Count==50&&filteredCards.All(c=>matchRules.Supports(c)),"All 50 neutral cards appear as playable");
   versionPicker.SelectedItem=versionPicker.Items.Cast<Choice>().First(c=>c.Key==TournamentClassRules.SetId);
   Check(filteredCards.Count==82&&filteredCards.All(c=>matchRules.Supports(c)),"All 82 class cards appear as playable");
   searchBox.Text="AT_045";var aviana=filteredCards.Single();
   Check(aviana.Id=="AT_045"&&aviana.DbfId==2796,"Deck builder finds the historical Aviana record");
   ApplyMode(false);ShowPage("deck");ShowDeckCard(aviana);Render(Path.Combine(folder,"tournament-aviana.png"));
   searchBox.Text="";versionPicker.SelectedIndex=0;draft=original;SyncDeckControls();
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
    Check(g.CardPoolId==TournamentClassRules.PoolId&&guest.match.CardPoolId==g.CardPoolId,"Both peers use the full tournament pool");
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
