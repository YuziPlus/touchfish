using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
public sealed class DecisionOption {
 public readonly string Id,Title,Description;
 public readonly int? Cost;
 internal DecisionOption(string id,string title,string description,int? cost){Id=id;Title=title;Description=description;Cost=cost;}
}

public sealed class DecisionObservation {
 public readonly int Id,Seat;
 public readonly string Prompt;
 public readonly DecisionOption[] Options;
 internal DecisionObservation(int id,int seat,string prompt,DecisionOption[] options){Id=id;Seat=seat;Prompt=prompt;Options=options;}
}

// Programs contain only immutable instructions. A frame can resume them by index.
internal enum DecisionEffect { GiveCard, DamageEnemyHero }

internal sealed class DecisionCandidate {
 internal readonly string Id,CardId,Title,Description;
 internal readonly DecisionEffect Effect;
 internal readonly int Weight,Amount;
 internal DecisionCandidate(string id,string cardId,int weight=1){
  Id=id;CardId=cardId;Weight=weight;Effect=DecisionEffect.GiveCard;
 }
 internal DecisionCandidate(string id,string title,string description,int damage){
  Id=id;Title=title;Description=description;Amount=damage;Weight=1;Effect=DecisionEffect.DamageEnemyHero;
 }
}

internal sealed class DecisionStep {
 internal readonly string Prompt;
 readonly int sampleSize;
 readonly DecisionCandidate[] candidates;

 internal DecisionStep(string prompt,int sampleSize,params DecisionCandidate[] candidates){
  if(sampleSize<1||candidates.Length==0||candidates.Any(c=>c==null||String.IsNullOrEmpty(c.Id)||c.Weight<=0)||
     candidates.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=candidates.Length)
   throw new ArgumentException("决策选项或权重无效。");
  candidates.Sum(c=>c.Weight);
  Prompt=prompt;
  this.sampleSize=sampleSize;
  this.candidates=(DecisionCandidate[])candidates.Clone();
 }

 internal DecisionCandidate[] Generate(MatchEngine game){
  var eligible=candidates.Where(c=>c.CardId==null||game.Catalog.Cards.ContainsKey(c.CardId)&&game.Rules.Supports(game.Card(c.CardId))).ToList();
  if(eligible.Count<=sampleSize)return eligible.ToArray();
  var chosen=new DecisionCandidate[sampleSize];
  int total=eligible.Sum(c=>c.Weight);
  for(int i=0;i<chosen.Length;i++){
   int roll=game.RandomIndex(total);
   int index=0;
   while(roll>=eligible[index].Weight)roll-=eligible[index++].Weight;
   var option=eligible[index];
   chosen[i]=option;
   total-=option.Weight;
   eligible.RemoveAt(index);
  }
  return chosen;
 }
}

internal sealed class DecisionProgram {
 readonly DecisionStep[] steps;
 internal DecisionProgram(params DecisionStep[] steps){
  if(steps.Length==0||steps.Any(step=>step==null))throw new ArgumentException("决策程序不能为空。");
  this.steps=(DecisionStep[])steps.Clone();
 }
 internal int Length{get{return steps.Length;}}
 internal DecisionStep Step(int index){return steps[index];}
}

internal sealed class PendingDecision {
 internal readonly int Id,Seat;
 internal readonly string Prompt;
 internal readonly DecisionCandidate[] Candidates;
 internal PendingDecision(int id,int seat,string prompt,DecisionCandidate[] candidates){Id=id;Seat=seat;Prompt=prompt;Candidates=candidates;}
 internal PendingDecision Copy(){return new PendingDecision(Id,Seat,Prompt,(DecisionCandidate[])Candidates.Clone());}
}

internal sealed class ResolutionFrame {
 internal int Seat,NextStep;
 internal string CardId;
 internal BattleUnit Summoned;
 internal BattleUnit[] CardListeners;
 internal ResolutionFrame Copy(Func<BattleUnit,BattleUnit> copyUnit){
  return new ResolutionFrame{Seat=Seat,NextStep=NextStep,CardId=CardId,
   Summoned=Summoned==null?null:copyUnit(Summoned),CardListeners=CardListeners.Select(copyUnit).ToArray()};
 }
}

public sealed partial class MatchEngine {
 PendingDecision pendingDecision;
 ResolutionFrame decisionFrame;
 public bool HasPendingDecision{get{return pendingDecision!=null;}}

 public DecisionObservation ObserveDecision(int seat){
  if(seat<0||seat>1)throw new ArgumentOutOfRangeException("seat");
  var pending=pendingDecision;
  if(pending==null||pending.Seat!=seat)return null;
  var options=new DecisionOption[pending.Candidates.Length];
  for(int i=0;i<options.Length;i++){
   var candidate=pending.Candidates[i];
   var card=candidate.CardId==null?null:Card(candidate.CardId);
   options[i]=card==null?new DecisionOption(candidate.Id,candidate.Title,candidate.Description,null)
    :new DecisionOption(candidate.Id,card.Name,card.Text,card.Cost);
  }
  return new DecisionObservation(pending.Id,seat,pending.Prompt,options);
 }

 public ActionResult SubmitDecision(int seat,int decisionId,string optionId){
  var pending=pendingDecision;
  if(Finished||pending==null||pending.Seat!=seat||pending.Id!=decisionId)
   return new ActionResult{Message="该决策已失效或不属于你。"};
  var selected=Array.Find(pending.Candidates,option=>option.Id==optionId);
  if(selected==null)return new ActionResult{Message="该选项无效。"};
  return Act(()=>{
   pendingDecision=null;
   switch(selected.Effect){
    case DecisionEffect.GiveCard:GiveNaxxCard(seat,selected.CardId);break;
    case DecisionEffect.DamageEnemyHero:Damage(new MatchTarget(1-seat),selected.Amount);Cleanup();break;
   }
   ContinueDecisionProgram();
  },allowExpired:true,decisionAction:true);
 }

 void StartDecisionProgram(int seat,CardRecord card,BattleUnit minion,BattleUnit[] listeners){
  decisionFrame=new ResolutionFrame{Seat=seat,CardId=card.Id,Summoned=minion,CardListeners=listeners};
  ContinueDecisionProgram();
 }

 void ContinueDecisionProgram(){
  var frame=decisionFrame;
  if(!Finished){
   var program=Rules.DynamicProgram(Card(frame.CardId));
   while(frame.NextStep<program.Length){
    var step=program.Step(frame.NextStep++);
    var options=step.Generate(this);
    if(options.Length==0)continue;
    pendingDecision=new PendingDecision(NewEffectInstanceId(),frame.Seat,step.Prompt,options);
    return;
   }
  }
  decisionFrame=null;
  if(Finished){if(summonTriggers!=null)summonTriggers.Clear();return;}
  CompletePlayedCard(frame.Seat,Card(frame.CardId),frame.Summoned,frame.CardListeners,stopOnFinish:true);
 }
}
}
