using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
// Rules enqueue work in insertion order; presentation never enters this queue.
public sealed class ResolutionQueue {
 readonly Queue<Action> pending=new Queue<Action>();bool draining;
 internal bool IsResolving{get{return draining||pending.Count!=0;}}
 public int Count{get{return pending.Count;}}
 public void Enqueue(Action action){if(action==null)throw new ArgumentNullException("action");pending.Enqueue(action);}
 public void Drain(){if(draining)return;draining=true;try{int steps=0;while(pending.Count>0){if(++steps>10000)throw new InvalidOperationException("结算超过安全上限，可能存在循环触发。");pending.Dequeue()();}}finally{draining=false;}}
}

public sealed partial class MatchEngine {
 BattleUnit[] damageListeners;ResolutionQueue damageTriggers;
 void FlushDamageTriggers(){var pending=damageTriggers;damageTriggers=null;damageListeners=null;if(pending!=null)pending.Drain();}
 void TriggerHealing(MatchTarget target,BattleUnit unit,int amount){var listeners=Players.SelectMany(p=>p.Board).Where(u=>u.Health>0&&!u.Silenced).OrderBy(u=>u.Id).ToArray();Action notify=()=>{if(unit!=null)ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.MinionHealed,target.Seat,healed:unit,amount:amount,listeners:listeners);ConditionalTriggerRules.Resolve(this,MatchTriggerTiming.CharacterHealed,target.Seat,healed:unit,amount:amount,healedTarget:target,listeners:listeners);};if(damageTriggers==null)notify();else damageTriggers.Enqueue(notify);}
 void TriggerDamage(BattleUnit unit,int amount,BattleUnit[] listeners){if(damageTriggers==null)ConditionalTriggerRules.MinionDamaged(this,unit,amount,listeners);else damageTriggers.Enqueue(()=>ConditionalTriggerRules.MinionDamaged(this,unit,amount,listeners));}
 // A simultaneous damage batch retains the listeners present before its first hit,
 // including mortally wounded listeners until death processing starts.
 public void DamageBatch(Action action){if(damageListeners!=null){action();return;}damageListeners=Players.SelectMany(p=>p.Board).Where(u=>u.Health>0&&!u.Silenced).OrderBy(u=>u.Id).ToArray();damageTriggers=new ResolutionQueue();try{action();FlushDamageTriggers();}finally{damageListeners=null;damageTriggers=null;}}
 public void DamageSequenceStep(MatchTarget target,int amount){var previous=damageListeners;var pending=damageTriggers;damageListeners=null;damageTriggers=null;try{Damage(target,amount);Cleanup();}finally{damageListeners=previous;damageTriggers=pending;}}
}
}
