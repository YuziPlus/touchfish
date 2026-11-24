using System;
using System.IO;
using System.Linq;

namespace Touchfish {
public partial class PanelWindow {
 public void PreviewTestReplay(string folder){
  previewRun=true;
  Directory.CreateDirectory(folder);
  var savedDraft=draft;
  try{
   draft=new DeckDocument{ClassId="MAGE",PoolId=catalog.Pools.First(p=>!p.Archived).Id};
   SyncDeckControls();
   showAllCards.IsChecked=true;
   versionPicker.SelectedItem=versionPicker.Items.Cast<Choice>().First(c=>c.Key==GvgRules.SetId);
   RefreshCollection();
   Check(!versionPicker.Items.Cast<Choice>().Any(c=>c.Key==GvgClassRules.SetId)&&filteredCards.Any(c=>c.SetId==GvgRules.SetId)&&filteredCards.Any(c=>c.SetId==GvgClassRules.SetId),"Unified GvG filter includes neutral and class cards");
  }finally{draft=savedDraft;SyncDeckControls();}
  OpenLocalTest();
  Check(currentPage=="lobby"&&recordNextTest.IsVisible&&recordNextTest.IsChecked==false,"Test lobby exposes opt-in recording");
  ApplyTheme(true,true);
  ApplyMode(true);
  Render(Path.Combine(folder,"test-replay-minimal.png"));
  var first=LobbyDeck(myClass,myDeck,false);
  var second=LobbyDeck(opponentClass,opponentDeck,false);
  StartConfiguredMatch();
  Check(match.TestMode&&testReplay==null,"Default test match does not record");

  string path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".jsonl");
  try{
   testReplay=new TestReplay(path,match,AppRelease.Version,first,second);
   recordedTest=match;
   int id=match.Players[0].Hand[0].Id;
   DoMatch("mulligan",selectedIds:new[]{id});
   matchViewSeat=1;
   awaitHandoff=false;
   DoMatch("mulligan",selectedIds:new int[0]);
   matchViewSeat=0;
   awaitHandoff=false;
   DoMatch("play",Int32.MaxValue);
   DoMatch("end");

   ReplayHeader header;
   var frames=TestReplay.Load(path,out header);
   Check(header.Schema==2&&header.Seed==match.Seed&&header.InitialDecks.Length==2,"Replay records the actual seed and both decks");
   var rebuilt=new MatchEngine(catalog,ReadReplayDeck(header.InitialDecks[0]),ReadReplayDeck(header.InitialDecks[1]),header.Seed.Value,mode:GameModes.Get(header.Mode),startingSeat:frames[0].FirstSeat);
   Check(Enumerable.Range(0,2).All(seat=>rebuilt.Players[seat].Hand.Select(h=>h.CardId).SequenceEqual(frames[0].Players[seat].Hand.Select(h=>h.CardId))),"Recorded metadata reconstructs both opening hands");
   File.WriteAllLines(path+".old",new[]{"{\"Schema\":1,\"Mode\":\"local-test\",\"Session\":\"legacy\",\"TestData\":true}",JsonData.Serializer().Serialize(frames[0])});
   Check(TestReplay.Load(path+".old",out header).Count==1&&header.Schema==1&&!header.Seed.HasValue,"Old replay remains readable");
   Check(frames.Count==4&&frames.Select(f=>f.Index).SequenceEqual(new[]{0,1,2,3}),"Only successful operations append frames");
   Check(frames[0].Players[0].Hand.Any(h=>h.Id==id)&&frames[1].Action.SelectedHandIds.SequenceEqual(new[]{id}),"Replay preserves the pre-action hand and mulligan action");
   Check(frames[2].LegalMoves.Any(m=>m.Kind=="end")&&frames[3].Active==1&&frames[3].Action.Seat==0,"Legal moves and turn transition survive round-trip");
   using(var reader=new StreamReader(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)))Check(!reader.ReadToEnd().Contains("DeckPreview"),"Replay omits deck order");
   ShowPage("lobby");
   strictTest.IsChecked=true;
   Render(Path.Combine(folder,"strict-test-minimal.png"));
   StartConfiguredMatch();
   Check(match.Mode==GameModes.StrictTest&&!match.TestMode,"Strict test uses its own mode with no test tools");
   using(var strictRecording=new TestReplay(path+".strict",match,AppRelease.Version,first,second)){
    Check(match.Mulligan(0,new int[0]).Success&&match.Mulligan(1,new int[0]).Success,"Strict test completes mulligan");
    Check(match.Players[0].Mana==1&&match.Players[1].Mana==0,"Strict test starts with one mana for the first player");
    Check(!match.TestTakeCard(0,"CS2_231").Success,"Strict test rejects direct take calls");
    Check(match.EndTurn().Success&&match.Players[1].Mana==1,"Second player starts on one mana");
    Check(match.EndTurn().Success&&match.Players[0].Mana==2,"First player advances to two mana");
   }
   Check(TestReplay.Load(path+".strict").Count==1&&File.ReadLines(path+".strict").First().Contains("\"strict-test\""),"Strict replay is loadable and labeled");
   OpenLan();
   Check(testReplay==null&&!recordNextTest.IsVisible,"Leaving test closes recording and hides its entry");
  }finally{
   CloseTestReplay();
   File.Delete(path);
   File.Delete(path+".strict");
   File.Delete(path+".old");
  }
 }
 static DeckDocument ReadReplayDeck(ReplayDeck replay){
  var deck=new DeckDocument{ClassId=replay.ClassId,PoolId=replay.PoolId};
  foreach(var group in replay.Cards.GroupBy(id=>id))deck.Cards.Add(group.Key,group.Count());
  return deck;
 }
}
}


