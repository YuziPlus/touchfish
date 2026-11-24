using System;
using System.Collections.Generic;
namespace Touchfish {
public sealed class GameModeDefinition {
 public readonly string Id;public readonly int BoardSlots,HandLimit,ManaLimit,StartingHealth,FirstHand,SecondHand,StartingMana;
 public readonly bool RequireCompleteDeck,AllowTestTools;
 public readonly Action<MatchEngine,int> TurnStarted,TurnEnded;public readonly Func<MatchEngine,int> EvaluateWinner;public readonly Action<CardCatalog,DeckDocument,bool> ValidateDeck;
 public GameModeDefinition(string id,int boardSlots=7,int handLimit=10,int manaLimit=10,int startingHealth=30,int firstHand=3,int secondHand=4,int startingMana=1,bool requireCompleteDeck=true,bool allowTestTools=false,Action<MatchEngine,int> turnStarted=null,Action<MatchEngine,int> turnEnded=null,Func<MatchEngine,int> evaluateWinner=null,Action<CardCatalog,DeckDocument,bool> validateDeck=null){
  if(String.IsNullOrWhiteSpace(id)||boardSlots<1||boardSlots>7||handLimit<1||handLimit>10||manaLimit<1||manaLimit>10||startingHealth<1||firstHand<0||secondHand<0||firstHand>=handLimit||secondHand>=handLimit||startingMana<0||startingMana>manaLimit)throw new ArgumentException("模式参数超出当前办公界面支持范围。");
  Id=id;BoardSlots=boardSlots;HandLimit=handLimit;ManaLimit=manaLimit;StartingHealth=startingHealth;FirstHand=firstHand;SecondHand=secondHand;StartingMana=startingMana;RequireCompleteDeck=requireCompleteDeck;AllowTestTools=allowTestTools;TurnStarted=turnStarted;TurnEnded=turnEnded;EvaluateWinner=evaluateWinner;ValidateDeck=validateDeck;
 }
}
public static class GameModes {
 public static readonly GameModeDefinition Classic=new GameModeDefinition("classic"),Arena=new GameModeDefinition("arena",validateDeck:ArenaDraftSession.Validate),Sealed=new GameModeDefinition("sealed",validateDeck:SealedSession.Validate),LocalTest=new GameModeDefinition("local-test",startingMana:10,requireCompleteDeck:false,allowTestTools:true);
 public static readonly GameModeDefinition StrictTest=new GameModeDefinition("strict-test",requireCompleteDeck:false);
 static readonly Dictionary<string,GameModeDefinition> modes=new Dictionary<string,GameModeDefinition>{{Classic.Id,Classic},{Arena.Id,Arena},{Sealed.Id,Sealed},{LocalTest.Id,LocalTest},{StrictTest.Id,StrictTest}};
 public static void Register(GameModeDefinition mode){if(mode==null||modes.ContainsKey(mode.Id))throw new ArgumentException("模式 ID 已存在或无效。");modes.Add(mode.Id,mode);}
 public static GameModeDefinition Get(string id){GameModeDefinition mode;if(!modes.TryGetValue(id,out mode))throw new ArgumentException("未知对战模式："+id);return mode;}
}
}
