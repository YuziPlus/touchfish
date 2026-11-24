using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
public sealed class PlayerInformation {
 internal readonly CardCatalog Catalog;
 internal readonly MatchRules Rules;
 internal readonly Func<string,CardRecord> Card;
 public readonly int Seat;
 public readonly string PoolId;
 internal readonly string[] Classes;
 internal readonly int[] HandCounts,DeckCounts,SecretCounts;
 internal readonly HandCard[] OwnHand;
 internal readonly string[] OwnSecrets,KnownOwnTop;
 public string ClassId(int seat){return Classes[seat];}
 public int HandCount(int seat){return HandCounts[seat];}
 public int DeckCount(int seat){return DeckCounts[seat];}
 public int SecretCount(int seat){return SecretCounts[seat];}
 public HandCard[] KnownOwnHand(){return OwnHand.Select(card=>card.CopyForSimulation()).ToArray();}
 internal readonly PublicHistoryEvent[] Events;
 public PublicHistoryEvent[] History{get{return (PublicHistoryEvent[])Events.Clone();}}
 internal PlayerInformation(CardCatalog catalog,MatchRules rules,int seat,string pool,string[] classes,int[] hand,int[] deck,int[] secrets,HandCard[] ownHand,string[] ownSecrets,PublicHistoryEvent[] events,string[] knownOwnTop,Func<string,CardRecord> card){
  Catalog=catalog;Rules=rules;Seat=seat;PoolId=pool;Classes=classes;HandCounts=hand;DeckCounts=deck;SecretCounts=secrets;
  OwnHand=ownHand;OwnSecrets=ownSecrets;Events=events;KnownOwnTop=knownOwnTop;
  Card=card;
 }
}

public interface IDeckPrior {
 DeckDocument Draw(DeckPriorContext context,Func<int,int> randomIndex);
}
public sealed class DeckPriorContext {
 public readonly string ClassId,PoolId;
 public readonly int Size;
 readonly Dictionary<string,int> required,limits;
 public Dictionary<string,int> Required(){return new Dictionary<string,int>(required);}
 public Dictionary<string,int> Limits(){return new Dictionary<string,int>(limits);}
 internal DeckPriorContext(string classId,string pool,int size,Dictionary<string,int> required,Dictionary<string,int> limits){
  ClassId=classId;PoolId=pool;Size=size;this.required=required;this.limits=limits;
 }
}
public sealed class LegalDeckPrior : IDeckPrior {
 readonly DeckDocument[] archetypes;
 readonly int[] weights;
 readonly int fallbackWeight;
 public LegalDeckPrior():this(new DeckDocument[0],new int[0],1){}
 public LegalDeckPrior(DeckDocument[] archetypes,int[] weights,int fallbackWeight){
  if(archetypes==null||weights==null||archetypes.Length!=weights.Length||weights.Any(w=>w<=0)||fallbackWeight<0)
   throw new ArgumentException("Invalid deck prior weights.");
  this.archetypes=archetypes.Select(d=>d.Copy()).ToArray();
  this.weights=(int[])weights.Clone();
  this.fallbackWeight=fallbackWeight;
 }
 public DeckDocument Draw(DeckPriorContext context,Func<int,int> randomIndex){
  var limits=context.Limits();
  var required=context.Required();
  var compatible=Enumerable.Range(0,archetypes.Length).Where(i=>{
   var d=archetypes[i];
   return d.ClassId==context.ClassId&&d.PoolId==context.PoolId&&d.Count==context.Size&&
    d.Cards.All(p=>limits.ContainsKey(p.Key)&&p.Value>0&&p.Value<=limits[p.Key])&&
    required.All(p=>d.Cards.ContainsKey(p.Key)&&d.Cards[p.Key]>=p.Value);
  }).ToArray();
  int total=checked(fallbackWeight+compatible.Sum(i=>weights[i]));
  if(total==0)return null;
  int roll=randomIndex(total);
  foreach(int i in compatible){if(roll<weights[i])return archetypes[i].Copy();roll-=weights[i];}
  var result=new DeckDocument{ClassId=context.ClassId,PoolId=context.PoolId};
  var available=new List<string>();
  foreach(var pair in limits.OrderBy(p=>p.Key,StringComparer.Ordinal)){
   int need;
   required.TryGetValue(pair.Key,out need);
   if(need>pair.Value)return null;
   if(need>0)result.Cards.Add(pair.Key,need);
   for(int n=need;n<pair.Value;n++)available.Add(pair.Key);
  }
  while(result.Count<context.Size&&available.Count>0){
   int i=randomIndex(available.Count);
   string card=available[i];
   available[i]=available[available.Count-1];
   available.RemoveAt(available.Count-1);
   int n;result.Cards.TryGetValue(card,out n);result.Cards[card]=n+1;
  }
  return result.Count==context.Size?result:null;
 }
}

