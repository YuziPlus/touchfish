using System;
using System.IO;
using System.Linq;
using Touchfish;
internal static class OodDecks {
 public static void Main(string[] args){
  var catalog=CardCatalog.Load(args[0]);
  var rules=new MatchRules(catalog);
  var zoo=ZooWarlockDeck.Create(catalog);
  zoo.Cards.Remove("VAN_EX1_319");
  zoo.Cards["VAN_CS2_182"]=2;
  var mage=rules.TrainingDeck("MAGE");
  var rogue=rules.TrainingDeck("ROGUE");
  Console.WriteLine(JsonData.Serializer().Serialize(new{zoo_variant=DeckCode.Encode(catalog,zoo),mage_board=DeckCode.Encode(catalog,mage),rogue_board=DeckCode.Encode(catalog,rogue)}));
 }
}
