using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Touchfish;
internal sealed class PolicyCheck {
 public AgentObservation observation{get;set;}
 public Candidate[] actions{get;set;}
 public float[] features{get;set;}
 public float[][] action_features{get;set;}
 public double[] logits{get;set;}
 public sealed class Candidate {
  public string kind{get;set;} public string source_card{get;set;} public string choice{get;set;}
  public string target_side{get;set;} public string target_zone{get;set;}
  public int? source_slot{get;set;} public int? target_slot{get;set;} public int? position{get;set;} public int? option_index{get;set;}
  public int[] replace_slots{get;set;}
  public float[] Encode(){return PolicyNetwork.EncodeAction(kind,source_slot??-1,target_slot??-1,position??-1,option_index??-1,target_side,target_zone,source_card,choice,replace_slots);}
 }
 static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
 public static int Main(string[] args){
  try{
   string error;
   var model=PolicyNetwork.TryLoad(Path.Combine(args[1],"policy-v1.bin"),out error);
   Check(model!=null,error);
   Check(PolicyNetwork.TryLoad(Path.Combine(args[1],"missing.bin"),out error)==null&&error!=null,"Missing policy fallback");
   foreach(var r in JsonData.Read<PolicyCheck[]>(Path.Combine(args[1],"parity.json"))){
    var x=ValueNetwork.Encode(r.observation);
    var a=r.actions.Select(c=>c.Encode()).ToArray();
    Check(x.Zip(r.features,(u,v)=>Math.Abs(u-v)).Max()<1e-6,"Observation parity");
    for(int i=0;i<a.Length;i++)Check(a[i].SequenceEqual(r.action_features[i]),"Action parity");
    var scores=model.Logits(x,a);
    Check(scores.Zip(r.logits,(u,v)=>Math.Abs(u-v)).Max()<2e-5,"Logit parity");
    var p=PolicyNetwork.Probabilities(scores);
    Check(p.All(v=>v>0)&&Math.Abs(p.Sum()-1)<1e-12,"Policy floor or normalization");
   }
   var catalog=CardCatalog.Load(args[0]);
   var deck=FreezeMageDeck.Create(catalog);
   var live=new DynamicDecisionFixture(catalog).OpenForBelief(514,deck,deck);
   var v0=AgentBridge.Observe(live,0,false,true);
   var options=SearchPolicy.Actions(v0);
   Check(options.Length==3&&options.Select((a,i)=>PolicyNetwork.EncodeAction(v0,a,i)[31+i]).All(v=>v==1),"Dynamic options not distinct");
   var result=new ExtremeSearch(policyNetwork:model).Choose(live,0,deck,deck,new SearchBudget{MaxIterations=16},128);
   Check(result.Policy!=null&&result.Policy.Calls>0&&options.Any(a=>a.ActionKey==result.BestMove.ActionKey),"Dynamic PUCT legality");
   live.Players[1].Deck.Reverse();
   live.Players[1].Hand[0].CardId="FUTURE_HIDDEN_CARD";
   var repeated=new ExtremeSearch(policyNetwork:model).Choose(live,0,deck,deck,new SearchBudget{MaxIterations=16},128);
   Check(result.BestMove.ActionKey==repeated.BestMove.ActionKey&&
    JsonData.Serializer().Serialize(result.RootActionStatistics)==JsonData.Serializer().Serialize(repeated.RootActionStatistics),
    "Hidden permutation affected seeded PUCT");
   var fixture=new AgentObservation{Seat=0,Hand=new[]{new AgentCard{Id=77,CardId="FUTURE_CARD"}},Self=new AgentPlayer{Board=new AgentUnit[0]},Enemy=new AgentPlayer{Board=new[]{new AgentUnit{Id=11},new AgentUnit{Id=12}}}};
   var first=new SearchAction("a",new AgentMove{Kind="play",Source=77,Position=0,Target=new MatchTarget(1,11)});
   var second=new SearchAction("b",new AgentMove{Kind="play",Source=77,Position=1,Target=new MatchTarget(1,12)});
   var fa=PolicyNetwork.EncodeAction(fixture,first,0);var fb=PolicyNetwork.EncodeAction(fixture,second,0);
   Check(fa[16]==1&&fb[17]==1&&fa[23]==1&&fb[24]==1,"Target and position encoding");
   Check(!PolicyNetwork.EncodeAction("mulligan",-1,-1,-1,-1,null,null,null,null,new[]{0}).SequenceEqual(
    PolicyNetwork.EncodeAction("mulligan",-1,-1,-1,-1,null,null,null,null,new[]{1})),"Mulligan subsets collapsed");
   Console.WriteLine("PASS policy parity, dynamic options, targets, positions, mulligan subsets, unknown IDs and load fallback.");
   return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
