using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Touchfish {
public sealed class CardRecord {
 public string EffectId;public string Id,BaseId,Name,Text,RawText,CardClass,Type,Rarity,SetId,Race;
 public int DbfId,LegacyDbfId,Cost,Attack,Health,Durability,Overload,SpellDamage;
 public bool Collectible,FriendlySecretsOnly;public string[] Mechanics,Entourage;
 internal CardRecord CopyForSimulation(){
  var copy=(CardRecord)MemberwiseClone();
  copy.Mechanics=Mechanics==null?null:(string[])Mechanics.Clone();
  copy.Entourage=Entourage==null?null:(string[])Entourage.Clone();
  return copy;
 }
}
public sealed class CardSetPackage {public int SchemaVersion;public string Id,Name,Version,DisplayName;public bool HiddenInBrowser;public CardRecord[] Cards;}
public sealed class CardSetInfo {public string Id,Name,Version;public bool HiddenInBrowser;}
public sealed class PoolDefinition {
 public string Id,Name;public string[] CardSetIds;public int DeckSize,CopyLimit,LegendaryLimit,DeckstringFormat;
 public Dictionary<string,int> HeroDbfIds;
 public Dictionary<string,string> CardReplacements;
 public bool Archived;
 internal PoolDefinition CopyForSimulation(){
  var copy=(PoolDefinition)MemberwiseClone();
  copy.CardSetIds=(string[])CardSetIds.Clone();
  copy.HeroDbfIds=new Dictionary<string,int>(HeroDbfIds,HeroDbfIds.Comparer);
  copy.CardReplacements=CardReplacements==null?null:new Dictionary<string,string>(CardReplacements,CardReplacements.Comparer);
  return copy;
 }
 public bool Contains(CardRecord card){return CardSetIds.Contains(card.SetId)&&(CardReplacements==null||!CardReplacements.ContainsKey(card.Id));}
}
public sealed class PoolFile {public int SchemaVersion;public PoolDefinition[] Pools;}
public interface ICardSetSource {IEnumerable<CardSetPackage> Load();}
public sealed class JsonDirectoryCardSetSource : ICardSetSource {
 readonly string directory;public JsonDirectoryCardSetSource(string path){directory=path;}
 public IEnumerable<CardSetPackage> Load(){foreach(var path in Directory.GetFiles(directory,"*.json").OrderBy(p=>p,StringComparer.Ordinal)){yield return JsonData.Read<CardSetPackage>(path);}}
}
public static class JsonData {
 public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=16*1024*1024,RecursionLimit=100};}
 public static T Read<T>(string path){return Serializer().Deserialize<T>(File.ReadAllText(path));}
 public static void Write(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,Serializer().Serialize(value),new System.Text.UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
}
public sealed class CardCatalog {
 public readonly Dictionary<string,CardRecord> Cards=new Dictionary<string,CardRecord>();
 public readonly Dictionary<int,CardRecord> ByDbf=new Dictionary<int,CardRecord>();
 public readonly List<CardSetInfo> Sets=new List<CardSetInfo>();
 public readonly List<PoolDefinition> Pools=new List<PoolDefinition>();
 public CardCatalog(ICardSetSource source,PoolFile poolFile){
  if(poolFile.SchemaVersion!=1||poolFile.Pools==null)throw new InvalidDataException("不支持的卡池配置版本。");
  var setIds=new HashSet<string>();
  foreach(var package in source.Load()){
   if(package.SchemaVersion!=1||package.Cards==null||!setIds.Add(package.Id))throw new InvalidDataException("牌库版本或 ID 无效。");
   Sets.Add(new CardSetInfo{Id=package.Id,Name=String.IsNullOrWhiteSpace(package.DisplayName)?(String.IsNullOrWhiteSpace(package.Name)?package.Id:package.Name):package.DisplayName,Version=package.Version,HiddenInBrowser=package.HiddenInBrowser});
   foreach(var card in package.Cards){
    if(String.IsNullOrWhiteSpace(card.Id)||String.IsNullOrWhiteSpace(card.Name)||card.SetId!=package.Id||Cards.ContainsKey(card.Id))throw new InvalidDataException("卡牌 ID 或所属牌库无效。");
    Cards.Add(card.Id,card);if(card.Collectible){if(card.DbfId<=0||ByDbf.ContainsKey(card.DbfId))throw new InvalidDataException("卡牌 DBF ID 重复。");ByDbf.Add(card.DbfId,card);}
   }
  }
  // Legacy IDs are aliases only; frozen VANILLA values always remain authoritative.
  foreach(var card in Cards.Values.Where(c=>c.Collectible&&c.LegacyDbfId>0))if(!ByDbf.ContainsKey(card.LegacyDbfId))ByDbf.Add(card.LegacyDbfId,card);
  foreach(var pool in poolFile.Pools){if(Pools.Any(p=>p.Id==pool.Id)||pool.DeckSize<=0||pool.DeckSize>100||pool.CopyLimit<1||pool.LegendaryLimit<1||pool.HeroDbfIds==null||pool.CardSetIds.Any(s=>!setIds.Contains(s)))throw new InvalidDataException("卡池配置无效。");Pools.Add(pool);}
  foreach(var pool in Pools)if(pool.CardReplacements!=null)foreach(var pair in pool.CardReplacements){CardRecord original,replacement;if(!Cards.TryGetValue(pair.Key,out original)||!Cards.TryGetValue(pair.Value,out replacement)||original.BaseId!=replacement.BaseId||!pool.CardSetIds.Contains(original.SetId)||!pool.Contains(replacement)||!original.Collectible||!replacement.Collectible)throw new InvalidDataException("卡池替代卡牌配置无效。");}
  if(Pools.Count==0)throw new InvalidDataException("没有可用卡池。");
 }
 CardCatalog(CardCatalog source){
  foreach(var entry in source.Cards)Cards.Add(entry.Key,entry.Value.CopyForSimulation());
  foreach(var entry in source.ByDbf)ByDbf.Add(entry.Key,Cards[entry.Value.Id]);
  foreach(var set in source.Sets)Sets.Add(new CardSetInfo{Id=set.Id,Name=set.Name,Version=set.Version,HiddenInBrowser=set.HiddenInBrowser});
  foreach(var pool in source.Pools)Pools.Add(pool.CopyForSimulation());
 }
 internal CardCatalog CopyForSimulation(){return new CardCatalog(this);}
 public static CardCatalog Load(string root){return new CardCatalog(new JsonDirectoryCardSetSource(Path.Combine(root,"data","cardsets")),JsonData.Read<PoolFile>(Path.Combine(root,"data","pools.json")));}
 public List<CardRecord> Deckable(PoolDefinition pool){return Cards.Values.Where(c=>c.Collectible&&pool.Contains(c)&&(c.Type=="MINION"||c.Type=="SPELL"||c.Type=="WEAPON")).OrderBy(c=>c.Cost).ThenBy(c=>c.Name,StringComparer.Ordinal).ToList();}
 public CardRecord ResolveDbf(PoolDefinition pool,int dbf){return Deckable(pool).FirstOrDefault(c=>c.DbfId==dbf||c.LegacyDbfId>0&&c.LegacyDbfId==dbf)??ResolveReplacedDbf(pool,dbf);}
 CardRecord ResolveReplacedDbf(PoolDefinition pool,int dbf){if(pool.CardReplacements==null)return null;foreach(var pair in pool.CardReplacements){var old=Card(pair.Key);if(old.DbfId==dbf||old.LegacyDbfId>0&&old.LegacyDbfId==dbf)return Card(pair.Value);}return null;}
 public List<CardRecord> BrowseDeckable(string setId=null){if(!String.IsNullOrEmpty(setId)&&!Sets.Any(s=>s.Id==setId))throw new InvalidDataException("未知卡牌版本："+setId);return Cards.Values.Where(c=>c.Collectible&&(String.IsNullOrEmpty(setId)||c.SetId==setId)&&(c.Type=="MINION"||c.Type=="SPELL"||c.Type=="WEAPON")).OrderBy(c=>c.Cost).ThenBy(c=>c.Name,StringComparer.Ordinal).ToList();}
 public List<CardRecord> BrowseForPool(PoolDefinition pool,string setId=null){return BrowseDeckable(setId).Where(c=>!Sets.First(s=>s.Id==c.SetId).HiddenInBrowser).Select(c=>pool.CardReplacements!=null&&pool.CardReplacements.ContainsKey(c.Id)?Card(pool.CardReplacements[c.Id]):c).GroupBy(c=>c.Id).Select(g=>g.First()).OrderBy(c=>c.Cost).ThenBy(c=>c.Name,StringComparer.Ordinal).ToList();}
 public CardRecord Card(string id){CardRecord card;if(!Cards.TryGetValue(id,out card))throw new InvalidDataException("未知卡牌："+id);return card;}
 public PoolDefinition Pool(string id){var pool=Pools.FirstOrDefault(p=>p.Id==id);if(pool==null)throw new InvalidDataException("未知卡池："+id);return pool;}
}
public sealed class DeckDocument {
 public int SchemaVersion=1;public string Id=Guid.NewGuid().ToString("N"),Name="新卡组",PoolId="classic-2014",ClassId="MAGE";
 public Dictionary<string,int> Cards=new Dictionary<string,int>();
 public int Count {get{return Cards.Values.Sum();}}
 public DeckDocument Copy(){return new DeckDocument{SchemaVersion=SchemaVersion,Id=Id,Name=Name,PoolId=PoolId,ClassId=ClassId,Cards=new Dictionary<string,int>(Cards)};}
}
public static class DeckRules {
 // Promote archived decks only into a compatible active superset, without modifying saved data.
 public static DeckDocument ForCurrentPlay(CardCatalog catalog,DeckDocument source){
  Validate(catalog,source,false);var copy=source.Copy();var old=catalog.Pool(source.PoolId);if(!old.Archived)return copy;
  var target=catalog.Pools.FirstOrDefault(p=>!p.Archived&&old.CardSetIds.All(set=>p.CardSetIds.Contains(set)));if(target==null)return copy;
  copy.PoolId=target.Id;copy.Cards.Clear();foreach(var pair in source.Cards){string id=pair.Key,replacement;if(target.CardReplacements!=null&&target.CardReplacements.TryGetValue(id,out replacement))id=replacement;int count;copy.Cards.TryGetValue(id,out count);copy.Cards[id]=count+pair.Value;}
  Validate(catalog,copy,false);return copy;
 }

