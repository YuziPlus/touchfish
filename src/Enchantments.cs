using System.Collections.Generic;
using System.Linq;
namespace Touchfish {
public sealed class Enchantment {public string Kind,CardId;public int Attack,Health,Amount,ExpiresTurn=-1;public bool RemovedBySilence=true;}
public sealed class EnchantmentCollection {
 public List<Enchantment> Items=new List<Enchantment>();
 internal EnchantmentCollection CopyForSimulation(){
  var copy=new EnchantmentCollection();
  foreach(var item in Items)copy.Items.Add(new Enchantment{Kind=item.Kind,CardId=item.CardId,Attack=item.Attack,Health=item.Health,Amount=item.Amount,ExpiresTurn=item.ExpiresTurn,RemovedBySilence=item.RemovedBySilence});
  return copy;
 }
 public int Stat(bool health){
  int total=0;
  foreach(var item in Items)if(item.Kind=="stats")total+=health?item.Health:item.Attack;
  return total;
 }
 public void SetStat(bool health,int value){var item=Items.FirstOrDefault(e=>e.Kind=="stats");if(item==null){if(value==0)return;item=new Enchantment{Kind="stats"};Items.Add(item);}if(health)item.Health=value;else item.Attack=value;if(item.Attack==0&&item.Health==0)Items.Remove(item);}
 public int Expiry(string kind){var item=Items.FirstOrDefault(e=>e.Kind==kind);return item==null?-1:item.ExpiresTurn;}
 public void SetExpiry(string kind,int turn){Items.RemoveAll(e=>e.Kind==kind);if(turn>=0)Items.Add(new Enchantment{Kind=kind,ExpiresTurn=turn});}
 public int Counter(string kind){
  int total=0;
  foreach(var item in Items)if(item.Kind==kind)total+=item.Amount;
  return total;
 }
 public void SetCounter(string kind,int value){Items.RemoveAll(e=>e.Kind==kind);if(value!=0)Items.Add(new Enchantment{Kind=kind,Amount=value});}
 public int Value(string kind){var item=Items.FirstOrDefault(e=>e.Kind==kind);return item==null?-1:item.Amount;}
 public void SetValue(string kind,int value){Items.RemoveAll(e=>e.Kind==kind);if(value>=0)Items.Add(new Enchantment{Kind=kind,Amount=value,RemovedBySilence=!kind.StartsWith("return-")});}
 public void Silence(){Items.RemoveAll(e=>e.RemovedBySilence);}
}
}
