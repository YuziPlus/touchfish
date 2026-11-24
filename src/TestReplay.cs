using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Touchfish {
public sealed class ReplayAction {
 public string Kind,Choice,CardId,OptionId;
 public int Seat,Source,Position=-1,DecisionId;
 public MatchTarget Target;
 public int[] SelectedHandIds;
}

public sealed class ReplayCard {
 public string CardId,Name;
 public int Id,Cost;
 public bool Generated;
}

public sealed class ReplayUnit {
 public string CardId,Name;
 public int Id,Attack,Health;
 public bool Taunt,Shield,Stealth,Frozen,Silenced,Windfury,Charge,Poison,CanAttack;
}

public sealed class ReplayDecision {
 public int Id;
 public string Prompt;
 public ReplayDecisionOption[] Options;
}

public sealed class ReplayDecisionOption {
 public string Id,Title,Description;
 public int? Cost;
}

public sealed class ReplayPlayer {
 public PublicPlayerState Public;
 public ReplayCard[] Hand;
 public ReplayUnit[] Board;
 public string[] OwnSecrets;
 public int DeckCount;
 public ReplayDecision Decision;
}

public sealed class ReplayFrame {
 public int Index,FirstSeat,Active,Turn,Winner;
 public long ElapsedMs;
 public string[] Log;
 public AgentMove[] LegalMoves;
 public ReplayAction Action;
 public ReplayPlayer[] Players;
}

public sealed class ReplayDeck {
 public string ClassId,PoolId;
 // Exact input order for the seeded shuffle. Repeated IDs represent copies.
 public string[] Cards;

 internal static ReplayDeck From(DeckDocument deck){
  return new ReplayDeck{ClassId=deck.ClassId,PoolId=deck.PoolId,Cards=deck.Cards.SelectMany(pair=>Enumerable.Repeat(pair.Key,pair.Value)).ToArray()};
 }
}

public sealed class ReplayHeader {
 public int Schema=1;
 public int? Seed;
 public ReplayDeck[] InitialDecks;
 public string Mode="local-test",Version,Pool,Session;
 public DateTime StartedUtc;
 public bool TestData=true;
}

public sealed class TestReplay : IDisposable {
 readonly StreamWriter writer;
 readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
 readonly System.Web.Script.Serialization.JavaScriptSerializer serializer=JsonData.Serializer();
 int index,logCount;
 public readonly string Path;
 internal static bool Supports(GameModeDefinition mode){return mode==GameModes.LocalTest||mode==GameModes.StrictTest;}
 public TestReplay(string path,MatchEngine game,string version,DeckDocument first,DeckDocument second){
  if(!Supports(game.Mode))throw new InvalidOperationException("录像仅限本地 test。");
  var decks=new[]{ReplayDeck.From(first),ReplayDeck.From(second)};
  Path=path;
  writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)){AutoFlush=true};
  try{
   writer.WriteLine(serializer.Serialize(new ReplayHeader{Schema=2,Seed=game.Seed,InitialDecks=decks,Mode=game.Mode.Id,Version=version,Pool=game.CardPoolId,Session=game.VisualSession,StartedUtc=DateTime.UtcNow}));
   Append(game,null);
  }catch{writer.Dispose();throw;}
 }
 public void Append(MatchEngine game,ReplayAction action){
  var frame=new ReplayFrame{
   Index=index,ElapsedMs=clock.ElapsedMilliseconds,Action=action,FirstSeat=game.FirstSeat,Active=game.Active,Turn=game.Turn,Winner=game.Winner,
   LegalMoves=AgentBridge.Observe(game,game.Active,false,true).Moves,
   Log=game.Log.Skip(logCount).ToArray(),Players=Enumerable.Range(0,2).Select(seat=>{
    var player=game.Players[seat];
    var decision=game.ObserveDecision(seat);
    return new ReplayPlayer{
     Public=PublicPlayerState.From(player),DeckCount=player.Deck.Count,OwnSecrets=game.SecretCards(seat),
     Decision=decision==null?null:new ReplayDecision{Id=decision.Id,Prompt=decision.Prompt,Options=decision.Options.Select(o=>new ReplayDecisionOption{Id=o.Id,Title=o.Title,Description=o.Description,Cost=o.Cost}).ToArray()},
     Hand=player.Hand.Select(h=>new ReplayCard{Id=h.Id,CardId=h.CardId,Name=game.Card(h.CardId).Name,Cost=game.HandCost(seat,h),Generated=h.Generated}).ToArray(),
     Board=player.Board.Select(u=>new ReplayUnit{Id=u.Id,CardId=u.CardId,Name=game.Card(u.CardId).Name,Attack=game.AttackValue(u),Health=u.Health,Taunt=u.Taunt,Shield=u.Shield,Stealth=u.Stealth,Frozen=u.Frozen,Silenced=u.Silenced,Windfury=u.Windfury,Charge=u.Charge,Poison=u.Poison,CanAttack=game.CanAttack(seat,u)==null}).ToArray()
    };
   }).ToArray()
  };
  writer.WriteLine(serializer.Serialize(frame));
  index++;
  logCount=game.Log.Count;
 }
 public void Dispose(){writer.Dispose();}

 public static List<ReplayFrame> Load(string path){
  ReplayHeader header;
  return Load(path,out header);
 }

 public static List<ReplayFrame> Load(string path,out ReplayHeader header){
  using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
  using(var reader=new StreamReader(stream)){
   if(stream.Length>32*1024*1024)throw new InvalidDataException("录像文件过大。");
   var serializer=JsonData.Serializer();
   header=serializer.Deserialize<ReplayHeader>(reader.ReadLine()??"null");
   if(header==null||(header.Schema!=1&&header.Schema!=2)||(header.Mode!=GameModes.LocalTest.Id&&header.Mode!=GameModes.StrictTest.Id)||!header.TestData||String.IsNullOrEmpty(header.Session))throw new InvalidDataException("不支持此录像格式。");
   if(header.Schema==2&&(!header.Seed.HasValue||header.InitialDecks==null||header.InitialDecks.Length!=2||header.InitialDecks.Any(d=>d==null||String.IsNullOrEmpty(d.ClassId)||String.IsNullOrEmpty(d.PoolId)||d.Cards==null||d.Cards.Any(String.IsNullOrEmpty))))throw new InvalidDataException("录像缺少开局牌表或种子。");
   var frames=new List<ReplayFrame>();
   string line;
   while((line=reader.ReadLine())!=null){
    var frame=serializer.Deserialize<ReplayFrame>(line);
    if(frame==null||frame.Index!=frames.Count||frame.Active<0||frame.Active>1||frame.Winner< -2||frame.Winner>1||frame.Players==null||frame.Players.Length!=2||frame.Players.Any(p=>p==null||p.Public==null||p.Hand==null||p.Hand.Any(c=>c==null)||p.Board==null||p.Board.Any(u=>u==null)||p.OwnSecrets==null)||frame.Log==null||frame.LegalMoves==null||(frame.Index==0)!=(frame.Action==null)||frame.Action!=null&&(frame.Action.Seat<0||frame.Action.Seat>1))throw new InvalidDataException("录像帧无效。");
    frames.Add(frame);
   }
   if(frames.Count==0)throw new InvalidDataException("录像没有开局画面。");
   return frames;
  }
 }
}
}