public sealed class DeckBelief {
 internal readonly PlayerInformation Information;
 internal readonly DeckDocument[] Known;
 internal readonly DeckPriorContext PriorContext;
 internal readonly IDeckPrior Prior;
 internal readonly List<BeliefLedger>[] Fixed;
 public readonly string Diagnostic;
 public bool Ready{get{return Diagnostic==null;}}
 public bool EnemyDeckKnown{get{return Known[1-Information.Seat]!=null;}}
 internal DeckBelief(PlayerInformation information,DeckDocument[] known,DeckPriorContext context,IDeckPrior prior,List<BeliefLedger>[] fixedLedgers,string diagnostic){
  Information=information;Known=known;PriorContext=context;Prior=prior;Fixed=fixedLedgers;Diagnostic=diagnostic;
 }

 public static DeckBelief Create(PlayerInformation information,DeckDocument own,DeckDocument opponent=null,IDeckPrior prior=null){
  if(information==null||own==null)throw new ArgumentNullException("information/own");
  var known=new DeckDocument[2];
  int seat=information.Seat;
  known[seat]=own.Copy();known[1-seat]=opponent==null?null:opponent.Copy();
  var fixedLedgers=new List<BeliefLedger>[2];
  DeckPriorContext context=null;
  string error=null;
  try{
   var unsupported=information.Events.FirstOrDefault(e=>e.Kind==HistoryKind.Unmodeled);
   if(unsupported!=null)
    throw new InvalidOperationException("公开历史包含暂未建模的隐藏状态变更。 "+unsupported.Origin);
   for(int player=0;player<2;player++){
    var deck=known[player];
    if(deck!=null){
     Validate(information,player,deck);
     fixedLedgers[player]=BeliefLedger.Replay(information,player,deck);
     if(fixedLedgers[player].Count==0)throw new InvalidOperationException("已知牌组与观察历史不相容。");
    }else{
     var pool=information.Catalog.Pool(information.PoolId);
     var limits=information.Catalog.Deckable(pool).Where(c=>information.Rules.Supports(c)&&(c.CardClass=="NEUTRAL"||c.CardClass==information.Classes[player]))
      .ToDictionary(c=>c.Id,c=>c.Rarity=="LEGENDARY"?pool.LegendaryLimit:pool.CopyLimit);
     var required=information.Events.Where(e=>e.Seat==player&&e.Kind==HistoryKind.RevealSecret).GroupBy(e=>e.CardId)
      .ToDictionary(g=>g.Key,g=>g.Count());
     // Visible sources can account for copies. The remainder is only a lower bound.
     foreach(var group in information.Events.Where(e=>e.Seat==player&&e.CardId!=null).GroupBy(e=>e.CardId)){
      int need=group.Where(e=>e.Kind==HistoryKind.Played||e.Kind==HistoryKind.Discard||e.Kind==HistoryKind.Burn||e.Kind==HistoryKind.Destroy||e.Kind==HistoryKind.RevealSecret||e.Kind==HistoryKind.SummonFromHand).Sum(e=>e.Count)
       -group.Where(e=>e.Kind==HistoryKind.Gain||e.Kind==HistoryKind.Return||e.Kind==HistoryKind.ShuffleIn).Sum(e=>e.Count);
      if(need>0)required[group.Key]=need;
     }
     if(required.Any(p=>!limits.ContainsKey(p.Key)||p.Value>limits[p.Key]))
      throw new InvalidOperationException("公开牌与当前构筑约束不相容。");
     context=new DeckPriorContext(information.Classes[player],information.PoolId,pool.DeckSize,required,limits);
    }
   }
  }catch(InvalidOperationException ex){error=ex.Message;}
   catch(System.IO.InvalidDataException){error="初始牌组不符合当前卡池。";}
  return new DeckBelief(information,known,context,prior??new LegalDeckPrior(),fixedLedgers,error);
 }

 internal static void Validate(PlayerInformation info,int seat,DeckDocument deck){
  if(deck.ClassId!=info.Classes[seat]||deck.PoolId!=info.PoolId)throw new InvalidOperationException("牌组职业或卡池不匹配。");
  DeckRules.Validate(info.Catalog,deck,true);
  if(deck.Cards.Keys.Any(id=>!info.Rules.Supports(info.Catalog.Card(id))))throw new InvalidOperationException("牌组包含未支持效果。");
 }
}

