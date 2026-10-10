using System;
using System.Collections.Generic;
using System.Linq;
namespace Touchfish {
// Discard operates on individual hand entries, after the played card has left the hand.
public sealed partial class MatchEngine {
 public HandCard[] DiscardRandom(int seat,int count){var removed=new List<HandCard>();var hand=Players[seat].Hand;for(int n=0;n<count&&hand.Count>0;n++){var card=hand[random.Next(hand.Count)];hand.Remove(card);removed.Add(card);RecordDiscard(seat,card);}return removed.ToArray();}
 public HandCard[] DiscardAll(int seat){var hand=Players[seat].Hand;var removed=hand.ToArray();hand.Clear();foreach(var card in removed)RecordDiscard(seat,card);return removed;}
 void RecordDiscard(int seat,HandCard card){RecordGraveyard(seat,seat,card.CardId,card.Id,GraveyardReason.Discarded);string name=Card(card.CardId).Name;Log.Add("玩家 "+(seat+1)+" · 弃牌："+name);EmitVisual("discard",seat,name:name);}
 public void DeathwingBattlecry(int seat,BattleUnit summoned){foreach(var unit in Players.SelectMany(p=>p.Board).Where(u=>u!=summoned).ToArray())unit.DamageTaken=unit.MaxHealth;DiscardAll(seat);}
}
}
