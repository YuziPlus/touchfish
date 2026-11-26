using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
namespace Touchfish {
public partial class PanelWindow {
 string DrawVisualText(System.Windows.DependencyObject root){var text=root as System.Windows.Controls.TextBlock;string value=text==null?"":text.Text;for(int n=0;n<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);n++)value+="|"+DrawVisualText(System.Windows.Media.VisualTreeHelper.GetChild(root,n));return value;}
 public void PreviewDrawVisualScope(string folder){
  previewRun=true;Directory.CreateDirectory(folder);LeaveLan();localTest=true;ApplyTheme(true,true);ApplyMode(false);SetEffectsLevel(EffectLevel.Low);match=MatchFixture();matchViewSeat=0;awaitHandoff=false;ShowPage("match");RenderMatch();UpdateLayout();var renderer=(OfficeMatchVisualEffects)matchEffects;renderer.Clear();
  var player=match.Players[0];player.Hand.Clear();for(int n=0;n<10;n++)player.Hand.Add(new HandCard{Id=80000+n,CardId="VAN_CS2_182"});player.Deck.Clear();player.Deck.Add("VAN_CS2_029");int before=renderer.OverdrawCount;match.Draw(0,1);RenderMatch();UpdateLayout();Check(renderer.OverdrawCount==before+1&&matchEffectsCanvas.Children.Count>0,"Full hand emits a centered overdraw card animation");Check(player.Hand.Count==10&&player.Deck.Count==0,"Overdraw leaves hand full and consumes exactly one deck card");var showStarted=DateTime.UtcNow;PumpLan(()=>DateTime.UtcNow-showStarted>=TimeSpan.FromMilliseconds(220),"Overdraw reaches visible animation frame");Render(Path.Combine(folder,"overdraw-normal.png"));
  before=renderer.FatigueCount;match.Draw(0,1);RenderMatch();UpdateLayout();Check(renderer.FatigueCount==before+1,"Empty deck draw emits fatigue animation");Check(((System.Windows.FrameworkElement)matchEffectsCanvas.Children[matchEffectsCanvas.Children.Count-1]).Opacity==0,"Queued fatigue stays hidden until preceding overdraw finishes");Check(enemyHandStatus!=null&&enemyHandStatus.Text.Contains("手牌 ")&&enemyHandStatus.Text.Contains("牌库 "),"Opponent resource line shows hand and deck counts");var fatigueStarted=DateTime.UtcNow;PumpLan(()=>DateTime.UtcNow-fatigueStarted>=TimeSpan.FromMilliseconds(1600),"Overdraw animation finishes before queued fatigue");Render(Path.Combine(folder,"fatigue-normal.png"));
  renderer.Clear();drawVisualNextUtc=DateTime.MinValue;var enemy=match.Players[1];enemy.Hand.Clear();for(int n=0;n<10;n++)enemy.Hand.Add(new HandCard{Id=90000+n,CardId="VAN_CS2_182"});enemy.Deck.Clear();enemy.Deck.Add("VAN_CS2_029");match.Draw(1,1);RenderMatch();UpdateLayout();string shown=DrawVisualText(matchEffectsCanvas);Check(shown.Contains(match.Card("VAN_CS2_029").Name)&&shown.Contains("费用")&&!shown.Contains("卡牌背面"),"Both viewers see the burned card face");drawVisualNextUtc=DateTime.UtcNow.AddMinutes(1);match=MatchFixture();RenderMatch();Check(drawVisualNextUtc<DateTime.UtcNow.AddSeconds(1),"New game clears pending draw animation queue");
  SetEffectsLevel(EffectLevel.Off);renderer.Clear();drawVisualNextUtc=DateTime.MinValue;match.Players[0].Hand.Clear();for(int n=0;n<10;n++)match.Players[0].Hand.Add(new HandCard{Id=91000+n,CardId="VAN_CS2_182"});match.Players[0].Deck.Clear();match.Players[0].Deck.Add("VAN_CS2_029");match.Draw(0,1);RenderMatch();UpdateLayout();shown=DrawVisualText(matchEffectsCanvas);Check(shown.Contains("火球术已破碎")&&!shown.Contains("费用")&&matchEffectsCanvas.Children.Count==1,"Effects off displays only a one-line burn notice");var notice=(System.Windows.Controls.TextBlock)matchEffectsCanvas.Children[0];Check(!notice.HasAnimatedProperties&&notice.RenderTransform==System.Windows.Media.Transform.Identity,"Effects off notice has no movement or opacity animation");Render(Path.Combine(folder,"overdraw-effects-off.png"));SetEffectsLevel(EffectLevel.High);drawVisualNextUtc=DateTime.MinValue;match.Players[0].Deck.Add("VAN_CS2_029");match.Draw(0,1);RenderMatch();shown=DrawVisualText(matchEffectsCanvas);Check(shown.Contains("费用")&&!shown.Contains("已破碎"),"High effects retains the full burned card");
  ApplyMode(true);RenderMatch();before=renderer.FatigueCount;match.Draw(0,1);RenderMatch();Check(renderer.FatigueCount==before&&matchEffectsCanvas.Visibility==System.Windows.Visibility.Collapsed,"Minimal mode remains visually quiet");ApplyMode(false);
  PreviewJoustVisuals(folder);
  localTest=false;File.WriteAllText(Path.Combine(folder,"draw-visual-verification.txt"),"PASS: overdraw, fatigue, two-card joust animation with winner emphasis and return, opponent deck count and minimal-mode suppression; rendered real WPF previews.");
 }

