using System;
using System.Linq;

namespace Touchfish {
public sealed class DeterminizationBuilder {
 readonly MatchEngine baseline;
 public readonly PlayerInformation Information;

 DeterminizationBuilder(MatchEngine baseline,PlayerInformation information){this.baseline=baseline;Information=information;}

 public static DeterminizationBuilder Capture(MatchEngine source,int seat,string[] knownOwnTop=null){
  return CaptureCore(source,seat,knownOwnTop,false);
 }

 public static DeterminizationBuilder CaptureOpening(MatchEngine source,int seat){
  return CaptureCore(source,seat,null,true);
 }

 static DeterminizationBuilder CaptureCore(MatchEngine source,int seat,string[] knownOwnTop,bool opening){
  if(source==null)throw new ArgumentNullException("source");
  if(seat<0||seat>1)throw new ArgumentOutOfRangeException("seat");
  var preview=knownOwnTop==null?new string[0]:(string[])knownOwnTop.Clone();
  if(preview.Length>3||preview.Length>source.Players[seat].Deck.Count||preview.Any(String.IsNullOrEmpty))
   throw new ArgumentException("已观察牌库顶信息无效。","knownOwnTop");
  var baseline=source.CopyForBelief(seat,opening);
  var info=new PlayerInformation(baseline.Catalog,baseline.Rules,seat,baseline.CardPoolId,source.Players.Select(p=>p.ClassId).ToArray(),
   source.Players.Select(p=>p.HandCount).ToArray(),source.Players.Select(p=>p.Deck.Count).ToArray(),source.Players.Select(p=>p.SecretCount).ToArray(),
   baseline.Players[seat].Hand.Select(h=>h.CopyForSimulation()).ToArray(),baseline.SecretCards(seat),source.ObserveHistory(seat),preview,baseline.Card);
  return new DeterminizationBuilder(baseline,info);
 }

 public FastSimulation Build(SampleResult sample){
  if(sample==null||!sample.Success||!ReferenceEquals(sample.Information,Information))
   throw new InvalidOperationException("采样结果与该观察快照不匹配。");
  var game=baseline.CopyForSimulation(sample.SimulationSeed);
  game.InstallBeliefSample(Information.Seat,sample);
  game.RestoreBeliefReturns(Information.Events);
  return new FastSimulation(game);
 }
}

public sealed partial class MatchEngine {
 internal MatchEngine CopyForBelief(int seat,bool opening=false){
  ValidateSimulationBoundary();
  if(Mode!=GameModes.Classic&&Mode!=GameModes.LocalTest)throw new InvalidOperationException("当前信念模型只支持标准构筑规则。");
  if(opening){
   if(Players.Any(player=>player.MulliganDone)||Turn!=1||Finished)
    throw new InvalidOperationException("起手采样要求双方均未确认换牌。");
  }else if(!MulliganComplete)
   throw new InvalidOperationException("隐藏世界要求已完成起手换牌。");
  if(HasPendingDecision&&ObserveDecision(seat)==null)
   throw new InvalidOperationException("当前席位无权读取待决策候选。");
  return new MatchEngine(this,0,seat);
 }

 internal void RestoreBeliefReturns(PublicHistoryEvent[] events){
  foreach(var entry in events){
   if(entry.Kind==HistoryKind.DelayReturn){
    var card=new HandCard{Id=NewEffectInstanceId(),CardId=entry.CardId,Generated=Card(entry.CardId).CardClass!=Players[entry.Seat].ClassId};
    comboReturns.Add(new ComboReturn{Seat=entry.Seat,Due=entry.Adjustment,Card=card});
   }else if(entry.Kind==HistoryKind.ReturnDue){
    var item=comboReturns.FirstOrDefault(r=>r.Seat==entry.Seat&&r.Card.CardId==entry.CardId);
    if(item!=null)comboReturns.Remove(item);
   }
  }
 }
 internal void InstallBeliefSample(int owner,SampleResult sample){
  int enemy=1-owner;
  for(int seat=0;seat<2;seat++){
   Players[seat].Deck.AddRange(sample.Decks[seat]);
   Players[seat].ReportedHandCount=-1;
  }
  foreach(var original in sample.EnemyCards){
   var card=original.CopyForSimulation();
   card.Id=NewEffectInstanceId();
   if(Card(card.CardId).CardClass!="NEUTRAL"&&Card(card.CardId).CardClass!=Players[enemy].ClassId)card.Generated=true;
   Players[enemy].Hand.Add(card);
  }
  RestoreOwnSecrets(enemy,sample.Secrets[enemy]);
 }
}
}
