using System;using System.Linq;using System.Collections.Generic;using System.IO;
namespace Touchfish {
public sealed class SealedOpenedCard {public string Id,Quality;}
// Reconstructs published long-run means and hard pity limits, not Blizzard's undisclosed joint sampler.
public sealed class SealedPackGenerator {
 const int SpellWeight=5,OtherCardWeight=4;
 public const int CardsPerPack=5;
 public const string Source="https://hs.blizzard.cn/news/hs-shop.html";
 static readonly double[] rates={.054738,.006089,.217663,.015463,.076123,.087132};
 static readonly int[] limits={40,361,10,142,29,25};
 static readonly string[] rarities={"LEGENDARY","LEGENDARY","EPIC","EPIC","RARE","COMMON"};
 readonly Random random;readonly CardRecord[] eligible;readonly int[] gaps=new int[6];readonly double[] hazards;
 readonly Dictionary<string,int> owned=new Dictionary<string,int>();bool firstLegend=true;
 public SealedPackGenerator(IEnumerable<CardRecord> cards,int seed){eligible=cards.Where(c=>new[]{"COMMON","RARE","EPIC","LEGENDARY"}.Contains(c.Rarity)).ToArray();foreach(string rarity in rarities.Distinct())if(!eligible.Any(c=>c.Rarity==rarity))throw new InvalidDataException("现开卡池缺少稀有度："+rarity);random=new Random(seed);hazards=rates.Select((p,i)=>Hazard(p,limits[i])).ToArray();}
 static double Hazard(double rate,int limit){double low=0,high=1;for(int n=0;n<64;n++){double q=(low+high)/2,mean=q/(1-Math.Pow(1-q,limit));if(mean<rate)low=q;else high=q;}return (low+high)/2;}
 string Draw(string rarity){var pool=eligible.Where(c=>c.Rarity==rarity).ToArray();int cap=rarity=="LEGENDARY"?1:2;var protectedPool=pool.Where(c=>!owned.ContainsKey(c.Id)||owned[c.Id]<cap).ToArray();if(protectedPool.Length>0)pool=protectedPool;int roll=random.Next(pool.Sum(card=>CardWeight(card)));
  int index=0;
  for(;index<pool.Length-1;index++){
   int weight=CardWeight(pool[index]);
   if(roll<weight)break;
   roll-=weight;
  }
  string id=pool[index].Id;int count;owned.TryGetValue(id,out count);owned[id]=count+1;return id;}
 static int CardWeight(CardRecord card){return card.Type=="SPELL"?SpellWeight:OtherCardWeight;}
 public SealedOpenedCard[] Open(){var result=new List<SealedOpenedCard>();bool rare=false;var hit=new bool[6];var due=new bool[6];for(int i=0;i<6;i++){gaps[i]++;int limit=i==0&&firstLegend?10:limits[i];double roll=random.NextDouble();due[i]=gaps[i]>=limit;hit[i]=due[i]||roll<hazards[i];}
  // Reserve hard-pity results before optional rolls. An early result prevents six deadlines coinciding.
  if(hit.All(h=>h)){int defer=Enumerable.Range(0,6).LastOrDefault(i=>!due[i]);if(due.All(d=>d))throw new InvalidOperationException("卡包保底排程异常。");hit[defer]=false;}
  if(!hit.Any(h=>h)&&Enumerable.Range(0,6).All(i=>gaps[i]>=((i==0&&firstLegend?10:limits[i])-1)))hit[5]=true;
  for(int i=0;i<6;i++)if(hit[i]){result.Add(new SealedOpenedCard{Id=Draw(rarities[i]),Quality=i==0||i==2?"normal":"golden"});gaps[i]=0;if(i==0)firstLegend=false;if(rarities[i]!="COMMON")rare=true;}
  if(!rare)result.Add(new SealedOpenedCard{Id=Draw("RARE"),Quality="normal"});
  // Match the published mean of 3.5 normal Commons; extra Rare slots have no publicly specified joint distribution.
  double noHigher=1;for(int i=0;i<5;i++)noHigher*=1-rates[i];double extraRare=1-3.5/(5-rates.Sum()-noHigher);
  while(result.Count<CardsPerPack)result.Add(new SealedOpenedCard{Id=Draw(random.NextDouble()<extraRare?"RARE":"COMMON"),Quality="normal"});return result.ToArray();
 }
}
public sealed class SealedView {
 public string Session,ClassId,Phase;public string[] Classes;public int Revision,Round,Opened,OpponentOpened,Wins0,Wins1,SeriesWinner=-2;public bool Confirmed,OpponentConfirmed;
 public Dictionary<string,int> Pool,Deck;public SealedOpenedCard[] LastPack;
}
public sealed class SealedSession {
 public const int PackCount=22,DeckSize=30;
 public readonly string Session=Guid.NewGuid().ToString("N"),PoolId;readonly CardCatalog catalog;readonly Func<CardRecord,bool> supported;readonly Random random;
 readonly string[][] choices=new string[2][];readonly string[] classes=new string[2];readonly int[] revisions=new int[2],opened=new int[2];readonly bool[] confirmed=new bool[2];
 readonly Dictionary<string,int>[] pools={new Dictionary<string,int>(),new Dictionary<string,int>()},decks={new Dictionary<string,int>(),new Dictionary<string,int>()};readonly SealedPackGenerator[] generators=new SealedPackGenerator[2];readonly SealedOpenedCard[][] last={new SealedOpenedCard[0],new SealedOpenedCard[0]};
 public int Round=1,Wins0,Wins1,SeriesWinner=-2;public bool ResultRecorded{get;private set;}public bool Ready{get{return confirmed.All(c=>c);}}
 public SealedSession(CardCatalog source,int seed,Func<CardRecord,bool> supports){catalog=source;supported=supports;PoolId=catalog.Pools.First(p=>!p.Archived).Id;random=new Random(seed);for(int seat=0;seat<2;seat++)choices[seat]=catalog.Pool(PoolId).HeroDbfIds.Keys.OrderBy(c=>c).OrderBy(c=>random.Next()).Take(3).ToArray();}
 public SealedView View(int seat){return new SealedView{Session=Session,ClassId=classes[seat],Phase=classes[seat]==null?"class":opened[seat]<PackCount?"open":confirmed[seat]?"ready":"build",Classes=choices[seat].ToArray(),Revision=revisions[seat],Round=Round,Opened=opened[seat],OpponentOpened=opened[1-seat],Confirmed=confirmed[seat],OpponentConfirmed=confirmed[1-seat],Pool=new Dictionary<string,int>(pools[seat]),Deck=new Dictionary<string,int>(decks[seat]),LastPack=last[seat].Select(c=>new SealedOpenedCard{Id=c.Id,Quality=c.Quality}).ToArray(),Wins0=Wins0,Wins1=Wins1,SeriesWinner=SeriesWinner};}
 public void Act(int seat,string session,int revision,string kind,string cardId=null,int amount=0){if(seat<0||seat>1||session!=Session||revision!=revisions[seat])throw new InvalidOperationException("现开状态已更新，请重新操作。");if(SeriesWinner!=-2||confirmed[seat])throw new InvalidOperationException("当前已确认，等待对方。");
  if(kind=="class"){if(classes[seat]!=null||!choices[seat].Contains(cardId))throw new InvalidOperationException("职业选择无效。");classes[seat]=cardId;generators[seat]=new SealedPackGenerator(catalog.Deckable(catalog.Pool(PoolId)).Where(c=>(c.CardClass=="NEUTRAL"||c.CardClass==cardId)&&supported(c)),random.Next());}
  else if(kind=="open"||kind=="open-all"){if(classes[seat]==null||opened[seat]>=PackCount)throw new InvalidOperationException("当前不能开包。");do{last[seat]=generators[seat].Open();foreach(var card in last[seat]){int count;pools[seat].TryGetValue(card.Id,out count);pools[seat][card.Id]=count+1;}opened[seat]++;}while(kind=="open-all"&&opened[seat]<PackCount);}
  else if(kind=="edit"){int available,count;if(opened[seat]!=PackCount||cardId==null||!pools[seat].TryGetValue(cardId,out available)||amount!=1&&amount!=-1)throw new InvalidOperationException("现开组牌操作无效。");decks[seat].TryGetValue(cardId,out count);if(count+amount<0||count+amount>available||amount>0&&decks[seat].Values.Sum()>=DeckSize)throw new InvalidOperationException("不能超过开出的数量或30张卡组上限。");if(count+amount==0)decks[seat].Remove(cardId);else decks[seat][cardId]=count+amount;}
  else if(kind=="confirm"){var deck=Deck(seat);Validate(catalog,deck,true);confirmed[seat]=true;}
  else throw new InvalidOperationException("未知现开操作。");revisions[seat]++;
 }
 public DeckDocument Deck(int seat){if(opened[seat]!=PackCount||decks[seat].Values.Sum()!=DeckSize)throw new InvalidOperationException("请从开出的牌中组成30张卡组。");return new DeckDocument{PoolId=PoolId,ClassId=classes[seat],Name="现开卡组",Cards=new Dictionary<string,int>(decks[seat])};}
 public static void Validate(CardCatalog catalog,DeckDocument deck,bool complete){if(deck==null||deck.Cards==null||deck.Cards.Values.Any(n=>n<1)||deck.Count>30||complete&&deck.Count!=30)throw new InvalidDataException("现开卡组需要30张牌。");var pool=catalog.Pool(deck.PoolId);if(!pool.HeroDbfIds.ContainsKey(deck.ClassId))throw new InvalidDataException("现开职业无效。");var rules=new MatchRules(catalog);foreach(var pair in deck.Cards){var c=catalog.Card(pair.Key);if(!c.Collectible||!pool.Contains(c)||(c.Type!="MINION"&&c.Type!="SPELL"&&c.Type!="WEAPON")||c.CardClass!="NEUTRAL"&&c.CardClass!=deck.ClassId||!rules.Supports(c))throw new InvalidDataException("现开卡组包含无效卡牌。");}}
 public void RecordResult(int winner){if(!Ready||ResultRecorded)return;if(winner< -1||winner>1)throw new ArgumentException();ResultRecorded=true;if(winner==0)Wins0++;if(winner==1)Wins1++;if(Wins0>=2)SeriesWinner=0;else if(Wins1>=2)SeriesWinner=1;else if(Round>=3)SeriesWinner=Wins0==Wins1?-1:Wins0>Wins1?0:1;}
 public void BeginRebuild(){if(!ResultRecorded||SeriesWinner!=-2)throw new InvalidOperationException("不能开始下一局。");Round++;ResultRecorded=false;for(int seat=0;seat<2;seat++){confirmed[seat]=false;revisions[seat]++;}}
}
}