internal sealed class BeliefLedger {
 internal readonly Dictionary<string,int> Bag;
 internal readonly Dictionary<string,int> ForcedDeck=new Dictionary<string,int>();
 internal readonly List<HandCard> KnownHand=new List<HandCard>();
 internal readonly List<string> HandCostProfiles=new List<string>();
 internal readonly List<HashSet<string>> SecretSlots=new List<HashSet<string>>();
 internal int Hand,Deck;
 int privateGains;

 BeliefLedger(DeckDocument deck){Bag=new Dictionary<string,int>(deck.Cards);Deck=deck.Count;}
 BeliefLedger(BeliefLedger source){
  Bag=new Dictionary<string,int>(source.Bag);ForcedDeck=new Dictionary<string,int>(source.ForcedDeck);KnownHand.AddRange(source.KnownHand);
  foreach(var slot in source.SecretSlots)SecretSlots.Add(new HashSet<string>(slot,StringComparer.Ordinal));
  Hand=source.Hand;Deck=source.Deck;privateGains=source.privateGains;
  HandCostProfiles.AddRange(source.HandCostProfiles);
 }
 internal static bool Remove(Dictionary<string,int> bag,string id){
  int n;if(id==null||!bag.TryGetValue(id,out n)||n==0)return false;
  if(n==1)bag.Remove(id);else bag[id]=n-1;
  return true;
 }

 bool RemoveUnforced(string id){
  int total,forced;
  ForcedDeck.TryGetValue(id,out forced);
  return Bag.TryGetValue(id,out total)&&total>forced&&Remove(Bag,id);
 }

 internal static List<BeliefLedger> Replay(PlayerInformation info,int seat,DeckDocument deck){
  var states=new List<BeliefLedger>{new BeliefLedger(deck)};
  var secrets=deck.Cards.Keys.Where(id=>MatchEngine.Has(info.Catalog.Card(id),"SECRET")).ToArray();
  foreach(var e in info.Events){
   if(e.Seat!=seat)continue;
   states=states.SelectMany(state=>state.Apply(e,secrets,info)).ToList();
   if(states.Count>256)throw new InvalidOperationException("公开历史的来源分支超过当前推断上限。");
   if(states.Count==0)break;
  }
  return states.Where(s=>s.privateGains==0&&s.Hand==info.HandCounts[seat]&&s.Deck==info.DeckCounts[seat]&&s.SecretSlots.Count==info.SecretCounts[seat]&&
   s.Bag.Values.Sum()+s.KnownHand.Count==s.Hand+s.Deck+s.SecretSlots.Count).ToList();
 }

 internal static int ProfileCost(string profile,int baseCost){
  if(profile=="0")return 0;
  var values=profile.Split(',');
  int value=int.Parse(values[0],System.Globalization.CultureInfo.InvariantCulture);
  for(int i=1;i<values.Length;i++)value=MatchEngine.ShiftCardCost(baseCost,value,int.Parse(values[i],System.Globalization.CultureInfo.InvariantCulture));
  return value;
 }

