using System;
using System.Linq;
namespace Touchfish {
public sealed partial class MatchEngine {
 bool tournamentPowerDrawing;
 internal int TournamentPowerDamage(int seat,int amount){
  return (amount+Players[seat].Board.Count(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="AT_003"))*AbilityMultiplier(seat);
 }
 internal void TournamentPowerDraw(int seat){
  tournamentPowerDrawing=true;
  try{Draw(seat,1);}finally{tournamentPowerDrawing=false;}
 }
 internal void ApplyTournamentDraw(int seat,HandCard hand){
  string baseId;int stats;
  if(TryReadTournamentBuff(hand.CardId,out baseId,out stats)){hand.CardId=baseId;hand.AttackBonus+=stats;hand.HealthBonus+=stats;}
  hand.CostAdjustment-=Players[seat].Board.Count(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="AT_014");
  if(tournamentPowerDrawing&&TournamentRules.Has(this,seat,"AT_027"))hand.CostAdjustment=-Card(hand.CardId).Cost;
 }
 internal void ResolveTournamentDraw(int seat,HandCard hand){
  if(!MulliganComplete||Card(hand.CardId).BaseId!="AT_130")return;
  Log.Add(PlayerLabel(seat)+" · 触发："+Card(hand.CardId).Name);
  DamageBatch(()=>{foreach(var target in Characters(seat,false).ToArray())Damage(target,1);});
  Cleanup();
 }
 public static bool TryReadTournamentBuff(string id,out string baseId,out int amount){
  baseId=id;amount=0;
  if(id==null||!id.StartsWith("TGT_BUFF:"))return false;
  var parts=id.Split(new[]{':'},3);
  if(parts.Length!=3||!Int32.TryParse(parts[1],out amount)||amount<1)throw new InvalidOperationException("牌库增益数据无效。");
  baseId=parts[2];return true;
 }
 internal void ApplyMistcaller(int seat){
  var p=Players[seat];
  foreach(var hand in p.Hand.Where(h=>Card(h.CardId).Type=="MINION")){hand.AttackBonus++;hand.HealthBonus++;}
  for(int n=0;n<p.Deck.Count;n++){
   if(Card(p.Deck[n]).Type!="MINION")continue;
   string id;int count;TryReadTournamentBuff(p.Deck[n],out id,out count);
   p.Deck[n]="TGT_BUFF:"+(count+1)+":"+id;
  }
  NoteHistory(HistoryKind.Unmodeled,seat);
 }
 internal void TournamentDiscard(int seat,HandCard discarded){
  foreach(var u in Players[seat].Board.Where(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="AT_021").ToArray())Buff(new MatchTarget(seat,u.Id),1,1);
  if(Card(discarded.CardId).BaseId=="AT_022")Damage(RandomTarget(Characters(1-seat)),4);
 }
 internal void TournamentAttack(BattleUnit attacker,MatchTarget target){
  if(!attacker.Silenced&&Card(attacker.CardId).BaseId=="AT_031"&&target.Hero)GiveGvgCard(attacker.Owner,"GAME_COIN");
  for(int s=0;s<2;s++){int count=attacker.Enchantments.Counter("tgt-glory-"+s);for(int n=0;n<count;n++)RestoreHealth(s,new MatchTarget(s),4);}
 }
 internal void ElekkJoust(int seat){
  int winner;
  var positions=TournamentRules.RevealJoust(this,seat,out winner);
  if(winner==seat)DrawFromDeck(seat,1,positions[seat]);
 }
 internal void PullTournamentSecrets(int seat){
  foreach(var id in Players[seat].Deck.Where(id=>Has(Card(id),"SECRET")).Distinct().ToArray()){
   if(SecretRequirement(seat,Card(id))!=null)continue;
   Players[seat].Deck.Remove(id);
   ArmSecret(seat,new HandCard{Id=NewEffectInstanceId(),CardId=id},true);
   NotifySecretFromDeck(seat,id);
  }
 }
 internal void TournamentTurnStart(int seat){
  foreach(var secret in secrets.Where(s=>s.Seat==seat&&Card(s.CardId).BaseId=="AT_073").ToArray()){
   if(Players[seat].Board.Count==0)continue;
   RevealSecret(secret);foreach(var unit in Players[seat].Board.ToArray())Buff(new MatchTarget(seat,unit.Id),1,1);
  }
 }
 internal void TournamentBearAfterAttack(MatchTarget target){
  if(!target.Hero||Finished)return;
  foreach(var secret in SecretQueue(target.Seat).Where(s=>Card(s.CardId).BaseId=="AT_060").ToArray()){
   if(Players[target.Seat].Board.Count>=Mode.BoardSlots)continue;
   RevealSecret(secret);Summon(target.Seat,"VAN_CS2_125");
  }
 }
}
}
