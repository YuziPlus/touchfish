using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Touchfish;

internal sealed class SelfPlayConfig {
 public int Games{get;set;}
 public int Seed{get;set;}
 public int Parallelism{get;set;}
 public int Iterations{get;set;}
 public int MulliganSamples{get;set;}
 public int MaxDecisions{get;set;}
 public string FirstSeat{get;set;}
 public string[] Decks{get;set;}
 public string[] Pool{get;set;}
 public string[] PriorPool{get;set;}
 public Dictionary<string,string> CustomDecks{get;set;}
 public string PoolId{get;set;}
 public bool Compression{get;set;}
 public bool Diagnostics{get;set;}
}

internal sealed class SelfPlayJob {
 public int Index,Seed,FirstSeat;
 public string[] Decks;
 public string Id{get{return "game-"+Index.ToString("D8");}}
}

internal sealed class SelfPlayShard {
 public string GameId{get;set;}
 public int Seed{get;set;}
 public int FirstSeat{get;set;}
 public int Winner{get;set;}
 public int Turns{get;set;}
 public string[] Decks{get;set;}
 public string File{get;set;}
 public string Sha256{get;set;}
 public long Bytes{get;set;}
 public int Samples{get;set;}
 public int Fallbacks{get;set;}
 public int Forced{get;set;}
 public int Iterations{get;set;}
 public int SamplingFailures{get;set;}
 public int SamplingAttempts{get;set;}
}

internal sealed class SelfPlaySearch {
 public string mode,stop_reason,fallback_reason;
 public int iterations,successful_samples,sampling_attempts,sampling_failures;
 public Dictionary<string,int> sampling_failure_reasons;
}

internal sealed class SelfPlayCandidate {
 public string key,kind,source_zone,source_card,target_side,target_zone,target_card,choice;
 public int? source_slot,target_slot,position,option_index,visits;
 public int[] replace_slots;
 public double? value;
}

internal static class SelfPlay {
 internal const int Schema=1;
 internal const string Model="extreme-heuristic-baseline";
 static readonly Encoding Utf8=new UTF8Encoding(false);

