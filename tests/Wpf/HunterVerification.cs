using System;
using System.IO;
using System.Linq;
namespace Touchfish {
public partial class PanelWindow {
 public void PreviewHunterScope(string folder){previewRun=true;Directory.CreateDirectory(folder);int before=checks;VerifyHunterTcp(folder);File.WriteAllText(Path.Combine(folder,"hunter-verification.txt"),"PASS: "+(checks-before)+" checks; actual loopback TCP, private Tracking UI and summon triggers.");}
 void VerifyHunterTcp(string folder){if(!VerificationRun.Network)return;LeaveLan();ApplyTheme(true,true);ApplyMode(false);SetFullEffects(true);var guest=new PanelWindow(true){ShowInTaskbar=false,Opacity=0};guest.Show();guest.ApplyTheme(true,true);guest.ApplyMode(false);try{
  lanPort.Text=FreeLanPort().ToString();lanDeck.SelectedIndex=0;lanClass.SelectedItem=Classes.First(c=>c.Key=="MAGE");BeginLan(true);guest.lanDeck.SelectedIndex=0;guest.lanClass.SelectedItem=Classes.First(c=>c.Key=="HUNTER");guest.lanAddress.Text="127.0.0.1:"+lanPort.Text;guest.BeginLan(false);PumpLan(()=>lanReady&&guest.lanReady,"Hunter peers connect");DoMatch("mulligan",selectedIds:new int[0]);PumpLan(()=>guest.match.Players[0].MulliganDone,"Hunter host mulligan");guest.DoMatch("mulligan",selectedIds:new int[0]);PumpLan(()=>match.MulliganComplete&&!guest.lanWaiting,"Hunter mulligan completion");DoMatch("end");PumpLan(()=>guest.match.Active==1,"Hunter guest turn");var g=lanAuthority.Game;foreach(var p in g.Players){p.Hand.Clear();p.Board.Clear();p.Mana=p.MaxMana=10;}g.Players[1].Deck.Clear();g.Players[1].Deck.AddRange(new[]{"VAN_CS2_182","VAN_CS2_179","VAN_CS2_231","VAN_NEW1_018"});int track=Inject(g,"DS1_184",1);PublishLan();PumpLan(()=>guest.match.Players[1].Hand.Any(h=>h.Id==track),"Tracking guest hand update");guest.BeginCard(track);Check(guest.ChoicePending()&&guest.match.Rules.Choices(guest.match.Card("VAN_DS1_184"),guest.match,1).Length==3,"Guest sees three private choices");Check(match.PrivateDeckPreview[1].Length==0&&LanProtocol.View(g,0,"hunter",1).Players[1].DeckPreview==null,"Opponent has no Tracking candidates");guest.Opacity=1;guest.Render(Path.Combine(folder,"137-hunter-tracking-codex.png"));guest.Opacity=0;guest.SelectChoice("track:1");PumpLan(()=>!guest.lanWaiting&&guest.match.Players[1].Hand.Any(h=>h.CardId=="VAN_CS2_179"),"Guest Tracking choice reaches authority");Check(g.Players[1].Deck.Single()=="VAN_NEW1_018"&&g.GraveyardFor(1,GraveyardReason.Discarded).Length==2,"Tracking authoritative discard and remaining order");Check(!g.Log.Any(x=>x.Contains(g.Card("VAN_CS2_179").Name))&&LanProtocol.View(g,0,"hunter",1).Players[1].Hand.Length==0,"Selected private card is absent from public log and opponent hand");
  var buzz=g.Summon(1,"VAN_CS2_237");var knife=g.Summon(1,"VAN_NEW1_019");g.Summon(0,"VAN_CS2_182");int unleash=Inject(g,"EX1_538",1);PublishLan();PumpLan(()=>guest.match.Players[1].Hand.Any(h=>h.Id==unleash),"Guest Unleash received");guest.BeginCard(unleash);PumpLan(()=>!guest.lanWaiting&&g.Players[1].Board.Any(u=>u.CardId=="GAME_HOUND"),"Hound summon reaches authority");Check(guest.match.Players[1].Hand.Any(h=>h.CardId=="VAN_NEW1_018")&&LanProtocol.View(g,0,"hunter",1).Players[1].Hand.Length==0,"Buzzard draw visible only to Hunter");PumpLan(()=>match.Players[1].Board.Any(u=>u.CardId=="GAME_HOUND"),"Hound public board synchronized");Render(Path.Combine(folder,"138-hunter-public-codex.png"));
  VerifyHunterBeastTargets(guest);
 }finally{guest.LeaveLan();guest.Close();LeaveLan();}}
 void VerifyHunterBeastTargets(PanelWindow guest){
  var g=lanAuthority.Game;
  for(int seat=0;seat<2;seat++)foreach(string key in new[]{"DS1_070","EX1_549"}){
   foreach(var p in g.Players){p.Hand.Clear();p.Board.Clear();p.Mana=p.MaxMana=10;p.ClassId="HUNTER";}
   g.Active=seat;var beast=g.Summon(seat,"VAN_DS1_175");var nonBeast=g.Summon(seat,"VAN_CS2_182");var enemy=g.Summon(1-seat,"VAN_DS1_175");int hand=Inject(g,key,seat);PublishLan();
   PumpLan(()=>guest.match.Active==seat&&guest.match.Players[seat].Board.Any(u=>u.Id==beast.Id)&&(seat==0||guest.match.Players[seat].Hand.Any(h=>h.Id==hand)),"Hunter Beast target fixture synchronizes");
   var actor=seat==0?this:guest;int mana=g.Players[seat].Mana;
   foreach(var target in new[]{new MatchTarget(seat,nonBeast.Id),new MatchTarget(1-seat,enemy.Id),new MatchTarget(seat)}){
    actor.BeginCard(hand);if(key=="DS1_070")actor.ClickEmptySlot(seat,2);actor.ClickMatchTarget(target);
    PumpLan(()=>!actor.lanWaiting,"Invalid Beast target receives authoritative reply");
    Check(g.Players[seat].Mana==mana&&g.Players[seat].Hand.Any(h=>h.Id==hand)&&nonBeast.BuffAttack==0&&enemy.BuffAttack==0&&g.Players[seat].Board.Count==2,"Hunter invalid target preserves hand, mana and board for "+key+" seat "+seat);
   }
   actor.BeginCard(hand);if(key=="DS1_070")actor.ClickEmptySlot(seat,2);actor.ClickMatchTarget(new MatchTarget(seat,beast.Id));
   PumpLan(()=>!actor.lanWaiting&&!g.Players[seat].Hand.Any(h=>h.Id==hand)&&guest.match.Unit(new MatchTarget(seat,beast.Id)).BuffAttack+guest.match.Unit(new MatchTarget(seat,beast.Id)).TempAttack==2,"Legal friendly Beast resolves and synchronizes");
   Check(g.AttackValue(beast)==3&&(key=="DS1_070"?beast.Taunt&&beast.MaxHealth==3:beast.ImmuneUntilTurn==g.Turn),"Hunter valid Beast receives intended effect for "+key+" seat "+seat);
  }
 }
}
}
