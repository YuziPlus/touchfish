using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
public sealed class SecretInstance {public int Id,Seat;public string CardId;}
public static class SecretRules {
 public static readonly string[] Ids={"EX1_130","EX1_132","EX1_136","EX1_287","EX1_289","EX1_294","EX1_295","EX1_379","EX1_533","EX1_554","EX1_594","EX1_609","EX1_610","EX1_611","tt_010"};
 public static void Apply(EffectContext c){c.Game.ArmSecret(c.Seat,c.PlayedHand);}
}
public sealed partial class MatchEngine {
 // Only the owner receives secret identities; public player snapshots retain counts only.
 readonly List<SecretInstance> secrets=new List<SecretInstance>();
 public string[] SecretCards(int seat){return secrets.Where(s=>s.Seat==seat).Select(s=>s.CardId).ToArray();}
 internal void RestoreOwnSecrets(int seat,string[] ids){secrets.RemoveAll(s=>s.Seat==seat);foreach(string id in ids)secrets.Add(new SecretInstance{Id=nextId++,Seat=seat,CardId=id});}
 internal void NotifySecretFromDeck(int seat,string id){EmitVisual("secret-armed",seat,name:Card(id).Name);var item=VisualEvents[VisualEvents.Count-1];item.CardId=id;item.RecipientSeat=seat;}
 public string SecretRequirement(int seat,CardRecord card){return Players[seat].SecretCount>=5?"最多同时挂出五张奥秘。":secrets.Any(s=>s.Seat==seat&&s.CardId==card.Id)?"不能重复挂出同名奥秘。":null;}
 public void DestroySecrets(int seat){secrets.RemoveAll(s=>s.Seat==seat);Players[seat].SecretCount=0;}
 public void ArmSecret(int seat,HandCard hand){secrets.Add(new SecretInstance{Id=hand.Id,Seat=seat,CardId=hand.CardId});Players[seat].SecretCount=secrets.Count(s=>s.Seat==seat);}
 SecretInstance[] SecretQueue(int seat){return Active==seat?new SecretInstance[0]:secrets.Where(s=>s.Seat==seat).ToArray();}
 bool SecretPresent(SecretInstance s){return secrets.Contains(s);}
 void RevealSecret(SecretInstance s){secrets.Remove(s);Players[s.Seat].SecretCount=secrets.Count(x=>x.Seat==s.Seat);HunterRules.SecretRevealed(this,s.Seat);string name=Card(s.CardId).Name;Log.Add(PlayerLabel(s.Seat)+" · 奥秘触发："+name);EmitVisual("spell",s.Seat,name:"奥秘触发："+name);}
 public bool SecretSpell(int caster,ref MatchTarget target){foreach(var s in SecretQueue(1-caster)){if(!SecretPresent(s))continue;string id=Card(s.CardId).BaseId;if(id=="EX1_287"){RevealSecret(s);return true;}if(id=="tt_010"&&target!=null&&!target.Hero&&Players[s.Seat].Board.Count<Mode.BoardSlots){RevealSecret(s);var bender=Summon(s.Seat,"GAME_SPELLBENDER");target=new MatchTarget(s.Seat,bender.Id);}}return false;}
 public void SecretMinionPlayed(int caster,BattleUnit minion){foreach(var s in SecretQueue(1-caster)){if(!SecretPresent(s))continue;if(minion==null||!Players[caster].Board.Contains(minion)||minion.Health<=0||Finished)break;switch(Card(s.CardId).BaseId){case "EX1_379":RevealSecret(s);SetHealth(new MatchTarget(caster,minion.Id),1);break;case "EX1_609":RevealSecret(s);Damage(new MatchTarget(caster,minion.Id),4);Cleanup();break;case "EX1_294":if(Players[s.Seat].Board.Count<Mode.BoardSlots){RevealSecret(s);CopySecretMinion(s.Seat,minion);}break;}}}
 void CopySecretMinion(int seat,BattleUnit source){Summon(seat,source.CardId,initialize:copy=>{int id=copy.Id;BattleUnitCopies.Copy(source,copy);copy.Id=id;copy.Owner=copy.OriginalOwner=seat;copy.SummonTurn=Turn;copy.AttacksUsed=0;copy.AuraHealth=0;copy.ReturnSeat=copy.ReturnTurn=-1;});}
 public bool SecretBeforeAttack(int attackerSeat,int attackerId,ref MatchTarget target){int defenderSeat=1-attackerSeat;foreach(var s in SecretQueue(defenderSeat)){if(!SecretPresent(s))continue;if(Finished||attackerId!=0&&Unit(new MatchTarget(attackerSeat,attackerId))==null)return false;bool ownTarget=target.Seat==defenderSeat;switch(Card(s.CardId).BaseId){
  case "EX1_130":if(ownTarget&&Players[defenderSeat].Board.Count<Mode.BoardSlots){RevealSecret(s);var unit=Summon(defenderSeat,"GAME_DEFENDER");target=new MatchTarget(defenderSeat,unit.Id);}break;
  case "EX1_289":if(ownTarget&&target.Hero){RevealSecret(s);GainGvgArmor(defenderSeat,8);}break;
  case "EX1_594":if(ownTarget&&target.Hero&&attackerId!=0){RevealSecret(s);var unit=Unit(new MatchTarget(attackerSeat,attackerId));unit.DamageTaken=unit.MaxHealth;Cleanup();return false;}break;
  case "EX1_554":if(ownTarget&&!target.Hero&&Players[defenderSeat].Board.Count<Mode.BoardSlots){RevealSecret(s);for(int n=0;n<3;n++)Summon(defenderSeat,"GAME_SNAKE");}break;
  case "EX1_611":if(attackerId!=0){RevealSecret(s);BounceRogueUnits(new[]{Unit(new MatchTarget(attackerSeat,attackerId))},2);return false;}break;
  case "EX1_610":if(ownTarget&&target.Hero){RevealSecret(s);DamageBatch(()=>{foreach(var t in Characters(attackerSeat))Damage(t,2);});Cleanup();}break;
  case "EX1_533":if(ownTarget&&target.Hero){var originalTarget=target;var candidates=Characters(0).Concat(Characters(1)).Where(t=>!(t.Seat==attackerSeat&&t.UnitId==attackerId)&&!(t.Seat==originalTarget.Seat&&t.UnitId==originalTarget.UnitId)).ToArray();if(candidates.Length>0){RevealSecret(s);target=RandomTarget(candidates);}}break;
 }}return !Finished&&(attackerId==0||Unit(new MatchTarget(attackerSeat,attackerId))!=null)&&(target.Hero||Unit(target)!=null);}
 public bool SecretPreHeroDamage(int seat,int amount){if(Players[seat].SecretImmuneTurn==Turn)return true;foreach(var s in SecretQueue(seat)){if(Card(s.CardId).BaseId=="EX1_295"&&amount-Players[seat].Armor>=Players[seat].Health){RevealSecret(s);Players[seat].SecretImmuneTurn=Turn;return true;}}return false;}
 public void SecretHeroDamaged(int seat,int amount){foreach(var s in SecretQueue(seat)){if(Card(s.CardId).BaseId=="EX1_132"){RevealSecret(s);Damage(new MatchTarget(1-seat),amount);}}}
 public void SecretMinionDied(BattleUnit dead,SecretInstance[] eligible=null){if(Active==dead.Owner)return;foreach(var s in (eligible??SecretQueue(dead.Owner)).Where(s=>s.Seat==dead.Owner).ToArray()){if(!SecretPresent(s))continue;switch(Card(s.CardId).BaseId){
  case "EX1_136":if(Players[dead.Owner].Board.Count>=Mode.BoardSlots)break;RevealSecret(s);var board=Players[dead.Owner].Board;int at=board.FindIndex(u=>u.Id==dead.DeathAnchor);Summon(dead.Owner,dead.CardId,at<0?board.Count:at,beforeTriggers:unit=>unit.DamageTaken=unit.MaxHealth-1);break;
  case "FP1_020":var target=RandomTarget(Characters(dead.Owner,false));if(target!=null){RevealSecret(s);Buff(target,3,2);}break;
  case "FP1_018":RevealSecret(s);GiveNaxxCard(dead.Owner,dead.CardId);GiveNaxxCard(dead.Owner,dead.CardId);break;
 }}}
}
}
