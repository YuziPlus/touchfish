using System.Linq;
namespace Touchfish {
public partial class PanelWindow {
 string HandAttackBonusDetail(){if(match==null)return "";var hand=match.Players[matchViewSeat].Hand.FirstOrDefault(h=>h.Id==inspectedHandId);return hand==null||hand.AttackBonus==0?"":"\n手牌成长：攻击 +"+hand.AttackBonus+"（当前攻击 "+(inspectedCard.Attack+hand.AttackBonus)+"）";}
}
}

