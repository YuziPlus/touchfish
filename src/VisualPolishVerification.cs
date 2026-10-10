using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Touchfish {
public partial class PanelWindow {
 TextBlock StatLabel(int seat,int id,int column){var border=(Border)visualTargets[VisualKey(seat,id)];return (TextBlock)((Grid)border.Child).Children[column];}
 public void PreviewVisualPolishScope(string folder){
  previewRun=true;Directory.CreateDirectory(folder);int before=checks;LeaveLan();match=MatchFixture();matchViewSeat=0;awaitHandoff=false;ClearPending();ApplyTheme(true,true);ApplyMode(false);SetFullEffects(true);
  var buffed=match.Summon(0,"VAN_CS2_182");match.Buff(new MatchTarget(0,buffed.Id),2,0);match.Damage(new MatchTarget(0,buffed.Id),2);var equality=match.Summon(0,"VAN_CS2_182");match.Damage(new MatchTarget(0,equality.Id),2);match.SetHealth(new MatchTarget(0,equality.Id),1);ShowPage("match");RenderMatch();
  Check(((SolidColorBrush)StatLabel(0,buffed.Id,0).Foreground).Color==(Color)ColorConverter.ConvertFromString("#85BC91"),"Rendered buffed attack is green with extras");
  Check(((SolidColorBrush)StatLabel(0,buffed.Id,2).Foreground).Color==(Color)ColorConverter.ConvertFromString("#D78B86"),"Rendered damaged health is red with extras");
  Check(equality.Health==1&&equality.MaxHealth==1&&StatLabel(0,equality.Id,2).Foreground==Ink,"Equality max-health reset is not marked damaged");Render(Path.Combine(folder,"114-stat-colors-codex.png"));
  match.Buff(new MatchTarget(0,equality.Id),0,2);match.Damage(new MatchTarget(0,equality.Id),1);RenderMatch();Check(StatLabel(0,equality.Id,2).Foreground!=Ink,"Damage after equality and health buff is red");match.Heal(new MatchTarget(0,buffed.Id),100);match.Silence(new MatchTarget(0,buffed.Id));RenderMatch();Check(StatLabel(0,buffed.Id,0).Foreground==Ink&&StatLabel(0,buffed.Id,2).Foreground==Ink,"Healing and silence reset stat colors");
  match.Buff(new MatchTarget(0,buffed.Id),1,0);match.Damage(new MatchTarget(0,buffed.Id),1);SetFullEffects(false);RenderMatch();Check(StatLabel(0,buffed.Id,0).Foreground==Ink&&StatLabel(0,buffed.Id,2).Foreground==Ink,"Extras off leaves stats neutral");SetFullEffects(true);ApplyMode(true);RenderMatch();Check(StatLabel(0,buffed.Id,0).Foreground==Ink&&StatLabel(0,buffed.Id,2).Foreground==Ink,"Minimal never colors stats");Render(Path.Combine(folder,"115-stat-colors-minimal.png"));ApplyMode(false);ApplyTheme(false,false);RenderMatch();Render(Path.Combine(folder,"116-stat-colors-wps.png"));Check(Width==548&&Height==474,"Color changes preserve window size");
  var copied=LanProtocol.ReadView(catalog,LanProtocol.Copy(LanProtocol.View(match,1,"stat-colors",0)));Check(copied.Players[0].Board.Single(u=>u.Id==equality.Id).MaxHealth==3&&copied.Players[0].Board.Single(u=>u.Id==equality.Id).DamageTaken==1,"Public snapshot preserves health cap separately from damage");
  OpenReleaseNotes();var body=(StackPanel)((ScrollViewer)releasePanel.Children[1]).Content;var groups=body.Children.OfType<Expander>().ToArray();Check(groups.Select(g=>(string)g.Tag).SequenceEqual(new[]{"v0.4.0","v0.3.0","v0.2.0","v0.1.0"})&&groups.All(g=>!g.IsExpanded),"Version families collapse in descending order");
  var versions=groups.SelectMany(g=>((StackPanel)g.Content).Children.OfType<Expander>()).ToArray();Check(versions.Any(v=>(string)v.Tag=="v0.4.4")&&versions.Any(v=>(string)v.Tag=="v0.1.1")&&versions.Length==versions.Select(v=>(string)v.Tag).Distinct().Count(),"Version history remains complete and distinct inside groups");Render(Path.Combine(folder,"117-release-families-wps.png"));ApplyTheme(true,true);ApplyMode(true);RenderExpandedRelease(folder);Render(Path.Combine(folder,"118-release-families-minimal.png"));Check(Width==392&&Height==300,"Grouped release notes fit minimal window");
  File.WriteAllText(Path.Combine(folder,"visual-polish-verification.txt"),"PASS: "+(checks-before)+" focused stat-color and grouped-history assertions; actual WPF previews, no user data or clipboard writes.");
 }
}
}
