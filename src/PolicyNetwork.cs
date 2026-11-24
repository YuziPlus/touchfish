using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Touchfish {
public sealed class PolicyMetrics {
 public int Calls,CacheHits,Failures;
 public double Milliseconds;
}
public sealed class PolicyNetwork {
 public const int Schema=1, ActionFeatures=63, Hidden=32;
 static readonly string[] ActionKinds={"play","attack","power","end","decision","mulligan"};
 readonly float[] state,action,bias,output;
 public readonly int ObservationSchema;
 readonly int observationWidth;
 readonly int actionWidth;
 PolicyNetwork(BinaryReader r,int schema){
  ObservationSchema=schema;
  observationWidth=schema==EffectFeatures.Schema?EffectFeatures.Width:schema==TacticalFeatures.Schema?TacticalFeatures.Width:ValueNetwork.Features;
  actionWidth=schema==EffectFeatures.Schema?ActionFeatures+EffectFeatures.EffectWidth:ActionFeatures;
  state=Read(r,Hidden*observationWidth);action=Read(r,Hidden*actionWidth);
  bias=Read(r,Hidden);output=Read(r,Hidden);
 }
 static float[] Read(BinaryReader r,int n){
  var x=new float[n];
  for(int i=0;i<n;i++){x[i]=r.ReadSingle();if(float.IsNaN(x[i])||float.IsInfinity(x[i])||Math.Abs(x[i])>100)throw new InvalidDataException("Invalid policy weights.");}
  return x;
 }
 public static PolicyNetwork TryLoad(string path,out string diagnostic){
  try{
   using(var r=new BinaryReader(File.OpenRead(path))){
    if(r.ReadInt32()!=0x31504c54||r.ReadInt32()!=Schema)throw new InvalidDataException("Incompatible policy model.");
    int observation=r.ReadInt32();
    if((observation!=ValueNetwork.Schema&&observation!=TacticalFeatures.Schema&&observation!=EffectFeatures.Schema)||
       r.ReadInt32()!=(observation==EffectFeatures.Schema?ActionFeatures+EffectFeatures.EffectWidth:ActionFeatures)||
       r.ReadInt32()!=Hidden)throw new InvalidDataException("Incompatible policy model.");
    var model=new PolicyNetwork(r,observation);
    if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Trailing policy bytes.");
    diagnostic=null;return model;
   }
  }catch(InvalidDataException ex){diagnostic=ex.Message;}
   catch(IOException ex){diagnostic=ex.Message;}
   catch(UnauthorizedAccessException ex){diagnostic=ex.Message;}
   catch(ArgumentException ex){diagnostic=ex.Message;}
  return null;
 }
 internal static float[] EncodeAction(AgentObservation view,SearchAction action,int option){
  var m=action.SimulatedMove;
  int source=-1,target=-1,position=-1;
  string side=null,zone=null,card=null,choice=action.OptionId;
  if(m!=null){
   position=m.Position;choice=m.Choice;
   if(m.Kind=="play")source=Array.FindIndex(view.Hand,h=>h.Id==m.Source);
   if(m.Kind=="attack"&&m.Source!=0)source=Array.FindIndex(view.Self.Board,u=>u.Id==m.Source);
   card=m.Kind=="play"&&source>=0?view.Hand[source].CardId:m.Kind=="attack"&&source>=0?view.Self.Board[source].CardId:null;
   if(m.Target!=null){
    bool own=m.Target.Seat==view.Seat;side=own?"self":"enemy";zone=m.Target.Hero?"hero":"board";
    if(!m.Target.Hero)target=Array.FindIndex((own?view.Self:view.Enemy).Board,u=>u.Id==m.Target.UnitId);
   }
  }
  return EncodeAction(m==null?"decision":m.Kind,source,target,position,m==null?option:-1,side,zone,card,choice,null);
 }
 static int Hash(string s){
  uint h=2166136261;
  foreach(char c in s)h=unchecked((h^c)*16777619);
  return (int)(h%8);
 }
 internal static float[] EncodeAction(string kind,int source,int target,int position,int option,string side,string zone,string card,string choice,int[] replacements){
  var x=new float[ActionFeatures];
  int k=Array.IndexOf(ActionKinds,kind);
  if(k>=0)x[k]=1;
  if(source>=0&&source<10)x[6+source]=1;
  if(target>=0&&target<7)x[16+target]=1;
  if(position>=0&&position<8)x[23+position]=1;
  if(option>=0&&option<5)x[31+option]=1;
  if(side!=null)x[36+(side=="self"?0:1)]=1;
  if(zone!=null)x[38+(zone=="hero"?0:1)]=1;
  if(card!=null)x[40+Hash(card)]=1;
  if(choice!=null)x[48+Hash(choice)]=1;
  if(replacements!=null){
   foreach(int i in replacements)if(i>=0&&i<6)x[56+i]=1;
   x[62]=replacements.Length/10f;
  }
  return x;
 }
 internal double[] Logits(float[] observation,float[][] candidates){
  if(observation.Length!=observationWidth)throw new ArgumentException("Policy observation schema mismatch.");
  var context=new double[Hidden];
  for(int h=0;h<Hidden;h++){
   double v=bias[h];int offset=h*observationWidth;
   for(int i=0;i<observation.Length;i++)v+=(double)state[offset+i]*observation[i];
   context[h]=v;
  }
  var scores=new double[candidates.Length];
  for(int j=0;j<candidates.Length;j++){
   var x=candidates[j];double score=0;
   for(int h=0;h<Hidden;h++){
    double v=context[h];int offset=h*actionWidth;
    for(int i=0;i<x.Length;i++)v+=(double)action[offset+i]*x[i];
    score+=Math.Max(0,v)*output[h];
   }
   scores[j]=score;
  }
  return scores;
 }
 internal static float[] EncodeEffectAction(MatchEngine game,AgentObservation view,SearchAction candidate,int index){
  var move=candidate.SimulatedMove;
  var source=move!=null&&move.Kind=="play"?Array.Find(view.Hand,h=>h.Id==move.Source):null;
  return EncodeAction(view,candidate,index).Concat(source==null?new float[EffectFeatures.EffectWidth]:
   EffectFeatures.Card(game.Rules,game.Card(source.CardId))).ToArray();
 }
 internal static double[] Probabilities(double[] logits){
  double max=logits.Max(),sum=0;
  var p=new double[logits.Length];
  for(int i=0;i<p.Length;i++){p[i]=Math.Exp(logits[i]-max);sum+=p[i];}
  if(double.IsNaN(sum)||double.IsInfinity(sum)||sum<=0)return null;
  for(int i=0;i<p.Length;i++)p[i]=0.95*p[i]/sum+0.05/p.Length;
  return p;
 }
}
}