 IEnumerable<BeliefLedger> Apply(PublicHistoryEvent e,string[] secrets,PlayerInformation info){
  switch(e.Kind){
   case HistoryKind.Draw:
    // A bomb never enters hand. A different inserted card needs a draw-origin branch.
    if(ForcedDeck.Keys.Any(id=>id!="GVG_056t"))yield break;
    Deck-=e.Count;Hand+=e.Count;
    HandCostProfiles.AddRange(Enumerable.Repeat(e.Adjustment.ToString(System.Globalization.CultureInfo.InvariantCulture),e.Count));
    break;
   case HistoryKind.HandCostFloorShift:
    for(int i=0;i<HandCostProfiles.Count;i++)HandCostProfiles[i]+=","+e.Adjustment.ToString(System.Globalization.CultureInfo.InvariantCulture);
    for(int i=0;i<KnownHand.Count;i++){
     var card=KnownHand[i].CopyForSimulation();
     card.CostAdjustment=MatchEngine.ShiftCardCost(info.Card(card.CardId).Cost,card.CostAdjustment,e.Adjustment);
     KnownHand[i]=card;
    }
    break;
   case HistoryKind.Mulligan:break;
   case HistoryKind.Gain:
    if(e.CardId==null){Hand+=e.Count;privateGains+=e.Count;break;}
    if(e.VisibleTo>=0)privateGains-=e.Count;else Hand+=e.Count;
    for(int i=0;i<e.Count;i++)KnownHand.Add(new HandCard{CardId=e.CardId,Generated=true,CostAdjustment=e.Adjustment});
    break;
   case HistoryKind.Return:
    Hand++;KnownHand.Add(new HandCard{CardId=e.CardId,CostAdjustment=e.Adjustment});break;
   case HistoryKind.Played:
   case HistoryKind.Discard:
   case HistoryKind.SummonFromHand:
    Hand--;
    var matching=KnownHand.Select((h,i)=>new{h,i}).Where(x=>x.h.CardId==e.CardId).Select(x=>x.i).Distinct().ToArray();
    foreach(int i in matching){
     var branch=new BeliefLedger(this);
     branch.KnownHand.RemoveAt(i);
     if(branch.Hand>=branch.KnownHand.Count)yield return branch;
    }
    if(Hand<KnownHand.Count||e.CardId!=null&&!RemoveUnforced(e.CardId))yield break;
    foreach(int i in HandCostProfiles.Select((cost,i)=>i).GroupBy(i=>HandCostProfiles[i]).Select(group=>group.First())){
     var branch=new BeliefLedger(this);
     branch.HandCostProfiles.RemoveAt(i);
     yield return branch;
    }
    yield break;
   case HistoryKind.Burn:
   case HistoryKind.Destroy:
    Deck--;
    if(ForcedDeck.ContainsKey(e.CardId)){
     var branch=new BeliefLedger(this);
     Remove(branch.ForcedDeck,e.CardId);
     if(Remove(branch.Bag,e.CardId)&&branch.Deck>=branch.ForcedDeck.Values.Sum())yield return branch;
    }
    if(!RemoveUnforced(e.CardId)||Deck<ForcedDeck.Values.Sum())yield break;
    break;
   case HistoryKind.ShuffleIn:
    Deck++;
    int existing;Bag.TryGetValue(e.CardId,out existing);Bag[e.CardId]=existing+1;
    ForcedDeck.TryGetValue(e.CardId,out existing);ForcedDeck[e.CardId]=existing+1;
    break;
   case HistoryKind.HandGrowth:
    if(Bag.ContainsKey(e.CardId)||KnownHand.Any(h=>h.CardId==e.CardId))yield break;
    break;
   case HistoryKind.ArmSecret:
    // An observed generated secret needs a hand-origin and timing model.
    if(KnownHand.Any(h=>secrets.Contains(h.CardId)||h.Generated&&h.CardId!="GAME_COIN"))yield break;
    SecretSlots.Add(new HashSet<string>(secrets,StringComparer.Ordinal));break;
   case HistoryKind.ArmSecretFromDeck:
    Deck--;
    SecretSlots.Add(new HashSet<string>(secrets,StringComparer.Ordinal));break;
   case HistoryKind.RevealSecret:
    // The revealed identity is public, but which earlier anonymous arm supplied it is private.
    for(int i=0;i<SecretSlots.Count;i++){
     if(!SecretSlots[i].Contains(e.CardId))continue;
     var branch=new BeliefLedger(this);
     branch.SecretSlots.RemoveAt(i);
     if(!branch.RemoveUnforced(e.CardId))continue;
     // The other armed secrets coexisted with this one and cannot share its identity.
     foreach(var slot in branch.SecretSlots)slot.Remove(e.CardId);
     if(branch.CanAssignSecrets(0,new HashSet<string>(StringComparer.Ordinal)))yield return branch;
    }
    yield break;
   case HistoryKind.NoSecretTrigger:
    foreach(var slot in SecretSlots)slot.RemoveWhere(id=>id==e.CardId||id.EndsWith("_"+e.CardId,StringComparison.Ordinal));
    break;
  }
  if(Deck>=0&&Hand>=KnownHand.Count&&privateGains>=0)yield return this;
 }

 bool CanAssignSecrets(int index,HashSet<string> assigned){
  if(index==SecretSlots.Count)return true;
  foreach(string id in SecretSlots[index]){
   int available,forced;
   ForcedDeck.TryGetValue(id,out forced);
   if(!Bag.TryGetValue(id,out available)||available<=forced||!assigned.Add(id))continue;
   if(CanAssignSecrets(index+1,assigned))return true;
   assigned.Remove(id);
  }
  return false;
 }
}

