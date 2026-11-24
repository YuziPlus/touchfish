using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Touchfish {
public sealed class OpeningMulliganResult {
 public int[] ReplaceIds;
 public OpeningMulliganScore[] Candidates;
 public int Worlds,SamplingFailures;
 public string FallbackReason;
}
public sealed class OpeningMulliganScore {
 public int[] ReplaceIds;
 public double MeanValue;
}

public static partial class OpeningMulligan {
 internal static OpeningMulliganResult Fallback(AgentObservation view,string reason){
  return new OpeningMulliganResult{ReplaceIds=Curve(view),Candidates=new OpeningMulliganScore[0],FallbackReason=reason};
 }

 internal static OpeningMulliganResult Search(AgentObservation view,DeterminizationBuilder builder,DeckBelief belief,
  IBeliefSampler sampler,int seed,int samples,CancellationToken token){
  if(samples<1||samples>1024)throw new ArgumentOutOfRangeException("samples");
  if(!belief.Ready)return Fallback(view,belief.Diagnostic);
  var subsets=Enumerable.Range(0,1<<view.Hand.Length).Select(mask=>view.Hand
   .Where((card,i)=>(mask&(1<<i))!=0).Select(card=>card.Id).ToArray()).ToArray();
  var sums=new double[subsets.Length];
  var random=new SimulationRandom(seed);
  int complete=0,failed=0;
  for(int attempt=0;attempt<samples&&!token.IsCancellationRequested;attempt++){
   var sample=sampler.Sample(belief,random.Next(int.MaxValue));
   if(sample==null||!sample.Success||!ReferenceEquals(sample.Information,builder.Information)){failed++;continue;}
   var initial=builder.Build(sample);
   int other=1-view.Seat;
   var enemySelection=Curve(initial.SearchView(other,false));
   int policySeed=random.Next(int.MaxValue);
   var outcomes=new double[subsets.Length];
   bool valid=true;
   for(int i=0;i<subsets.Length;i++){
    if(token.IsCancellationRequested){valid=false;break;}
    var world=initial.Fork();
    if(!world.Mulligan(view.Seat,subsets[i]).Success||!world.Mulligan(other,enemySelection).Success){valid=false;break;}
    outcomes[i]=Rollout(world,view.Seat,policySeed);
   }
   if(!valid){if(!token.IsCancellationRequested)failed++;continue;}
   for(int i=0;i<sums.Length;i++)sums[i]+=outcomes[i];
   complete++;
  }
  if(complete==0){
   var result=Fallback(view,token.IsCancellationRequested?"换牌搜索已取消。":"没有成功采样的起手世界。");
   result.SamplingFailures=failed;
   return result;
  }
  var candidates=subsets.Select((ids,i)=>new OpeningMulliganScore{ReplaceIds=ids,MeanValue=sums[i]/complete}).ToArray();
  var best=candidates.OrderByDescending(candidate=>candidate.MeanValue)
   .ThenBy(candidate=>candidate.ReplaceIds.Length).First();
  return new OpeningMulliganResult{ReplaceIds=(int[])best.ReplaceIds.Clone(),Candidates=candidates,Worlds=complete,SamplingFailures=failed};
 }

 static double Rollout(FastSimulation world,int seat,int seed){
  var random=new SimulationRandom(seed);
  double total=0;
  int checkpoints=0;
  int ownTurnActions=0;
  for(int step=0;step<28&&world.Turn<=6&&!world.Finished;step++){
   int actor=world.ActiveSeat;
   var view=world.SearchView(actor,true);
   var actions=SearchPolicy.Actions(view);
   if(actions.Length==0)break;
   var action=SearchPolicy.Pick(view,actions,RolloutStyle.Heuristic,random);
   if(!action.Execute(world,actor).Success)throw new InvalidOperationException("模拟换牌后的合法操作执行失败。");
   var move=action.SimulatedMove;
   if(actor==seat&&move!=null&&move.Kind=="play")ownTurnActions++;
   if(move!=null&&move.Kind=="end"){
    total+=SearchPolicy.Evaluate(world.SearchView(seat,false));
    checkpoints++;
   }
  }
  if(world.Finished)return ExtremeSearch.TerminalValue(world.Winner,seat);
  double final=SearchPolicy.Evaluate(world.SearchView(seat,false));
  return Math.Min(1,0.55*final+0.35*(checkpoints==0?final:total/checkpoints)+0.1*Math.Min(1,ownTurnActions/4.0));
 }
}
}
