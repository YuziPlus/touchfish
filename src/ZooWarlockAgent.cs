using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
public static class ZooWarlockDeck {
 // The same Classic list used by the Zoo sparring opponent, frozen card IDs.
 public static DeckDocument Create(CardCatalog catalog){var deck=DeckCode.Decode(catalog,"AAEDAf0GAoKhBMOhBA77lQTVlgTtlgSBoQSuoQSyoQS5oQS9oQTToQTuoQSdogShogSjogTDowQAAA==","classic-2014");deck.Name="经典动物园";return deck;}
}
public sealed class ZooWarlockAgent : IMatchAgent {
 public int[] Mulligan(AgentObservation v){
  bool one=v.Hand.Any(c=>c.Cost==1&&c.Type=="MINION");var kept=new HashSet<string>();
  return v.Hand.Where(c=>{bool keep=c.Cost==1&&c.Type=="MINION"||one&&c.Key=="NEW1_019"||one&&c.Key=="EX1_162";return !keep||!kept.Add(c.Key);}).Select(c=>c.Id).ToArray();
 }
 public AgentMove Choose(AgentObservation v){if(!v.CanAct)return null;return v.Moves.Select((m,n)=>new{Move=Position(v,m),Index=n}).OrderByDescending(x=>Score(v,x.Move)).ThenBy(x=>x.Move.Cost).ThenBy(x=>x.Index).First().Move;}
 static bool Face(AgentObservation v,AgentMove m){return m.Target!=null&&m.Target.Seat!=v.Seat&&m.Target.Hero;}
 static AgentUnit Target(AgentObservation v,AgentMove m){return m.Target==null||m.Target.Hero?null:(m.Target.Seat==v.Seat?v.Self:v.Enemy).Board.FirstOrDefault(u=>u.Id==m.Target.UnitId);}
 static int Ready(AgentObservation v,int target=0){return v.Moves.Where(m=>m.Kind=="attack"&&m.Target.Seat!=v.Seat&&m.Target.UnitId==target).GroupBy(m=>m.Source).Sum(g=>g.First().Damage);}
 static double Value(AgentUnit u){return 2+u.Attack*1.7+u.Health*0.6+(u.Shield?2:0)+(u.Key=="NEW1_021"&&!u.Silenced?55:0)+(u.SpellDamage>0?4:0);}
 static double DiscardCost(AgentObservation v,int source){var cards=v.Hand.Where(c=>c.Id!=source).ToArray();return cards.Length==0?0:cards.Average(c=>c.Key=="EX1_310"?14:c.Key=="EX1_308"?9:c.Key=="EX1_316"?7:3+c.Cost);}
 static double Score(AgentObservation v,AgentMove m){
  var t=Target(v,m);double hp=v.Enemy.Health+v.Enemy.Armor;double threat=v.Enemy.Board.Where(u=>!u.Frozen).Sum(u=>u.Attack*(u.Windfury?2:1))+(!v.Enemy.Frozen?v.Enemy.HeroAttack:0);bool danger=threat>=v.Self.Health+v.Self.Armor-3&&threat>0;
  if(m.Kind=="end")return 0;
  if(m.Kind=="attack"){
   if(Face(v,m))return v.Enemy.Immune?-100:m.Damage>=hp?1000:16+m.Damage;
   if(t==null)return -100;var a=v.Self.Board.FirstOrDefault(u=>u.Id==m.Source);bool kill=!t.Shield&&(m.Damage>=t.Health||a!=null&&a.Poison),die=a==null?t.Attack>=v.Self.Health:!a.Shield&&(t.Attack>=a.Health||t.Poison);
   if(t.Key=="NEW1_021"&&!t.Silenced)return 95+Math.Min(m.Damage,t.Health);
   return (kill?Value(t)+8:t.Shield?8:-3)+(danger?t.Attack*2:0)-(die?(a==null?80:Value(a)):0)+(kill&&!die?8:0)-Math.Max(0,m.Damage-t.Health)*0.6;
  }
  if(m.Kind=="power"){int burst=v.Enemy.ClassId=="HUNTER"?4:3;if(v.Self.DeckCount==0||v.Self.HandCount>=9||v.Self.Health<=threat+burst+2)return -100;return 12+Math.Max(0,5-v.Self.HandCount)*4;}
  if(m.Key=="GAME_COIN")return v.Self.Mana<10&&v.Hand.Any(h=>h.Cost==v.Self.Mana+1&&h.Type=="MINION")&&v.Self.Board.Length<6?42:-20;
  if(m.Key=="EX1_308"){
   if(Face(v,m))return v.Enemy.Immune?-100:hp<=4?1000:hp<=Ready(v)+4?160:v.Hand.Any(h=>h.Key=="EX1_310")?-20:hp<=10&&v.Self.HandCount<=2?30-DiscardCost(v,m.Source):-15;
   if(t==null||m.Target.Seat==v.Seat)return -100;
   if(t.Key=="NEW1_021"&&!t.Silenced&&Ready(v,t.Id)<t.Health&&Ready(v,t.Id)+4>=t.Health)return 100-DiscardCost(v,m.Source);
   return !t.Shield&&t.Health<=4?Value(t)+(danger?22:8)-DiscardCost(v,m.Source): -25;
  }
  if(m.Key=="EX1_316"){
   if(t==null||m.Target.Seat!=v.Seat||t.Frozen)return -100;var attacks=v.Moves.Where(a=>a.Kind=="attack"&&a.Source==t.Id).ToArray();if(attacks.Length==0)return -100;
   if(attacks.Any(a=>Face(v,a))&&!v.Enemy.Immune&&Ready(v)+4>=hp)return 180;
   if(v.Enemy.Board.Any(u=>u.Key=="NEW1_021"&&!u.Silenced&&attacks.Any(a=>a.Target.UnitId==u.Id)&&Ready(v,u.Id)<u.Health&&Ready(v,u.Id)+4>=u.Health))return 110-Value(t)*0.4;
   return v.Enemy.Board.Any(u=>!u.Shield&&attacks.Any(a=>a.Target.UnitId==u.Id)&&u.Health>t.Attack&&u.Health<=t.Attack+4&&u.Attack>=3)?36-Value(t)*0.5:-25;
  }
  if(m.Key=="CS2_203")return t!=null&&m.Target.Seat!=v.Seat&&!t.Silenced?(t.Key=="NEW1_021"?150:t.Taunt?75:t.Shield||t.SpellDamage>0?40:4):t==null?12:-100;
  if(m.Key=="CS2_188"||m.Key=="EX1_046"){
   if(t==null)return 22;if(m.Target.Seat!=v.Seat)return -100;bool ready=v.Moves.Any(a=>a.Kind=="attack"&&a.Source==t.Id);if(ready&&Ready(v)+2>=hp&&!v.Enemy.Immune&&v.Moves.Any(a=>a.Source==t.Id&&a.Kind=="attack"&&Face(v,a)))return 170;
   return 30+(ready?9:0)+(ready&&v.Enemy.Board.Any(u=>!u.Shield&&u.Health>t.Attack&&u.Health<=t.Attack+2)?20:0);
  }
  if(m.Key=="EX1_019")return t!=null&&m.Target.Seat==v.Seat?38+t.Health+(v.Moves.Any(a=>a.Kind=="attack"&&a.Source==t.Id)?7:0):t==null?25:-100;
  var card=v.Hand.First(h=>h.Id==m.Source);if(card.Type!="MINION")return -20;bool doom=v.Enemy.Board.Any(u=>u.Key=="NEW1_021"&&!u.Silenced);
  if(doom&&!card.Charge)return -15;
  if(m.Key=="EX1_310"){double expected=5+AdjacentWolves(v,m.Position);if(!v.Enemy.Immune&&!v.Enemy.Board.Any(u=>u.Taunt&&!u.Stealth)&&Ready(v)+expected>=hp)return 175;double score=57-Math.Min(2,Math.Max(0,v.Self.HandCount-1))*DiscardCost(v,m.Source);bool unload=v.Moves.Any(other=>other.Kind=="play"&&other.Source!=m.Source&&other.Key!="EX1_310"&&other.Key!="EX1_308"&&other.Cost<=v.Self.Mana-m.Cost&&Score(v,other)>0);return unload?Math.Min(27,score):score;}
  if(m.Key=="EX1_319"&&v.Self.Health<=threat+4)return -30;
  double overextend=v.Enemy.ClassId=="MAGE"&&v.Enemy.MaxMana>=5&&v.Self.Board.Length>=4?24:0;
  if(m.Key=="EX1_093")return (v.Self.Board.Length>=2?48:v.Self.Board.Length==1?32:10)+PositionBenefit(v,m);
  if(m.Key=="EX1_162")return (v.Self.Board.Length>=2?43:30)+PositionBenefit(v,m)-overextend;
  if(m.Key=="NEW1_019")return 42-overextend;
  return 31+card.Cost*2+(m.Key=="EX1_020"?4:0)+PositionBenefit(v,m)-overextend;
 }
 static int AdjacentWolves(AgentObservation v,int pos){return (pos>0&&v.Self.Board[pos-1].Key=="EX1_162"&&!v.Self.Board[pos-1].Silenced?1:0)+(pos<v.Self.Board.Length&&v.Self.Board[pos].Key=="EX1_162"&&!v.Self.Board[pos].Silenced?1:0);}
 static double PositionBenefit(AgentObservation v,AgentMove m){
  int pos=m.Position;double value=AdjacentWolves(v,pos)*2;
  if(m.Key=="EX1_093")foreach(var u in v.Self.Board.Where((u,n)=>n==pos-1||n==pos)){value+=2+u.Health*0.3+u.Attack*0.5+(u.Shield?2:0);}
  if(m.Key=="EX1_162")foreach(var u in v.Self.Board.Where((u,n)=>n==pos-1||n==pos)){value+=1+(v.Moves.Any(a=>a.Kind=="attack"&&a.Source==u.Id)?3:1);}
  return value;
 }
 static AgentMove Position(AgentObservation v,AgentMove m){
  if(m.Kind!="play"||v.Hand.First(c=>c.Id==m.Source).Type!="MINION")return m;
  var copy=new AgentMove{Kind=m.Kind,Key=m.Key,Source=m.Source,Cost=m.Cost,Target=m.Target,Choice=m.Choice};double best=Double.MinValue;
  for(int n=0;n<=v.Self.Board.Length;n++){copy.Position=n;double score=PositionBenefit(v,copy)+(v.Hand.First(c=>c.Id==m.Source).Charge?AdjacentWolves(v,n)*2:0);if(score>best){best=score;m=new AgentMove{Kind=copy.Kind,Key=copy.Key,Source=copy.Source,Cost=copy.Cost,Target=copy.Target,Choice=copy.Choice,Position=n};}}return m;
 }
}
public sealed class LocalAgentDefinition {
 public readonly string Id,Name,ClassId;public readonly Func<CardCatalog,DeckDocument> Deck;public readonly Func<IMatchAgent> Create;
 public LocalAgentDefinition(string id,string name,string classId,Func<CardCatalog,DeckDocument> deck,Func<IMatchAgent> create){Id=id;Name=name;ClassId=classId;Deck=deck;Create=create;}
}
public static class MatchAgents {
 static readonly List<LocalAgentDefinition> definitions=new List<LocalAgentDefinition>{
  new LocalAgentDefinition("freeze","经典冰法 AI","MAGE",FreezeMageDeck.Create,()=>new FreezeMageAgent()),
  new LocalAgentDefinition("zoo","经典动物园 AI","WARLOCK",ZooWarlockDeck.Create,()=>new ZooWarlockAgent()),
  new LocalAgentDefinition("miracle","经典奇迹贼 AI","ROGUE",MiracleRogueDeck.Create,()=>new MiracleRogueAgent()),
  new LocalAgentDefinition("secret-paladin","佛祖骑 AI","PALADIN",TournamentPaladinDecks.Secret,()=>new PaladinAgent(false)),
  new LocalAgentDefinition("murloc-paladin","铺场鱼人骑 AI","PALADIN",TournamentPaladinDecks.Murloc,()=>new PaladinAgent(true))
 };
 public static LocalAgentDefinition[] All{get{return definitions.ToArray();}}
 public static LocalAgentDefinition Find(string id){return definitions.FirstOrDefault(d=>d.Id==id);}
 public static void Register(LocalAgentDefinition definition){if(definition==null||String.IsNullOrWhiteSpace(definition.Id)||definition.Id=="test"||Find(definition.Id)!=null||String.IsNullOrWhiteSpace(definition.Name)||String.IsNullOrWhiteSpace(definition.ClassId)||definition.Deck==null||definition.Create==null)throw new ArgumentException("人机定义无效或重复。");definitions.Add(definition);}
}
}
