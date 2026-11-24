using System.Linq;

namespace Touchfish {
public static class DruidSpellRules {
 public static readonly string[] RuntimeCardIds={"GAME_EXCESS_MANA"};
 public static void WildGrowth(MatchEngine game,int seat){var player=game.Players[seat];if(player.MaxMana<game.Mode.ManaLimit){player.MaxMana++;return;}game.GenerateExcessMana(seat);}
 public static void SoulOfForest(MatchEngine game,int seat){foreach(var unit in game.Players[seat].Board.Where(u=>u.Health>0).ToArray())unit.Enchantments.Items.Add(new Enchantment{Kind="forest-deathrattle",Amount=1});}
}
public sealed partial class MatchEngine {
 public void GenerateExcessMana(int seat){var player=Players[seat];if(player.Hand.Count>=Mode.HandLimit){Log.Add(PlayerLabel(seat)+" · 手牌已满，未获得法力过剩。");return;}player.Hand.Add(new HandCard{Id=nextId++,CardId="GAME_EXCESS_MANA",Generated=true});NoteHistory(HistoryKind.Gain,seat,"GAME_EXCESS_MANA");Log.Add(PlayerLabel(seat)+" · 获得法力过剩 1 张。");}
}
}
