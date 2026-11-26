using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Touchfish {
public sealed partial class OfficeMatchVisualEffects {
 public const int JoustTravelMs=220,JoustHoldMs=1000,JoustPulseMs=280;
 public const int JoustReturnMs=JoustTravelMs+JoustHoldMs+JoustPulseMs;
 public const int JoustDurationMs=JoustReturnMs+JoustTravelMs;

 public void Joust(CardRecord own,CardRecord enemy,int winner,Point? ownDeck,Point? enemyDeck,int delay){
  if(!enabled())return;
  const double width=112,height=96;
  double center=overlay.ActualWidth/2,top=Math.Max(6,(overlay.ActualHeight-height)/2);
  var ownPlace=new Point(center-width-14,top);
  var enemyPlace=new Point(center+14,top);
  AddJoustCard(own,"我方",ownPlace,ownDeck??new Point(24,overlay.ActualHeight-36),width,height,winner==0,delay);
  AddJoustCard(enemy,"对手",enemyPlace,enemyDeck??new Point(24,50),width,height,winner==1,delay);
 }

 void AddJoustCard(CardRecord card,string owner,Point place,Point deck,double width,double height,bool winner,int delay){
  var body=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
  body.Children.Add(JoustText("拼点 · "+owner,9));
  body.Children.Add(JoustText(card==null?"无随从":card.Name,12));
  if(card!=null){
   body.Children.Add(JoustText("费用 "+card.Cost,14));
   body.Children.Add(JoustText(card.Attack+" 攻 / "+card.Health+" 血",9));
  }
  var cardFace=new Border{
   Tag="joust-"+owner,Width=width,Height=height,Padding=new Thickness(6),
   Background=surface,BorderBrush=ink,BorderThickness=new Thickness(1),
   Child=body,Opacity=0,RenderTransformOrigin=new Point(.5,.5)
  };
  var scale=new ScaleTransform();
  var motion=new TranslateTransform();
  var transforms=new TransformGroup();
  transforms.Children.Add(scale);
  transforms.Children.Add(motion);
  cardFace.RenderTransform=transforms;
  Canvas.SetLeft(cardFace,place.X);
  Canvas.SetTop(cardFace,place.Y);
  overlay.Children.Add(cardFace);

  double offsetX=deck.X-place.X-width/2,offsetY=deck.Y-place.Y-height/2;
  motion.BeginAnimation(TranslateTransform.XProperty,JoustMotion(offsetX,delay));
  motion.BeginAnimation(TranslateTransform.YProperty,JoustMotion(offsetY,delay));
  if(winner){
   var pulse=JoustFrames(delay,new[]{0,JoustTravelMs+JoustHoldMs,JoustReturnMs-JoustPulseMs/2,JoustReturnMs,JoustDurationMs},
    new[]{1.0,1,1.18,1,1});
   scale.BeginAnimation(ScaleTransform.ScaleXProperty,pulse);
   scale.BeginAnimation(ScaleTransform.ScaleYProperty,pulse);
  }
  var fade=JoustFrames(delay,new[]{0,JoustTravelMs,JoustReturnMs,JoustDurationMs},new[]{.4,1,1,0});
  fade.Completed+=(s,e)=>overlay.Children.Remove(cardFace);
  cardFace.BeginAnimation(UIElement.OpacityProperty,fade);
 }

 TextBlock JoustText(string value,double size){
  return new TextBlock{Text=value,FontSize=size,FontWeight=FontWeights.Normal,Foreground=ink,
   TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,1,0,1)};
 }
 static DoubleAnimationUsingKeyFrames JoustMotion(double offset,int delay){
  return JoustFrames(delay,new[]{0,JoustTravelMs,JoustReturnMs,JoustDurationMs},new[]{offset,0,0,offset});
 }
 static DoubleAnimationUsingKeyFrames JoustFrames(int delay,int[] times,double[] values){
  var frames=new DoubleAnimationUsingKeyFrames{
   BeginTime=TimeSpan.FromMilliseconds(delay),Duration=TimeSpan.FromMilliseconds(JoustDurationMs)
  };
  for(int i=0;i<times.Length;i++)
   frames.KeyFrames.Add(new LinearDoubleKeyFrame(values[i],KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(times[i]))));
  return frames;
 }
}

public partial class PanelWindow {
 void PresentJoust(MatchVisualEvent item,int delay){
  if(item.JoustCards==null||item.JoustCards.Length!=2)return;
  UpdateLayout();
  var own=JoustCard(item.JoustCards[matchViewSeat]);
  var enemy=JoustCard(item.JoustCards[1-matchViewSeat]);
  matchEffects.Joust(own,enemy,item.JoustWinner<0?-1:item.JoustWinner==matchViewSeat?0:1,
   JoustDeckAnchor(deckCountText,true),JoustDeckAnchor(enemyHandStatus,false),delay);
 }

 CardRecord JoustCard(string id){
  CardRecord card;
  return id!=null&&catalog.Cards.TryGetValue(id,out card)?card:null;
 }

 Point? JoustDeckAnchor(TextBlock counter,bool own){
  if(counter==null||!counter.IsVisible||!counter.IsDescendantOf(shell)||counter.ActualHeight<=0)return null;
  // The enemy's count follows mana and hand size on the same line.
  return counter.TranslatePoint(new Point(own?65:115,counter.ActualHeight/2),matchEffectsCanvas);
 }
}
}
