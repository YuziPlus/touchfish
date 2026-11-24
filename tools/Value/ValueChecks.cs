using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Touchfish;

internal sealed class ValueChecks {
 public AgentObservation observation{get;set;}
 public float[] features{get;set;}
 public double prediction{get;set;}
 public double heuristic{get;set;}

 static void Require(bool yes,string reason){if(!yes)throw new InvalidOperationException(reason);}
 public static int Main(string[] args){
  try{
   var model=ValueNetwork.Load(Path.Combine(args[1],"value-v1.bin"));
   string reason;
   Require(ValueNetwork.TryLoad(Path.Combine(args[1],"missing.bin"),out reason)==null&&reason!=null,"Missing model fallback");
   string corrupt=Path.GetTempFileName();
   try{
    File.WriteAllBytes(corrupt,new byte[16]);
    Require(ValueNetwork.TryLoad(corrupt,out reason)==null&&reason!=null,"Incompatible model fallback");
   }finally{File.Delete(corrupt);}
   var rows=JsonData.Read<ValueChecks[]>(Path.Combine(args[1],"parity.json"));
   foreach(var row in rows){
    var x=ValueNetwork.Encode(row.observation);
    Require(x.Length==row.features.Length&&x.Zip(row.features,(a,b)=>Math.Abs(a-b)).Max()<1e-6,"Feature parity");
    Require(Math.Abs(model.Predict(row.observation)-row.prediction)<2e-6,"Prediction parity");
    Require(Math.Abs(SearchPolicy.Evaluate(row.observation)-row.heuristic)<1e-10,"Heuristic parity");
    if(row.observation.Hand.Length>0){
     row.observation.Hand[0].CardId="UNKNOWN_FUTURE_CARD";
     Require(Math.Abs(model.Predict(row.observation)-row.prediction)<2e-6,"Unknown card identity changed attribute encoding");
    }
   }
   var catalog=CardCatalog.Load(args[0]);
   var zoo=ZooWarlockDeck.Create(catalog);
   var freeze=FreezeMageDeck.Create(catalog);
   foreach(int root in new[]{0,1}){
    var live=new MatchEngine(catalog,zoo,freeze,7311+root,startingSeat:root);
    live.Mulligan(0,null);live.Mulligan(1,null);
    live.Players[root].Mana=10;
    var budget=new SearchBudget{MaxIterations=24,MaxActions=1,MaxTurns=1};
    var own=root==0?zoo:freeze;var enemy=root==0?freeze:zoo;
    var baseline=new ExtremeSearch().Choose(live,root,own,enemy,budget,829);
    var shadow=new ExtremeSearch(valueNetwork:model,valueMode:LeafValueMode.Shadow).Choose(live,root,own,enemy,budget,829);
    var neural=new ExtremeSearch(valueNetwork:model,valueMode:LeafValueMode.Neural).Choose(live,root,own,enemy,budget,829);
    var missing=new ExtremeSearch(valueMode:LeafValueMode.Neural).Choose(live,root,own,enemy,budget,829);
    Require(baseline.BestMove.ActionKey==shadow.BestMove.ActionKey&&
     JsonData.Serializer().Serialize(baseline.RootActionStatistics)==JsonData.Serializer().Serialize(shadow.RootActionStatistics),"Shadow changed search");
    Require(JsonData.Serializer().Serialize(baseline.RootActionStatistics)==JsonData.Serializer().Serialize(missing.RootActionStatistics),"Missing model changed fallback");
    Require(neural.LeafValue.Calls>0&&neural.LeafValue.Failures==0,"No real neural inference");
    Require(SearchPolicy.Actions(AgentBridge.Observe(live,root,false,true)).Any(a=>a.ActionKey==neural.BestMove.ActionKey),"Illegal neural action");
    var visible=AgentBridge.Observe(live,root,false,true);
    live.Players[1-root].Deck.Reverse();
    live.Players[1-root].Hand[0].CardId="VAN_EX1_289";
    Require(ValueNetwork.Encode(visible).SequenceEqual(ValueNetwork.Encode(AgentBridge.Observe(live,root,false,true))),"Hidden permutation changed features");
    var lethal=new MatchEngine(catalog,zoo,freeze,8449,startingSeat:root);
    lethal.Mulligan(0,null);lethal.Mulligan(1,null);
    lethal.Players[1-root].Health=1;
    var attacker=lethal.Summon(root,"VAN_CS2_182");
    attacker.SummonTurn=0;
    var terminal=new ExtremeSearch(valueNetwork:model,valueMode:LeafValueMode.Neural).Choose(lethal,root,own,enemy,budget,830);
    var lethalKey=SearchPolicy.Actions(AgentBridge.Observe(lethal,root,false,true)).Single(a=>a.Move.Kind=="attack"&&a.Move.Target.Hero).ActionKey;
    Require(terminal.RootActionStatistics.Any(a=>a.ActionKey==lethalKey&&a.TerminalWinCount>0&&a.MeanValue==1),"Terminal win was not scored from the root perspective");
   }
   var durations=new double[12000];
   for(int i=0;i<200;i++)model.Predict(rows[i%rows.Length].observation);
   long startMemory=GC.GetTotalMemory(true);
   int gen0=GC.CollectionCount(0);
   for(int i=0;i<durations.Length;i++){
    long begin=Stopwatch.GetTimestamp();
    model.Predict(rows[i%rows.Length].observation);
    durations[i]=(Stopwatch.GetTimestamp()-begin)*1000000.0/Stopwatch.Frequency;
   }
   Array.Sort(durations);
   Console.WriteLine("PASS parity and search checks; predictions={0} mean_us={1:F2} p95_us={2:F2} model_bytes={3} retained_delta={4} gen0={5}",durations.Length,durations.Average(),durations[(int)(.95*durations.Length)],new FileInfo(Path.Combine(args[1],"value-v1.bin")).Length,GC.GetTotalMemory(true)-startMemory,GC.CollectionCount(0)-gen0);
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
}


