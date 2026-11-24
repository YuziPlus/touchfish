using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Touchfish;

namespace Touchfish {
public enum LeafValueMode { Heuristic, Neural, Shadow }

public sealed class LeafValueMetrics {
 public int Calls,Failures,Terminal,Truncated;
 public double Milliseconds,Sum,Minimum=1,Maximum;
}

public sealed class ValueNetwork {
 public const int Features=293;
 public const int Schema=1;
 const int Hidden=128;
 readonly float[][] weights;
 readonly float[][] biases;
 ValueNetwork(float[][] weights,float[][] biases){this.weights=weights;this.biases=biases;}

 public static ValueNetwork Load(string path){
  using(var reader=new BinaryReader(File.OpenRead(path))){
   if(reader.ReadInt32()!=0x31564c54||reader.ReadInt32()!=Schema||reader.ReadInt32()!=Features||reader.ReadInt32()!=Hidden)
    throw new InvalidDataException("Incompatible value model.");
   int[] sizes={Features,Hidden,Hidden,1};
   var w=new float[3][];
   var b=new float[3][];
   for(int i=0;i<3;i++){w[i]=Read(reader,sizes[i]*sizes[i+1]);b[i]=Read(reader,sizes[i+1]);}
   if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Trailing model bytes.");
   return new ValueNetwork(w,b);
  }
 }
 public static ValueNetwork TryLoad(string path,out string diagnostic){
  try{var network=Load(path);diagnostic=null;return network;}
  catch(InvalidDataException ex){diagnostic=ex.Message;}
  catch(IOException ex){diagnostic=ex.Message;}
  catch(UnauthorizedAccessException ex){diagnostic=ex.Message;}
  catch(ArgumentException ex){diagnostic=ex.Message;}
  return null;
 }
 static float[] Read(BinaryReader reader,int size){
  var values=new float[size];
  for(int i=0;i<size;i++){
   values[i]=reader.ReadSingle();
   if(float.IsNaN(values[i])||float.IsInfinity(values[i])||Math.Abs(values[i])>100)throw new InvalidDataException("Invalid model weight.");
  }
  return values;
 }
 public double Predict(AgentObservation observation){
  return Forward(Encode(observation));
 }
 internal double Forward(float[] input){
  for(int layer=0;layer<3;layer++){
   var output=new float[biases[layer].Length];
   for(int j=0;j<output.Length;j++){
    double sum=biases[layer][j];
    for(int i=0;i<input.Length;i++)sum+=(double)input[i]*weights[layer][j*input.Length+i];
    output[j]=(float)(layer==2?sum:Math.Max(0,sum));
   }
   input=output;
  }
  return 1/(1+Math.Exp(-input[0]));
 }

 public static float[] Encode(AgentObservation v){
  var x=new List<float>(Features);
  Add(x,v.Turn/40f,v.ActiveSeat==v.Seat?1:0,v.FirstSeat==v.Seat?1:0,v.SpellPower/5f,
   v.CardsPlayed/10f,v.DeathsThisTurn/10f,v.PreparationDiscount/5f,v.Decision==null?0:v.Decision.Options.Length/5f,
   v.MulliganComplete?1:0);
  Player(x,v.Self);Player(x,v.Enemy);
  for(int i=0;i<10;i++){
   if(i>=v.Hand.Length){Zeros(x,7);continue;}
   var c=v.Hand[i];
   Add(x,1,c.Cost/10f,c.BaseCost/10f,c.Type=="MINION"?1:0,c.Type=="SPELL"?1:0,c.Charge?1:0,c.Secret?1:0);
  }
  return x.ToArray();
 }
 static void Player(List<float> x,AgentPlayer p){
  Add(x,p.Health/30f,p.Armor/30f,p.Mana/10f,p.MaxMana/10f,p.DeckCount/30f,p.HandCount/10f,
   p.HeroAttack/10f,p.WeaponAttack/10f,p.WeaponDurability/5f,p.Fatigue/10f,p.SecretCount/5f,
   p.LockedMana/10f,p.Overload/10f,p.Frozen?1:0,p.Immune?1:0,p.PowerUsed?1:0);
  for(int i=0;i<7;i++){
   if(i>=p.Board.Length){Zeros(x,13);continue;}
   var u=p.Board[i];
   Add(x,1,u.Attack/12f,u.Health/12f,u.MaxHealth/12f,u.Cost/10f,u.AttacksRemaining/2f,
    u.Frozen?1:0,u.Shield?1:0,u.Taunt?1:0,u.Stealth?1:0,u.Poison?1:0,u.Windfury?1:0,u.SpellDamage/5f);
  }
 }
 static void Add(List<float> x,params float[] values){foreach(float value in values)x.Add(Math.Max(-4,Math.Min(4,value)));}
 static void Zeros(List<float> x,int n){for(int i=0;i<n;i++)x.Add(0);}
}
}
