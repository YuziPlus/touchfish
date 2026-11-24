using System;

namespace Touchfish {
// The seeded System.Random sequence used by the desktop engine, with explicit copyable state.
internal sealed class SimulationRandom {
 const int Range=int.MaxValue;
 readonly int[] values;
 int next,other=21;

 internal SimulationRandom(int seed){
  values=new int[56];
  int previous=161803398-(seed==int.MinValue?int.MaxValue:Math.Abs(seed));
  values[55]=previous;
  int current=1;
  for(int i=1;i<55;i++){
   int index=21*i%55;
   values[index]=current;
   current=previous-current;
   if(current<0)current+=Range;
   previous=values[index];
  }
  for(int pass=0;pass<4;pass++){
   for(int i=1;i<=55;i++){
    values[i]-=values[1+(i+30)%55];
    if(values[i]<0)values[i]+=Range;
   }
  }
 }
 SimulationRandom(SimulationRandom source){
  values=(int[])source.values.Clone();
  next=source.next;
  other=source.other;
 }
 internal SimulationRandom Copy(){return new SimulationRandom(this);}
 internal int Next(int maximum){
  if(maximum<0)throw new ArgumentOutOfRangeException("maximum");
  if(++next==56)next=1;
  if(++other==56)other=1;
  int sample=values[next]-values[other];
  if(sample==Range)sample--;
  if(sample<0)sample+=Range;
  values[next]=sample;
  return (int)(sample*(1.0/Range)*maximum);
 }
}
}