 public static string CanAdd(CardCatalog catalog,DeckDocument deck,CardRecord card){
  var pool=catalog.Pool(deck.PoolId);if(!card.Collectible||!pool.Contains(card)||(card.Type!="MINION"&&card.Type!="SPELL"&&card.Type!="WEAPON"))return "这张牌不能加入当前卡池。";
  if(card.CardClass!="NEUTRAL"&&card.CardClass!=deck.ClassId)return "只能加入本职业或中立卡牌。";
  if(deck.Count>=pool.DeckSize)return "卡组已满。";int count;deck.Cards.TryGetValue(card.Id,out count);int max=card.Rarity=="LEGENDARY"?pool.LegendaryLimit:pool.CopyLimit;
  if(count>=max)return (card.Rarity=="LEGENDARY"?"同一传说卡":"同一卡牌")+"最多 "+max+" 张。";return null;
 }
 public static void Validate(CardCatalog catalog,DeckDocument deck,bool complete){
  if(deck==null||deck.SchemaVersion!=1||deck.Cards==null)throw new InvalidDataException("卡组文件格式无效。");var pool=catalog.Pool(deck.PoolId);
  if(!pool.HeroDbfIds.ContainsKey(deck.ClassId))throw new InvalidDataException("该职业不属于当前卡池。");
  if(deck.Cards.Count>pool.DeckSize)throw new InvalidDataException("卡组条目过多。");
  foreach(var pair in deck.Cards){var card=catalog.Card(pair.Key);if(!card.Collectible||!pool.Contains(card)||(card.Type!="MINION"&&card.Type!="SPELL"&&card.Type!="WEAPON"))throw new InvalidDataException("卡组包含当前卡池以外的卡牌。");if(card.CardClass!="NEUTRAL"&&card.CardClass!=deck.ClassId)throw new InvalidDataException("卡组包含其他职业的卡牌。");int max=card.Rarity=="LEGENDARY"?pool.LegendaryLimit:pool.CopyLimit;if(pair.Value<=0||pair.Value>max)throw new InvalidDataException(card.Name+" 的数量不合法。");}
  if(deck.Count>pool.DeckSize||complete&&deck.Count!=pool.DeckSize)throw new InvalidDataException("完整卡组需要 "+pool.DeckSize+" 张牌，当前 "+deck.Count+" 张。");
 }
}
public sealed class RawDeckCode {public int Format,Hero;public Dictionary<int,int> Cards=new Dictionary<int,int>();}
public static class DeckCode {
 public static string Encode(CardCatalog catalog,DeckDocument deck){DeckRules.Validate(catalog,deck,true);var pool=catalog.Pool(deck.PoolId);return EncodeRaw(new RawDeckCode{Format=pool.DeckstringFormat,Hero=pool.HeroDbfIds[deck.ClassId],Cards=deck.Cards.ToDictionary(p=>catalog.Card(p.Key).DbfId,p=>p.Value)});}
 public static string EncodeRaw(RawDeckCode deck){using(var stream=new MemoryStream()){
  stream.WriteByte(0);Write(stream,1);Write(stream,deck.Format);Write(stream,1);Write(stream,deck.Hero);
  for(int count=1;count<=2;count++){var cards=deck.Cards.Where(p=>p.Value==count).OrderBy(p=>p.Key).ToArray();Write(stream,cards.Length);foreach(var card in cards)Write(stream,card.Key);}
  var multiple=deck.Cards.Where(p=>p.Value!=1&&p.Value!=2).OrderBy(p=>p.Key).ToArray();Write(stream,multiple.Length);foreach(var card in multiple){Write(stream,card.Key);Write(stream,card.Value);}stream.WriteByte(0);return Convert.ToBase64String(stream.ToArray());
 }}
 public static RawDeckCode DecodeRaw(string input){
  if(input==null||input.Length>16384)throw new InvalidDataException("代码为空或过长。");var lines=input.Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0&&!s.StartsWith("#")).ToArray();if(lines.Length!=1)throw new InvalidDataException("请粘贴一串卡组代码。");byte[] bytes;try{bytes=Convert.FromBase64String(lines[0]);}catch(FormatException){throw new InvalidDataException("卡组代码不是有效的 Base64。");}
  if(bytes.Length>4096)throw new InvalidDataException("卡组代码过长。");using(var stream=new MemoryStream(bytes)){
   if(stream.ReadByte()!=0||Read(stream)!=1)throw new InvalidDataException("不支持的卡组代码版本。");var result=new RawDeckCode{Format=Read(stream)};if(result.Format<1||result.Format>4)throw new InvalidDataException("不支持的卡组模式。");if(Read(stream)!=1)throw new InvalidDataException("卡组必须有一个英雄。");result.Hero=Read(stream);
   for(int count=1;count<=3;count++){int n=Read(stream);if(n>100)throw new InvalidDataException("卡组条目过多。");for(int i=0;i<n;i++){int id=Read(stream),amount=count==3?Read(stream):count;if(id<=0||amount<=0||amount>100||result.Cards.ContainsKey(id))throw new InvalidDataException("代码中的卡牌重复或数量无效。");result.Cards.Add(id,amount);}}
   if(stream.Position<stream.Length){if(stream.ReadByte()!=0)throw new InvalidDataException("经典卡组不支持备选牌库。");}if(stream.Position!=stream.Length)throw new InvalidDataException("卡组代码尾部有多余数据。");return result;
  }
 }
 public static DeckDocument Decode(CardCatalog catalog,string input,string poolId){
  var raw=DecodeRaw(input);var pool=catalog.Pool(poolId);if(raw.Format!=pool.DeckstringFormat&&raw.Format!=1&&raw.Format!=2)throw new InvalidDataException("代码模式不属于当前卡池。");var hero=pool.HeroDbfIds.FirstOrDefault(p=>p.Value==raw.Hero);if(hero.Key==null)throw new InvalidDataException("无法识别经典英雄。");var deck=new DeckDocument{ClassId=hero.Key,PoolId=poolId,Name="导入卡组"};
  var title=input.Split(new char[]{'\r','\n'}).FirstOrDefault(s=>s.Trim().StartsWith("### "));if(title!=null)deck.Name=title.Trim().Substring(4).Trim();
  foreach(var pair in raw.Cards){CardRecord card=catalog.ResolveDbf(pool,pair.Key);if(card==null)throw new InvalidDataException("代码含有未知或非经典卡牌："+pair.Key);if(deck.Cards.ContainsKey(card.Id))throw new InvalidDataException("代码同时包含同一卡的两个版本。");deck.Cards.Add(card.Id,pair.Value);}DeckRules.Validate(catalog,deck,true);return deck;
 }
 static void Write(Stream stream,int number){if(number<0)throw new InvalidDataException("代码数值不能为负。");uint value=(uint)number;do{byte next=(byte)(value&127);value>>=7;if(value!=0)next|=128;stream.WriteByte(next);}while(value!=0);}
 static int Read(Stream stream){uint value=0;for(int shift=0;shift<35;shift+=7){int next=stream.ReadByte();if(next<0)throw new InvalidDataException("代码被截断。");if(shift==28&&(next&248)!=0)throw new InvalidDataException("代码数值溢出。");value|=(uint)(next&127)<<shift;if((next&128)==0)return checked((int)value);}throw new InvalidDataException("无效的变长整数。");}
}
}