public sealed class SampleResult {
 internal readonly PlayerInformation Information;
 internal readonly string[][] Decks,Secrets;
 internal readonly HandCard[] EnemyCards;
 internal readonly int SimulationSeed;
 readonly DeckDocument[] initialDecks;
 public readonly string Diagnostic;
 public bool Success{get{return Diagnostic==null;}}
 public DeckDocument InitialDeck(int seat){
  var deck=initialDecks[seat].Copy();
  deck.Id="sampled-"+seat;
  deck.Name="假想初始牌组";
  return deck;
 }
 public string[] Deck(int seat){return (string[])Decks[seat].Clone();}
 public string[] SecretCards(int seat){return (string[])Secrets[seat].Clone();}
 public HandCard[] OpponentHand(){return EnemyCards.Select(h=>h.CopyForSimulation()).ToArray();}
 internal SampleResult(PlayerInformation info,string diagnostic){Information=info;Diagnostic=diagnostic;}
 internal SampleResult(PlayerInformation info,string[][] decks,string[][] secrets,HandCard[] hand,int seed,DeckDocument[] initialDecks){
  Information=info;Decks=decks;Secrets=secrets;EnemyCards=hand;SimulationSeed=seed;this.initialDecks=initialDecks;
 }
}

public interface IBeliefSampler {
 SampleResult Sample(DeckBelief belief,int seed);
}

public sealed class BeliefSampler : IBeliefSampler {
 public SampleResult Sample(DeckBelief belief,int seed){
  if(belief==null)throw new ArgumentNullException("belief");
  var info=belief.Information;
  if(!belief.Ready)return new SampleResult(info,belief.Diagnostic);
  var random=new SimulationRandom(seed);
  var decks=new string[2][];
  var secrets=new string[2][];
  var initialDecks=new DeckDocument[2];
  HandCard[] hand=null;
  for(int seat=0;seat<2;seat++){
   bool own=seat==info.Seat;
   var fixedStates=belief.Fixed[seat];
   bool found=false;
   for(int attempt=0;attempt<(fixedStates==null?64:1)&&!found;attempt++){
    var states=fixedStates;
    var initial=belief.Known[seat];
    if(states==null){
     var deck=belief.Prior.Draw(belief.PriorContext,random.Next);
     if(deck==null)break;
     initial=deck;
     try{DeckBelief.Validate(info,seat,deck);states=BeliefLedger.Replay(info,seat,deck);}
     catch(InvalidOperationException){continue;}
     catch(System.IO.InvalidDataException){continue;}
    }
    int start=states.Count==0?0:random.Next(states.Count);
    for(int i=0;i<states.Count&&!found;i++){
     var state=states[(start+i)%states.Count];
     found=Fill(info,seat,state,random,out decks[seat],out secrets[seat],ref hand);
     if(found)initialDecks[seat]=fixedStates==null?initial.Copy():initial;
    }
   }
   if(!found)return new SampleResult(info,own?"己方合法观察不足以还原剩余牌库。":"先验及公开历史不足以生成一致的对手隐藏状态。");
  }
  return new SampleResult(info,decks,secrets,hand,random.Next(int.MaxValue),initialDecks);
 }

