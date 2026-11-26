using System;
using System.IO;
using System.Linq;
namespace Touchfish {
public partial class PanelWindow {
 public void PreviewAiScope(string folder){
  previewRun=true;Directory.CreateDirectory(folder);int before=checks;LeaveLan();lanModePicker.SelectedItem=lanModePicker.Items.Cast<Choice>().First(c=>c.Key=="ai");ApplyTheme(true,true);ApplyMode(false);localOpponentMode.SelectedItem=localOpponentMode.Items.Cast<Choice>().First(c=>c.Key=="freeze");RefreshAgentLobby();ShowPage("lan");Check(hostButton.Visibility==System.Windows.Visibility.Collapsed&&joinButton.Visibility==System.Windows.Visibility.Collapsed&&aiStartButton.Visibility==System.Windows.Visibility.Visible,"Human AI has its own action without network controls");Check(localOpponentMode.Items.Cast<Choice>().All(c=>c.Key!="test")&&lanNotice.Text.Contains("人机"),"Human AI selector excludes test and has correct notice");Render(Path.Combine(folder,"150-freeze-ai-lobby.png"));Check(lanAgentField.Visibility==System.Windows.Visibility.Visible&&lanNetworkFields.Visibility==System.Windows.Visibility.Collapsed,"AI opponent fixed to classic mage");ApplyMode(true);Render(Path.Combine(folder,"150-freeze-ai-lobby-minimal.png"));Check(localOpponentMode.ActualWidth>0&&Width==392,"AI selector visible in fixed minimal lobby");ApplyMode(false);lanDeck.SelectedIndex=0;lanClass.SelectedItem=Classes.First(c=>c.Key=="WARLOCK");lanName.Text="练习玩家";StartHumanAiMatch();Check(agentMatch&&match!=null&&!match.TestMode&&match.Players[1].MulliganDone&&!match.Players[0].MulliganDone,"AI normal opening and automatic private mulligan");DoMatch("mulligan",selectedIds:new int[0]);Check(match.MulliganComplete&&!awaitHandoff&&matchViewSeat==0,"Human confirmation never opens handoff or enemy hand");if(match.Active==0)DoMatch("end");Check(match.Active==1&&!CanMatchInput(),"Human input blocked during AI turn");int blockedTurn=match.Turn;DoMatch("end");Check(match.Turn==blockedTurn,"Stale human end callback cannot skip AI turn");match.ReceiveAnimationWait(match.AnimationSequence+1,2000);int locked=match.Log.Count;TickAgent();Check(match.Log.Count==locked,"AI respects shared animation lock");match.ReceiveAnimationWait(match.AnimationSequence,0);int turn=match.Turn;for(int n=0;n<50&&!match.Finished&&match.Active==1;n++)TickAgent();Check(match.Finished||match.Active==0&&match.Turn>turn,"AI acts and returns human control");Check(matchViewSeat==0&&!awaitHandoff,"Fixed human perspective after AI actions");Render(Path.Combine(folder,"151-freeze-ai-match-normal.png"));ApplyMode(true);Check(Width==392&&Height==300&&HeroStatusLabel(1,false)==match.Players[1].Health.ToString(),"Minimal AI keeps health-only enemy");Render(Path.Combine(folder,"152-freeze-ai-match-minimal.png"));
  match.Active=1;ShowPage("lan");int count=match.Log.Count;TickAgent();Check(match.Log.Count==count,"AI pauses away from match page");ShowPage("match");long clock=0;match.ClockNowMs=()=>clock;match.Summon(0,"VAN_EX1_560");clock=15001;TickTurnClock();Check(matchViewSeat==0&&!awaitHandoff&&match.Active==0,"Timeout preserves human view in AI match");
  OpenLan();Check(agentMatch&&currentPage=="match","Battle tab resumes active AI");LeaveLan();OpenLan();Check(match==null&&!agentMatch&&localAgent==null&&!localTest,"LAN entry clears AI session");OpenLocalTest();StartConfiguredMatch();Check(!agentMatch&&match.TestMode&&match.Players[0].MaxMana==10,"Original two-person test still ten mana");
  PreviewPaladinAgents(folder);
  File.WriteAllText(Path.Combine(folder,"ai-verification.txt"),"PASS: "+(checks-before)+" WPF checks; local AI session, Paladin opponent selection and tactics, fixed human viewpoint, normal/minimal layouts, timer and original test mode. No physical mouse or LAN claims.");
 }

 void PreviewPaladinAgents(string folder){
  foreach(string id in new[]{"secret-paladin","murloc-paladin"}){
   LeaveLan();
   lanModePicker.SelectedItem=lanModePicker.Items.Cast<Choice>().First(c=>c.Key=="ai");
   localOpponentMode.SelectedItem=localOpponentMode.Items.Cast<Choice>().Single(c=>c.Key==id);
   lanDeck.SelectedIndex=0;
   ApplyMode(false);
   Render(Path.Combine(folder,id+"-lobby.png"));

   var definition=ChosenAgent();
   var deck=definition.Deck(catalog);
   StartHumanAiMatch();
   Check(agentMatch&&localAgent is PaladinAgent&&!match.TestMode&&match.PlayerLabel(1)==definition.Name,
    id+" starts a standard local match under its own name");
   var ai=match.Players[1];
   var original=ai.Deck.Concat(ai.Hand.Select(card=>card.CardId)).ToLookup(cardId=>cardId);
   Check(ai.ClassId=="PALADIN"&&ai.MulliganDone&&!match.Players[0].MulliganDone
    &&original.Sum(group=>group.Count())==30
    &&deck.Cards.All(pair=>original[pair.Key].Count()==pair.Value),id+" loads its original 30 cards and mulligans");

   DoMatch("mulligan",selectedIds:new int[0]);
   if(match.Active==0)DoMatch("end");
   Check(matchViewSeat==0&&!CanMatchInput(),id+" keeps the human perspective during AI turns");
   ai.Hand.Clear();ai.Deck.Clear();ai.Board.Clear();ai.Mana=ai.MaxMana=6;
   if(id=="secret-paladin"){
    foreach(string secret in new[]{"FP1_020","VAN_EX1_130","VAN_EX1_379","VAN_EX1_136","AT_073","FP1_020"})
     ai.Deck.Add(secret);
    match.GiveNaxxCard(1,"AT_079");
    TickAgent();
    Check(ai.Board.Single().CardId=="AT_079"&&match.SecretCards(1).Length==5&&ai.Deck.SequenceEqual(new[]{"FP1_020"}),
     "Secret Paladin plays Challenger and arms five distinct secrets");
   }else{
    match.GiveNaxxCard(1,"AT_076");
    TickAgent();
    TickAgent();
    Check(ai.Board.Count==3&&ai.Board[0].CardId=="AT_076"&&ai.Board[2].Race=="MURLOC"&&ai.Mana==0,
     "Murloc Paladin plays Knight and activates Inspire immediately");
   }
   TickAgent();
   Check(match.Active==0&&matchViewSeat==0,id+" returns the turn to the human");
   Render(Path.Combine(folder,id+"-match.png"));
  }
  LeaveLan();
 }
}
}
