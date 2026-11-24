using System;using System.Linq;using System.IO;
namespace Touchfish {public partial class PanelWindow {
 public void PreviewMiracleAiScope(string folder){
  previewRun=true;Directory.CreateDirectory(folder);int before=checks;LeaveLan();ApplyTheme(true,true);ApplyMode(false);
  lanModePicker.SelectedItem=lanModePicker.Items.Cast<Choice>().First(c=>c.Key=="ai");localOpponentMode.SelectedItem=localOpponentMode.Items.Cast<Choice>().First(c=>c.Key=="miracle");
  lanDeck.SelectedIndex=0;lanClass.SelectedItem=Classes.First(c=>c.Key=="MAGE");ShowPage("lan");Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{}));Check(localOpponentMode.SelectionBoxItem.ToString()=="经典奇迹贼 AI","Miracle selection label updated in rendered lobby");Render(Path.Combine(folder,"miracle-lobby-normal.png"));
  ApplyMode(true);Render(Path.Combine(folder,"miracle-lobby-minimal.png"));Check(Width==392&&Height==300&&localOpponentMode.ActualWidth>100,"Miracle selector fits minimal");
  ApplyMode(false);StartHumanAiMatch();Check(agentMatch&&localAgent is MiracleRogueAgent&&match.Players[1].ClassId=="ROGUE"&&!match.TestMode&&match.Players[1].MulliganDone,"Miracle normal match, independent strategy, automatic mulligan");
  var aiCards=match.Players[1].Deck.Concat(match.Players[1].Hand.Select(hand=>hand.CardId)).Select(match.Card).ToArray();
  Check(aiCards.Count(card=>card.BaseId=="EX1_116"&&card.Cost==4)==1&&aiCards.Count(card=>card.BaseId=="EX1_095"&&card.Cost==5)==2,"Human AI entry preserves Classic Leeroy and Auctioneer");
  DoMatch("mulligan",selectedIds:new int[0]);if(match.Active==0)DoMatch("end");Check(match.Active==1&&!CanMatchInput(),"Human cannot manipulate Miracle turn");
  for(int n=0;n<50&&!match.Finished&&match.Active==1;n++)TickAgent();
  Check(match.Finished||match.Active==0,"Miracle completes own turn");Check(matchViewSeat==0&&!awaitHandoff,"Miracle keeps human perspective");Render(Path.Combine(folder,"miracle-match-normal.png"));
  ApplyMode(true);Render(Path.Combine(folder,"miracle-match-minimal.png"));Check(Width==392&&Height==300&&HeroStatusLabel(1,false)==match.Players[1].Health.ToString(),"Miracle minimal enemy remains health only");
  LeaveLan();OpenLocalTest();StartConfiguredMatch();Check(!agentMatch&&match.TestMode,"Test remains independent of selected Miracle");
  File.WriteAllText(Path.Combine(folder,"miracle-ai-verification.txt"),"PASS: "+(checks-before)+" WPF checks; mode selector, launch, mulligan, actual AI turn, normal/minimal, test isolation. No physical mouse claims.");
 }
}}
