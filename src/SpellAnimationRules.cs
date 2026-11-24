using System;
namespace Touchfish {
public sealed partial class MatchEngine {
 public const int PyroblastImpactMs=1000,PyroblastAnimationMs=1600;
 DateTime spellImpactAt=DateTime.MaxValue;Action pendingSpellImpact;
 public bool SpellImpactPending{get{return pendingSpellImpact!=null;}}
 bool DeferPyroblast(EffectContext context,Action finished){
  if(context.Card.Type!="SPELL"||MatchRules.EffectKey(context.Card)!="EX1_279"||!HighAnimationsEnabled)return false;
  EmitPyroblastVisual("pyroblast",context);StartAnimationWait(PyroblastAnimationMs);spellImpactAt=AnimationNowUtc().AddMilliseconds(PyroblastImpactMs);
  pendingSpellImpact=()=>{EmitPyroblastVisual("pyroblast-impact",context);ApplyCardEffect(context);finished();};return true;
 }
 void EmitPyroblastVisual(string kind,EffectContext context){EmitVisual(kind,context.Seat,context.Target.UnitId);VisualEvents[VisualEvents.Count-1].TargetSeat=context.Target.Seat;}
 public bool PollSpellImpact(){if(pendingSpellImpact==null||AnimationNowUtc()<spellImpactAt)return false;var action=pendingSpellImpact;pendingSpellImpact=null;spellImpactAt=DateTime.MaxValue;resolvingActions++;try{action();SyncTurnClock();MarkSearchStateChanged();}finally{resolvingActions--;}if(Finished)Log.Add(Winner==-1?"对局结束 · 平局":"对局结束 · "+PlayerLabel(Winner)+" 获胜");return true;}
}
}