 static int Main(string[] args){
  try{
#if SELFPLAY_VERIFY
   if(args.Length==2&&args[1]=="--verify"){SelfPlayChecks.Run(CardCatalog.Load(args[0]));return 0;}
#endif
   if(args.Length!=4)throw new ArgumentException("Usage: SelfPlay ROOT CONFIG OUTPUT BUILD_FINGERPRINT");
   Run(args[0],args[1],args[2],args[3]);
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
 }

 internal static SelfPlayJob[] Schedule(SelfPlayConfig config){
  var random=new SimulationRandom(config.Seed);
  var result=new SelfPlayJob[config.Games];
  int seed=0,first=0;
  string[] decks=null;
  for(int i=0;i<result.Length;i++){
   if(config.FirstSeat!="paired"||i%2==0){
    seed=random.Next(int.MaxValue);
    first=config.FirstSeat=="seat0"?0:config.FirstSeat=="seat1"?1:random.Next(2);
    decks=config.Decks??new[]{config.Pool[random.Next(config.Pool.Length)],config.Pool[random.Next(config.Pool.Length)]};
   }
   result[i]=new SelfPlayJob{Index=i,Seed=seed,FirstSeat=config.FirstSeat=="paired"&&i%2==1?1-first:first,Decks=decks};
  }
  return result;
 }

 static Dictionary<string,DeckDocument> LoadDecks(CardCatalog catalog,SelfPlayConfig config){
  var definitions=MatchAgents.All.ToDictionary(d=>d.Id,d=>d.Deck,StringComparer.Ordinal);
  var decks=new Dictionary<string,DeckDocument>(StringComparer.Ordinal);
  var ids=(config.Decks??config.Pool).Concat(config.PriorPool??new string[0]).Distinct(StringComparer.Ordinal);
  foreach(string id in ids){
   Func<CardCatalog,DeckDocument> factory;
   string code;
   if(config.CustomDecks!=null&&config.CustomDecks.TryGetValue(id,out code))decks.Add(id,DeckCode.Decode(catalog,code,config.PoolId));
   else if(definitions.TryGetValue(id,out factory))decks.Add(id,factory(catalog));
   else throw new ArgumentException("Unknown deck: "+id);
   if(decks[id].PoolId!=config.PoolId)throw new ArgumentException("Mixed card pools: "+id);
  }
  return decks;
 }

 static void Validate(SelfPlayConfig c){
  if(c==null||c.Games<1||c.Games>1000000||c.Parallelism<1||c.Parallelism>64||c.Iterations<1||
   c.MulliganSamples<1||c.MulliganSamples>1024||c.MaxDecisions<3||c.MaxDecisions>10000||
   !new[]{"paired","random","seat0","seat1"}.Contains(c.FirstSeat)||String.IsNullOrWhiteSpace(c.PoolId)||
   (c.Decks==null?(c.Pool==null||c.Pool.Length==0):c.Decks.Length!=2))
   throw new ArgumentException("Invalid self-play configuration.");
 }

 static void Run(string root,string configPath,string output,string fingerprint){
  var config=JsonData.Read<SelfPlayConfig>(configPath);
  Validate(config);
  var catalog=CardCatalog.Load(root);
  var decks=LoadDecks(catalog,config);
  var jobs=Schedule(config);
  var budget=new SearchBudget{MaxIterations=config.Iterations,MaxSamplingAttempts=config.Iterations};
  // Only this separately declared pool reaches P2. The selected opponent deck is never passed in.
  var prior=(config.PriorPool??new string[0]).Select(id=>decks[id]).ToArray();
  string identity=JsonData.Serializer().Serialize(new{schema_version=Schema,model=Model,build=fingerprint,
   config.Games,config.Seed,config.FirstSeat,config.Decks,config.Pool,config.PriorPool,config.CustomDecks,config.PoolId,
   config.Compression,config.MaxDecisions,config.MulliganSamples,budget});
  Directory.CreateDirectory(output);
  using(var locked=new FileStream(Path.Combine(output,"run.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
  using(var cancel=new CancellationTokenSource()){
   string identityPath=Path.Combine(output,"config.json");
   if(File.Exists(identityPath)&&File.ReadAllText(identityPath)!=identity)throw new InvalidDataException("Output has a different configuration or source build.");
   if(!File.Exists(identityPath))AtomicText(identityPath,identity);
   string shards=Path.Combine(output,"shards"), diagnostics=Path.Combine(output,"diagnostics");
   Directory.CreateDirectory(shards);
   Directory.CreateDirectory(diagnostics);
   var completed=new SortedDictionary<int,SelfPlayShard>();
   foreach(var job in jobs){
    string receipt=Path.Combine(shards,job.Id+".json");
    if(!File.Exists(receipt))continue;
    var shard=JsonData.Read<SelfPlayShard>(receipt);
    var expected=job.Id+(config.Compression?".jsonl.gz":".jsonl");
    var data=new FileInfo(Path.Combine(shards,expected));
    if(shard.GameId!=job.Id||shard.Seed!=job.Seed||shard.FirstSeat!=job.FirstSeat||!shard.Decks.SequenceEqual(job.Decks)||
     shard.File!=expected||!data.Exists||data.Length!=shard.Bytes||Hash(data.FullName)!=shard.Sha256)
     throw new InvalidDataException("Invalid completed shard: "+job.Id);
    completed.Add(job.Index,shard);
   }
   int errors=0,startingCount=completed.Count;
   var outcomes=new int[3];
   foreach(var shard in completed.Values)outcomes[shard.Winner+1]++;
   var remaining=jobs.Where(job=>!completed.ContainsKey(job.Index)).ToArray();
   var clock=Stopwatch.StartNew();
   int[] gc=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
   ConsoleCancelEventHandler onCancel=(sender,eventArgs)=>{eventArgs.Cancel=true;cancel.Cancel();};
   Console.CancelKeyPress+=onCancel;
   try{
    Parallel.ForEach(remaining,new ParallelOptions{MaxDegreeOfParallelism=config.Parallelism},(job,state)=>{
     if(cancel.IsCancellationRequested){state.Stop();return;}
     try{
      var shard=Play(catalog,job,decks,budget,prior,config,shards,diagnostics,cancel.Token);
      lock(completed){
       completed.Add(job.Index,shard);
       outcomes[shard.Winner+1]++;
       Console.WriteLine("{0}/{1} {2} winner={3} wins={4}/{5} draws={6} {7:F3} games/s",completed.Count,jobs.Length,job.Id,shard.Winner,
        outcomes[1],outcomes[2],outcomes[0],
        (completed.Count-startingCount)/Math.Max(0.001,clock.Elapsed.TotalSeconds));
      }
     }catch(Exception ex){
      lock(completed){
       errors++;
       JsonData.Write(Path.Combine(diagnostics,job.Id+".error.json"),new{job.Id,job.Seed,job.FirstSeat,job.Decks,Error=ex.ToString()});
       Console.Error.WriteLine(job.Id+": "+ex.Message);
      }
     }
    });
   }finally{
    Console.CancelKeyPress-=onCancel;
    var all=completed.Values.ToArray();
    JsonData.Write(Path.Combine(output,"manifest.json"),new{schema_version=Schema,model=Model,build=fingerprint,games=all});
    JsonData.Write(Path.Combine(output,"summary.json"),new{requested=jobs.Length,completed=all.Length,new_games=all.Length-startingCount,errors,
     cancelled=cancel.IsCancellationRequested,wins=new[]{all.Count(x=>x.Winner==0),all.Count(x=>x.Winner==1)},draws=all.Count(x=>x.Winner==-1),
     samples=all.Sum(x=>(long)x.Samples),bytes=all.Sum(x=>x.Bytes),fallbacks=all.Sum(x=>(long)x.Fallbacks),forced=all.Sum(x=>(long)x.Forced),
     sampling_failures=all.Sum(x=>(long)x.SamplingFailures),sampling_attempts=all.Sum(x=>(long)x.SamplingAttempts),
     wall_seconds=clock.Elapsed.TotalSeconds,parallelism=config.Parallelism,gc=Enumerable.Range(0,3).Select(n=>GC.CollectionCount(n)-gc[n]).ToArray(),
     peak_working_set_bytes=Process.GetCurrentProcess().PeakWorkingSet64,managed_bytes=GC.GetTotalMemory(false)});
   }
   if(completed.Count!=jobs.Length)throw new InvalidOperationException("Run incomplete. See diagnostics and rerun with the same configuration.");
  }
 }

 internal static SelfPlayShard Play(CardCatalog catalog,SelfPlayJob job,Dictionary<string,DeckDocument> decks,SearchBudget budget,
  DeckDocument[] priorDecks,SelfPlayConfig config,string shards,string diagnostics,CancellationToken token){
  var own=job.Decks.Select(id=>decks[id].Copy()).ToArray();
  var game=new MatchEngine(catalog,own[0],own[1],job.Seed,startingSeat:job.FirstSeat);
  game.ClockNowMs=()=>0; // Offline turn clocks do not consume wall time while searching.
  var searches=new[]{new ExtremeSearch(),new ExtremeSearch()};
  var seedStream=new SimulationRandom(job.Seed ^ 0x13ed412b);
  var random=new[]{new SimulationRandom(seedStream.Next(int.MaxValue)),new SimulationRandom(seedStream.Next(int.MaxValue))};
  var priors=own.Select(_=>new LegalDeckPrior(priorDecks,priorDecks.Select(d=>1).ToArray(),priorDecks.Length==0?1:0)).ToArray();
  var serializer=JsonData.Serializer();
  var rows=new List<string>();
  var result=new SelfPlayShard{GameId=job.Id,Seed=job.Seed,FirstSeat=job.FirstSeat,Decks=job.Decks};
  var replacements=new int[2][];
  var known=own.Select(d=>d.Cards.OrderBy(kv=>kv.Key,StringComparer.Ordinal).Select(kv=>new{card_id=kv.Key,count=kv.Value}).ToArray()).ToArray();
  Action<AgentObservation,SelfPlayCandidate[],string,SelfPlaySearch> record=(view,candidates,chosen,search)=>{
   if(candidates.Count(c=>c.key==chosen)!=1)throw new InvalidDataException("Selected action is not uniquely legal.");
   rows.Add(serializer.Serialize(new{schema_version=Schema,game_id=job.Id,game_seed=job.Seed,seat=view.Seat,turn=view.Turn,
    decision_index=rows.Count,player_class=view.Self.ClassId,model=Model,search_config=budget,mulligan_samples=config.MulliganSamples,
    observation=Observation(game,view,known[view.Seat]),legal_actions=candidates,selected_action=chosen,search}));
   result.Iterations+=search.iterations;
   result.SamplingAttempts+=search.sampling_attempts;
   result.SamplingFailures+=search.sampling_failures;
   if(search.fallback_reason!=null)result.Fallbacks++;
   if(search.mode=="forced")result.Forced++;
  };

  // Both observations and choices are captured before either player confirms.
  for(int seat=0;seat<2;seat++){
   token.ThrowIfCancellationRequested();
   var view=AgentBridge.Observe(game,seat,false,false);
   int rng=random[seat].Next(int.MaxValue);
   var opening=searches[seat].ChooseMulligan(game,seat,own[seat],null,rng,config.MulliganSamples,token,priors[seat]);
   replacements[seat]=opening.ReplaceIds;
   var scores=opening.Candidates.ToDictionary(x=>Mask(view,x.ReplaceIds),x=>x.MeanValue);
   var candidates=Enumerable.Range(0,1<<view.Hand.Length).Select(mask=>new SelfPlayCandidate{key="mulligan:"+mask,kind="mulligan",
    replace_slots=Enumerable.Range(0,view.Hand.Length).Where(i=>(mask&(1<<i))!=0).ToArray(),
    value=scores.ContainsKey(mask)?(double?)scores[mask]:null}).ToArray();
   record(view,candidates,"mulligan:"+Mask(view,replacements[seat]),new SelfPlaySearch{mode="mulligan",fallback_reason=opening.FallbackReason,
    successful_samples=opening.Worlds,sampling_attempts=opening.Worlds+opening.SamplingFailures,sampling_failures=opening.SamplingFailures});
  }
  for(int seat=0;seat<2;seat++)Require(game.Mulligan(seat,replacements[seat]));

  while(!game.Finished){
   token.ThrowIfCancellationRequested();
   if(rows.Count>=config.MaxDecisions)throw new InvalidOperationException("Decision limit reached at turn "+game.Turn+" after "+rows.Count+" decisions.");
   int seat=game.Active;
   var view=AgentBridge.Observe(game,seat,false,true);
   var actions=SearchPolicy.Actions(view);
   if(actions.Length==0)throw new InvalidOperationException("No legal action at turn "+game.Turn);
   var search=actions.Length==1?null:searches[seat].Choose(game,seat,own[seat],null,budget,random[seat].Next(int.MaxValue),token,priors[seat]);
   token.ThrowIfCancellationRequested();
   var chosen=search==null?actions[0]:search.BestMove;
   if(chosen==null)throw new InvalidOperationException("Search returned no action.");
   var stats=search==null||search.RootActionStatistics==null?new Dictionary<string,RootActionStatistics>():
    search.RootActionStatistics.ToDictionary(x=>x.ActionKey);
   record(view,actions.Select((action,index)=>Candidate(view,action,index,stats)).ToArray(),chosen.ActionKey,
    search==null?new SelfPlaySearch{mode="forced"}:new SelfPlaySearch{mode="extreme",iterations=search.Iterations,
     successful_samples=search.SuccessfulSamples,sampling_attempts=search.SamplingAttempts,sampling_failures=search.SamplingFailures,
     stop_reason=search.StopReason,fallback_reason=search.FallbackReason,sampling_failure_reasons=search.SamplingFailureReasons});
   Require(chosen.Execute(game,seat));
  }
  result.Winner=game.Winner;
  result.Turns=game.Turn;
  result.Samples=rows.Count;
  result.File=job.Id+(config.Compression?".jsonl.gz":".jsonl");
  string path=Path.Combine(shards,result.File);
  token.ThrowIfCancellationRequested();
  Publish(path,rows,result,game,config.Compression,serializer);
  result.Bytes=new FileInfo(path).Length;
  result.Sha256=Hash(path);
  if(config.Diagnostics)JsonData.Write(Path.Combine(diagnostics,job.Id+".json"),new{result,log=game.Log});
  JsonData.Write(Path.Combine(shards,job.Id+".json"),result);
  return result;
 }

 internal static object Observation(MatchEngine game,AgentObservation view,object ownDeck){
  return new{view.Seat,view.Turn,view.FirstSeat,view.ActiveSeat,view.CardPoolId,view.BoardSlots,view.HandLimit,view.ManaLimit,
   view.MulliganComplete,view.CanAct,view.SpellPower,view.PowerCost,view.PowerDamage,view.CardsPlayed,view.DeathsThisTurn,
   view.PreparationDiscount,view.Self,view.Enemy,view.Hand,view.OwnSecrets,view.Decision,own_initial_deck=ownDeck,
   history=game.ObserveHistory(view.Seat)};
 }

 static int Mask(AgentObservation view,int[] ids){
  int mask=0;
  foreach(int id in ids){
   int slot=Array.FindIndex(view.Hand,card=>card.Id==id);
   if(slot<0||(mask&(1<<slot))!=0)throw new InvalidDataException("Illegal mulligan subset.");
   mask|=1<<slot;
  }
  return mask;
 }

 internal static SelfPlayCandidate Candidate(AgentObservation view,SearchAction action,int index,Dictionary<string,RootActionStatistics> stats){
  var move=action.Move;
  var candidate=new SelfPlayCandidate{key=action.ActionKey,kind=move==null?"decision":move.Kind};
  RootActionStatistics value;
  if(stats.TryGetValue(action.ActionKey,out value)){
   candidate.visits=value.Visits;
   candidate.value=value.Visits>0?(double?)value.MeanValue:null;
  }
  if(move==null){candidate.option_index=index;candidate.choice=action.OptionId;return candidate;}
  candidate.position=move.Position>=0?(int?)move.Position:null;
  candidate.choice=move.Choice;
  if(move.Kind=="play"){
   candidate.source_zone="hand";
   int slot=Array.FindIndex(view.Hand,card=>card.Id==move.Source);
   candidate.source_slot=slot;
   candidate.source_card=view.Hand[slot].CardId;
  }else if(move.Kind=="attack"&&move.Source!=0){
   candidate.source_zone="board";
   int slot=Array.FindIndex(view.Self.Board,unit=>unit.Id==move.Source);
   candidate.source_slot=slot;
   candidate.source_card=view.Self.Board[slot].CardId;
  }else if(move.Kind=="attack"||move.Kind=="power")candidate.source_zone="hero";
  if(move.Target!=null){
   bool self=move.Target.Seat==view.Seat;
   candidate.target_side=self?"self":"enemy";
   candidate.target_zone=move.Target.Hero?"hero":"board";
   if(!move.Target.Hero){
    var board=(self?view.Self:view.Enemy).Board;
    int slot=Array.FindIndex(board,unit=>unit.Id==move.Target.UnitId);
    candidate.target_slot=slot;
    candidate.target_card=board[slot].CardId;
   }
  }
  return candidate;
 }

 static void Require(ActionResult result){
  if(!result.Success)throw new InvalidOperationException("Authoritative engine rejected legal action: "+result.Message);
 }

 static void Publish(string path,List<string> rows,SelfPlayShard result,MatchEngine game,bool compression,System.Web.Script.Serialization.JavaScriptSerializer serializer){
  string temp=path+".tmp";
  try{
   using(var file=File.Create(temp))
   using(var payload=compression?(Stream)new GZipStream(file,CompressionMode.Compress):file)
   using(var writer=new StreamWriter(payload,Utf8)){
    writer.NewLine="\n";
    foreach(string row in rows){
     var data=serializer.DeserializeObject(row) as Dictionary<string,object>;
     int seat=(int)data["seat"];
     data["z"]=result.Winner<0?0:result.Winner==seat?1:-1;
     data["final"]=new{winner=result.Winner,turns=result.Turns,health=game.Players.Select(player=>player.Health).ToArray()};
     writer.WriteLine(serializer.Serialize(data));
    }
   }
   Replace(temp,path);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }

 static void AtomicText(string path,string text){
  string temp=path+".tmp";
  File.WriteAllText(temp,text,Utf8);
  Replace(temp,path);
 }
 static void Replace(string temp,string path){if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 internal static string Hash(string path){
  using(var file=File.OpenRead(path))
  using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
 }
}
