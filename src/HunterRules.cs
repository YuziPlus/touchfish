using System;
using System.Linq;
using System.Collections.Generic;

namespace Touchfish {
public static class HunterRules {
 public static readonly ConditionalTrigger[] Triggers={new ConditionalTrigger{CardId="CS2_237",Timing=MatchTriggerTiming.MinionSummoned,Condition=c=>c.Summoned!=null&&c.Seat==c.Source.Owner&&c.Summoned.Race=="BEAST"&&c.Summoned.Id!=c.Source.Id,Apply=c=>c.Game.Draw(c.Source.Owner,1)}};
 public const string TrackingCardId="DS1_184";
 public static string[] TrackingPreview(MatchEngine game,int seat){
  if(game==null||seat<0||seat>1)return new string[0];var deck=game.Players[seat].Deck;
  if(deck.Count>0&&deck[0]!="HIDDEN")return deck.Take(3).ToArray();
  return game.PrivateDeckPreview[seat]??new string[0];
 }
 public static MatchChoiceOption[] TrackingOptions(MatchEngine game,int seat){return TrackingPreview(game,seat).Select((id,index)=>new MatchChoiceOption("track:"+index,game.Card(id).Name,game.Card(id).Cost+"费 · "+game.Card(id).Text)).ToArray();}
 public static bool IsTrackingChoiceValid(MatchEngine game,int seat,string choice){int index=TrackingIndex(choice);return index>=0&&index<TrackingPreview(game,seat).Length;}
 static int TrackingIndex(string choice){if(String.IsNullOrEmpty(choice)||!choice.StartsWith("track:"))return -1;int index;return Int32.TryParse(choice.Substring(6),out index)?index:-1;}
 public static string Requirement(MatchEngine game,int seat,string cardId){if(cardId=="DS1_183"&&game.Players[1-seat].Board.Count<2)return "对方至少需要两个随从。";return null;}
 public static void Apply(EffectContext c){switch(c.Card.BaseId){
  case "DS1_183":MultiShot(c);break;
  case "DS1_184":Track(c);break;
  case "DS1_188":break;
  case "DS1_070":if(c.Target!=null){var unit=c.Game.LivingUnit(c.Target);if(unit!=null&&unit.Owner==c.Seat&&unit.Race=="BEAST"){c.Game.Buff(c.Target,2,2);unit.Taunt=true;}}break;
  case "EX1_531":case "CS2_237":break;
  case "EX1_536":break;
  case "EX1_537":ExplosiveShot(c);break;
  case "EX1_538":Unleash(c);break;
  case "EX1_544":Flare(c);break;
  case "EX1_549":if(c.Target!=null){var beast=c.Game.LivingUnit(c.Target);if(beast!=null&&beast.Owner==c.Seat&&beast.Race=="BEAST"){beast.TempAttack+=2;beast.ImmuneUntilTurn=c.Game.Turn;}}break;
 }}
 static void Track(EffectContext c){var p=c.Game.Players[c.Seat];var preview=TrackingPreview(c.Game,c.Seat);int chosen=TrackingIndex(c.Choice);if(preview.Length==0)return;if(chosen<0)chosen=0;if(chosen>=preview.Length)return;
  for(int i=preview.Length-1;i>=0;i--)if(i!=chosen){string discarded=p.Deck[i];p.Deck.RemoveAt(i);c.Game.RecordGraveyard(c.Seat,c.Seat,discarded,c.Game.NewEffectInstanceId(),GraveyardReason.Discarded);}
  if(p.Deck.Count==0)return;string selected=p.Deck[0];p.Deck.RemoveAt(0);if(p.Hand.Count<c.Game.Mode.HandLimit){p.Hand.Add(new HandCard{Id=c.Game.NewEffectInstanceId(),CardId=selected});c.Game.Log.Add(c.Game.PlayerLabel(c.Seat)+" · 抽牌 1 张");}else c.Game.RecordGraveyard(c.Seat,c.Seat,selected,c.Game.NewEffectInstanceId(),GraveyardReason.Burned);
 }
 static void MultiShot(EffectContext c){c.Game.DamageRandomMinions(1-c.Seat,2,c.Game.SpellDamageAmount(c.Seat,3));}
 static void ExplosiveShot(EffectContext c){var target=c.Game.Unit(c.Target);if(target==null)return;var board=c.Game.Players[target.Owner].Board;int index=board.IndexOf(target);var adjacent=new[]{index>0?board[index-1]:null,index+1<board.Count?board[index+1]:null}.Where(u=>u!=null).ToArray();c.Game.DamageBatch(()=>{c.Game.Damage(c.Target,c.Game.SpellDamageAmount(c.Seat,5));foreach(var unit in adjacent)c.Game.Damage(new MatchTarget(unit.Owner,unit.Id),c.Game.SpellDamageAmount(c.Seat,2));});}
 static void Unleash(EffectContext c){int count=c.Game.Players[1-c.Seat].Board.Count;for(int i=0;i<count&&c.Game.Players[c.Seat].Board.Count<c.Game.Mode.BoardSlots;i++)c.Game.Summon(c.Seat,"GAME_HOUND");}
 static void Flare(EffectContext c){foreach(var unit in c.Game.Players.SelectMany(p=>p.Board))unit.Stealth=false;c.Game.DestroySecrets(1-c.Seat);c.Game.Draw(c.Seat,1);}
 public static void MinionDied(MatchEngine game,BattleUnit dead){if(dead==null||dead.Race!="BEAST")return;foreach(var hyena in game.Players[dead.Owner].Board.Where(u=>u.Health>0&&!u.Silenced&&game.Card(u.CardId).BaseId=="EX1_531").ToArray())game.Buff(new MatchTarget(hyena.Owner,hyena.Id),2,1);}
 public static void SecretRevealed(MatchEngine game,int seat){for(int owner=0;owner<2;owner++){var p=game.Players[owner];if(p.WeaponId==null||p.WeaponDurability<=0)continue;var weapon=game.Card(p.WeaponId);if(weapon.BaseId=="EX1_536"&&(owner==seat||!weapon.FriendlySecretsOnly))p.WeaponDurability++;}}
}
}