 static bool Fill(PlayerInformation info,int seat,BeliefLedger state,SimulationRandom random,out string[] deck,out string[] secrets,ref HandCard[] enemyHand){
  deck=null;secrets=new string[state.SecretSlots.Count];
  var bag=new Dictionary<string,int>(state.Bag);
  var forced=new Dictionary<string,int>(state.ForcedDeck);
  foreach(var pair in forced)
   for(int i=0;i<pair.Value;i++)if(!BeliefLedger.Remove(bag,pair.Key))return false;
  bool own=seat==info.Seat;
  if(own){
   Array.Copy(info.OwnSecrets,secrets,secrets.Length);
   for(int i=0;i<secrets.Length;i++)if(!state.SecretSlots[i].Contains(secrets[i])||!BeliefLedger.Remove(bag,secrets[i]))return false;
   var known=state.KnownHand.ToList();
   var anonymous=new List<HandCard>();
   foreach(var card in info.OwnHand){
    int index=known.FindIndex(h=>h.CardId==card.CardId&&h.CostAdjustment==card.CostAdjustment);
    if(index>=0)known.RemoveAt(index);
    else{if(!BeliefLedger.Remove(bag,card.CardId))return false;anonymous.Add(card);}
   }
   if(known.Count!=0||anonymous.Count!=state.HandCostProfiles.Count||!MatchCosts(info,anonymous,state.HandCostProfiles))return false;
   foreach(string id in info.KnownOwnTop)if(!BeliefLedger.Remove(forced,id)&&!BeliefLedger.Remove(bag,id))return false;
  }else if(!AssignSecrets(state,0,bag,secrets,random))return false;
  var remaining=bag.OrderBy(p=>p.Key,StringComparer.Ordinal).SelectMany(p=>Enumerable.Repeat(p.Key,p.Value)).ToArray();
  for(int i=remaining.Length-1;i>0;i--){int j=random.Next(i+1);string card=remaining[i];remaining[i]=remaining[j];remaining[j]=card;}
  int unknown=own?0:state.Hand-state.KnownHand.Count;
  if(remaining.Length+forced.Values.Sum()+(own?info.KnownOwnTop.Length:0)!=state.Deck+unknown)return false;
  if(!own){
   if(unknown!=state.HandCostProfiles.Count)return false;
   var cards=state.KnownHand.Select(h=>h.CopyForSimulation()).Concat(remaining.Take(unknown).Select((id,i)=>new HandCard{
    CardId=id,CostAdjustment=BeliefLedger.ProfileCost(state.HandCostProfiles[i],info.Card(id).Cost)})).ToArray();
   for(int i=cards.Length-1;i>0;i--){int j=random.Next(i+1);var card=cards[i];cards[i]=cards[j];cards[j]=card;}
   enemyHand=cards;
  }
  var shuffled=remaining.Skip(unknown).ToList();
  foreach(var pair in forced.OrderBy(p=>p.Key,StringComparer.Ordinal))
   for(int i=0;i<pair.Value;i++)shuffled.Insert(random.Next(shuffled.Count+1),pair.Key);
  deck=own?info.KnownOwnTop.Concat(shuffled).ToArray():shuffled.ToArray();
  return true;
 }

 static bool MatchCosts(PlayerInformation info,List<HandCard> cards,List<string> profiles){
  var owner=Enumerable.Repeat(-1,profiles.Count).ToArray();
  for(int i=0;i<cards.Count;i++)
   if(!AssignCost(i,cards,profiles,info,owner,new bool[profiles.Count]))return false;
  return true;
 }

 static bool AssignCost(int card,List<HandCard> cards,List<string> profiles,PlayerInformation info,int[] owner,bool[] seen){
  int cost=info.Card(cards[card].CardId).Cost;
  for(int slot=0;slot<profiles.Count;slot++){
   if(seen[slot]||BeliefLedger.ProfileCost(profiles[slot],cost)!=cards[card].CostAdjustment)continue;
   seen[slot]=true;
   if(owner[slot]<0||AssignCost(owner[slot],cards,profiles,info,owner,seen)){owner[slot]=card;return true;}
  }
  return false;
 }

 static bool AssignSecrets(BeliefLedger state,int index,Dictionary<string,int> bag,string[] slots,SimulationRandom random){
  if(index==slots.Length)return true;
  var candidates=new List<KeyValuePair<string,int>>();
  foreach(string id in SecretCandidates(state,index,bag,slots)){
   int count=bag[id];
   BeliefLedger.Remove(bag,id);
   slots[index]=id;
   int weight=count*CountSecretAssignments(state,index+1,bag,slots);
   bag[id]=count;
   if(weight>0)candidates.Add(new KeyValuePair<string,int>(id,weight));
  }
  if(candidates.Count==0)return false;
  int roll=random.Next(candidates.Sum(entry=>entry.Value));
  int choice=0;
  while(roll>=candidates[choice].Value)roll-=candidates[choice++].Value;
  slots[index]=candidates[choice].Key;
  BeliefLedger.Remove(bag,slots[index]);
  return AssignSecrets(state,index+1,bag,slots,random);
 }

 static int CountSecretAssignments(BeliefLedger state,int index,Dictionary<string,int> bag,string[] slots){
  if(index==slots.Length)return 1;
  int total=0;
  foreach(string id in SecretCandidates(state,index,bag,slots)){
   int count=bag[id];
   BeliefLedger.Remove(bag,id);
   slots[index]=id;
   total+=count*CountSecretAssignments(state,index+1,bag,slots);
   bag[id]=count;
  }
  return total;
 }

 static string[] SecretCandidates(BeliefLedger state,int index,Dictionary<string,int> bag,string[] slots){
  return state.SecretSlots[index].Where(id=>bag.ContainsKey(id)&&!slots.Take(index).Contains(id))
   .OrderBy(id=>id,StringComparer.Ordinal).ToArray();
 }
}
}