 void PreviewJoustVisuals(string folder){
  SetEffectsLevel(EffectLevel.Off);
  match=MatchFixture();
  var own=new[]{"VAN_CS2_029","AT_125"};
  var enemy=new[]{"VAN_CS2_023","AT_082"};
  match.Players[0].Deck.Clear();match.Players[0].Deck.AddRange(own);
  match.Players[1].Deck.Clear();match.Players[1].Deck.AddRange(enemy);
  Check(match.Play(0,NaxxUiHand(match,"AT_133")).Success,"Jouster played normally");
  RenderMatch();
  var winner=JoustVisualCard("我方");var loser=JoustVisualCard("对手");
  var winnerScale=(ScaleTransform)((TransformGroup)winner.RenderTransform).Children[0];
  var loserScale=(ScaleTransform)((TransformGroup)loser.RenderTransform).Children[0];
  PumpLan(()=>winner.Opacity>=.995,"Both cards arrive from their deck counters");
  var hold=System.Diagnostics.Stopwatch.StartNew();
  string shown=DrawVisualText(matchEffectsCanvas);
  Check(shown.Contains(match.Card("AT_125").Name)&&shown.Contains(match.Card("AT_082").Name)
   &&shown.Contains("费用 "+match.Card("AT_125").Cost)&&shown.Contains("费用 "+match.Card("AT_082").Cost),
   "Comparison shows both names and costs even with extra effects off");
  Check(winnerScale.ScaleX==1&&loserScale.ScaleX==1,"Neither card scales on arrival");
  Render(Path.Combine(folder,"joust-compare.png"));
  PumpLan(()=>winnerScale.ScaleX>1.10,"Higher-cost card enlarges after the hold");
  Check(hold.ElapsedMilliseconds>=900&&loserScale.ScaleX==1,"About one second of comparison, then only the winner pulses");
  Render(Path.Combine(folder,"joust-winner.png"));
  RenderMatch();
  Check(matchEffectsCanvas.Children.OfType<Border>().Count(card=>(card.Tag as string??"").StartsWith("joust-"))==2,
   "Unchanged snapshot does not queue another animation");
  PumpLan(()=>!matchEffectsCanvas.Children.Contains(winner)&&!matchEffectsCanvas.Children.Contains(loser),
   "Both animated cards travel back and are removed");
  Check(match.Players[0].Deck.SequenceEqual(own)&&match.Players[1].Deck.SequenceEqual(enemy),
   "The full animation leaves both deck orders intact");
  ApplyMode(true);
  TournamentRules.Joust(match,0);
  RenderMatch();
  Check(matchEffectsCanvas.Children.Count==0,"Minimal mode suppresses the overlay");
  ApplyMode(false);
 }

 Border JoustVisualCard(string side){
  return matchEffectsCanvas.Children.OfType<Border>().Single(card=>Equals(card.Tag,"joust-"+side));
 }
}
}
