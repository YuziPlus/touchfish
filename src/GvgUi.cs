using System.Linq;
namespace Touchfish {
public partial class PanelWindow {
 string HandAttackBonusDetail(){if(match==null)return "";var hand=match.Players[matchViewSeat].Hand.FirstOrDefault(h=>h.Id==inspectedHandId);return hand==null||hand.AttackBonus==0&&hand.HealthBonus==0?"":"\n手牌成长：当前攻击 "+(inspectedCard.Attack+hand.AttackBonus)+" · 当前生命 "+(inspectedCard.Health+hand.HealthBonus);}
}
}

