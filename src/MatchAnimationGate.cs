using System;


namespace Touchfish {
public sealed partial class MatchEngine {
 public const int EntranceAnimationMs=2000;
 EffectLevel animationEffects;
 public bool EntranceAnimationsEnabled{get{return !headless&&animationEffects>=EffectLevel.Low;}set{animationEffects=value?EffectLevel.High:EffectLevel.Off;}}
 public bool HighAnimationsEnabled{get{return !headless&&animationEffects==EffectLevel.High;}}
 public void SetAnimationEffects(EffectLevel level){animationEffects=EffectSettings.FromWire((int)level);}
 public int AnimationSequence;
 DateTime animationUntil=DateTime.MinValue;
 internal Func<DateTime> AnimationNowUtc=()=>DateTime.UtcNow;
 public int AnimationRemainingMs{get{return headless?0:Math.Max(SpellImpactPending?1:0,(int)Math.Ceiling((animationUntil-AnimationNowUtc()).TotalMilliseconds));}}
 public bool AnimationPlaying{get{return AnimationRemainingMs>0;}}
 internal void StartEntranceWait(){if(!EntranceAnimationsEnabled)return;StartAnimationWait(EntranceAnimationMs);}
 internal void StartAnimationWait(int duration){AnimationSequence++;animationUntil=AnimationNowUtc().AddMilliseconds(duration);}
 internal void ReceiveAnimationWait(int sequence,int remaining){if(sequence!=AnimationSequence){AnimationSequence=sequence;animationUntil=DateTime.UtcNow.AddMilliseconds(remaining);}else if(remaining==0)animationUntil=DateTime.MinValue;}
 internal void LimitAnimationWait(int remaining){animationUntil=DateTime.UtcNow.AddMilliseconds(Math.Min(AnimationRemainingMs,remaining));}
}
}
