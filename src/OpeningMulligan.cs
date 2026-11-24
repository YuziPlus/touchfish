using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchfish {
public static partial class OpeningMulligan {
 // Generic fallback and a symmetric control for isolating turn search.
 public static int[] Curve(AgentObservation view){
  if(view==null)throw new ArgumentNullException("view");
  var minions=view.Hand.Where(card=>card.Type=="MINION").OrderBy(card=>card.Cost).ThenBy(card=>card.Id).ToArray();
  var keep=new HashSet<int>();
  if(minions.Any(card=>card.Cost<=1))keep.Add(minions.First(card=>card.Cost<=1).Id);
  var two=minions.FirstOrDefault(card=>card.Cost==2);
  if(two!=null)keep.Add(two.Id);
  if(keep.Count>0){
   var three=minions.FirstOrDefault(card=>card.Cost==3);
   if(three!=null)keep.Add(three.Id);
  }
  return view.Hand.Where(card=>!keep.Contains(card.Id)).Select(card=>card.Id).ToArray();
 }
}
}
