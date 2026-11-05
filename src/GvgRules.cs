using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchfish {
// GvG neutral effects. Random generation is bounded by the actual match's open card pool.
public static class GvgRules {
 public const string SetId="goblins-gnomes-2014";
 public static readonly string[] CardIds={"GVG_006","GVG_013","GVG_016","GVG_044","GVG_064","GVG_065","GVG_067","GVG_068","GVG_069","GVG_070","GVG_071","GVG_074","GVG_075","GVG_076","GVG_078","GVG_079","GVG_081","GVG_082","GVG_084","GVG_085","GVG_089","GVG_090","GVG_091","GVG_092","GVG_093","GVG_094","GVG_095","GVG_096","GVG_097","GVG_098","GVG_099","GVG_102","GVG_103","GVG_104","GVG_105","GVG_106","GVG_107","GVG_108","GVG_109","GVG_110","GVG_111","GVG_112","GVG_113","GVG_114","GVG_115","GVG_116","GVG_117","GVG_118","GVG_119","GVG_120","GVG_121"};
 public static readonly string[] Parts=Enumerable.Range(1,7).Select(n=>"PART_00"+n).ToArray();
 public static readonly string[] DeathIds={"GVG_076","GVG_078","GVG_082","GVG_096","GVG_105","GVG_110t","GVG_114","GVG_115"};
 public static void Register(MatchRules rules,CardCatalog catalog){
  foreach(string id in CardIds.Concat(new[]{"GVG_092t","GVG_110t","GVG_111t"}))rules.Register(id,"NONE",Apply);
  rules.Register("GVG_108","FRIENDLY_MINION",Apply);rules.Register("GVG_120","BEAST_MINION",Apply);
  foreach(string id in Parts)rules.Register(id,id=="PART_002"||id=="PART_004"?"FRIENDLY_MINION":"MINION",Apply);
  foreach(var trigger in Triggers)rules.RegisterTrigger(trigger);
 }
 static MatchTarget Self(MatchTriggerContext c){return new MatchTarget(c.Source.Owner,c.Source.Id);}
 public static readonly ConditionalTrigger[] Triggers={
  new ConditionalTrigger{CardId="GVG_016",Timing=MatchTriggerTiming.CardPlayed,Condition=c=>c.Seat!=c.Source.Owner,Apply=c=>c.Game.RemoveDeckTop(c.Source.Owner,3)},
  new ConditionalTrigger{CardId="GVG_067",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Seat!=c.Source.Owner,Apply=c=>c.Game.Buff(Self(c),1,0)},
  new ConditionalTrigger{CardId="GVG_068",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Seat!=c.Source.Owner,Apply=c=>c.Game.Buff(Self(c),2,0)},
  new ConditionalTrigger{CardId="GVG_075",Timing=MatchTriggerTiming.MinionSummoned,Condition=c=>c.Seat==c.Source.Owner&&c.Summoned!=null&&c.Summoned.Race=="PIRATE",Apply=c=>{var t=c.Game.RandomTarget(c.Game.Characters(1-c.Source.Owner));if(t!=null)c.Game.Damage(t,2);}},
  new ConditionalTrigger{CardId="GVG_089",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>c.Seat==c.Source.Owner&&c.Game.Players[c.Seat].SecretCount>0,Apply=c=>c.Game.RestoreHealth(c.Source.Owner,new MatchTarget(c.Source.Owner),4)},
  new ConditionalTrigger{CardId="GVG_094",Timing=MatchTriggerTiming.TurnEnd,Condition=c=>true,Apply=c=>c.Game.Draw(c.Seat,Math.Max(0,3-c.Game.Players[c.Seat].HandCount))},
  new ConditionalTrigger{CardId="GVG_103",Timing=MatchTriggerTiming.TurnStart,Condition=c=>true,Apply=c=>c.Game.Buff(Self(c),1,0)},
  new ConditionalTrigger{CardId="GVG_104",Timing=MatchTriggerTiming.MinionPlayed,Condition=c=>c.Seat==c.Source.Owner&&c.Card!=null&&c.Card.Type=="MINION"&&c.Card.Attack+(c.PlayedHand==null?0:c.PlayedHand.AttackBonus)==1&&c.Summoned!=null,Apply=c=>c.Game.Buff(new MatchTarget(c.Summoned.Owner,c.Summoned.Id),2,2)},
  new ConditionalTrigger{CardId="GVG_106",Timing=MatchTriggerTiming.MinionDied,Condition=c=>c.Died!=null&&c.Died.Owner==c.Source.Owner&&c.Died.Race=="MECHANICAL",Apply=c=>c.Game.Buff(Self(c),2,2)},
  new ConditionalTrigger{CardId="GVG_111",Timing=MatchTriggerTiming.TurnStart,Condition=c=>c.Seat==c.Source.Owner&&c.Game.Players[c.Seat].Board.Count(u=>u.Health>0&&u.Race=="MECHANICAL")>=3,Apply=c=>c.Game.CombineGvgMechs(c.Source)},
  new ConditionalTrigger{CardId="GVG_116",Timing=MatchTriggerTiming.MinionDied,Condition=c=>c.Died!=null&&c.Died.Owner!=c.Source.Owner,Apply=c=>c.Game.Summon(c.Source.Owner,"VAN_EX1_029")},
  new ConditionalTrigger{CardId="GVG_117",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Seat==c.Source.Owner&&c.PaidCost==1,Apply=c=>c.Game.GenerateGvgCard(c.Source.Owner,card=>card.Type=="MINION"&&card.Race=="MECHANICAL")},
  new ConditionalTrigger{CardId="GVG_118",Timing=MatchTriggerTiming.SpellCast,Condition=c=>c.Seat!=c.Source.Owner,Apply=c=>c.Game.Summon(c.Source.Owner,"GVG_068")}
 };
 public static void Apply(EffectContext c){var g=c.Game;switch(c.Card.BaseId){
  case "GVG_069":g.RestoreHealth(c.Seat,new MatchTarget(c.Seat),8);break;
  case "GVG_074":g.StealGvgSecret(c.Seat);break;
  case "GVG_090":for(int n=0;n<6&&!g.Finished;n++){var selected=g.RandomTarget(g.Characters(0).Concat(g.Characters(1)).Where(t=>t.Hero||c.Summoned==null||t.UnitId!=c.Summoned.Id));if(selected!=null)g.DamageSequenceStep(selected,1);}break;
  case "GVG_092":g.DrawGvgExperiment(c.Seat);break;
  case "GVG_097":if(c.Summoned!=null){int count=g.Players[1-c.Seat].Board.Count(u=>u.Health>0&&HasDeathrattle(g,u));g.Buff(new MatchTarget(c.Seat,c.Summoned.Id),count,count);}break;
  case "GVG_099":var enemy=g.RandomTarget(g.Characters(1-c.Seat,false));if(enemy!=null)g.Damage(enemy,4);break;
  case "GVG_102":if(c.Summoned!=null&&g.Players[c.Seat].Board.Any(u=>u.Id!=c.Summoned.Id&&u.Health>0&&u.Race=="MECHANICAL")){g.Buff(new MatchTarget(c.Seat,c.Summoned.Id),1,1);g.GiveGvgPart(c.Seat);}break;
  case "GVG_107":foreach(var u in g.Players[c.Seat].Board.Where(u=>u.Health>0&&(c.Summoned==null||u.Id!=c.Summoned.Id)).ToArray()){switch(g.RandomIndex(3)){case 0:u.Windfury=true;break;case 1:u.Taunt=true;break;case 2:u.Shield=true;break;}}break;
  case "GVG_108":var unit=g.LivingUnit(c.Target);if(unit!=null)g.TransformGvgRandom(c.Target,g.Card(unit.CardId).Cost);break;
  case "GVG_110":int at=c.Summoned==null?-1:g.Players[c.Seat].Board.IndexOf(c.Summoned);g.Summon(c.Seat,"GVG_110t",at);g.Summon(c.Seat,"GVG_110t",at<0?-1:at+2);break;
  case "GVG_115":g.GiveGvgPart(c.Seat);break;
  case "GVG_119":for(int seat=0;seat<2;seat++){var weapons=g.GenerationPool().Where(card=>card.Type=="WEAPON").ToArray();if(weapons.Length>0)g.Equip(seat,weapons[g.RandomIndex(weapons.Length)].Id);}break;
  case "GVG_120":var beast=g.LivingUnit(c.Target);if(beast!=null&&beast.Race=="BEAST")beast.DamageTaken=beast.MaxHealth;break;
  case "PART_001":g.Buff(c.Target,0,1);break;
  case "PART_002":g.ReturnToHand(c.Target);break;
  case "PART_003":var taunt=g.LivingUnit(c.Target);if(taunt!=null)taunt.Taunt=true;break;
  case "PART_004":var stealth=g.LivingUnit(c.Target);if(stealth!=null&&!stealth.Stealth){stealth.Stealth=true;stealth.StealthExpirySeat=c.Seat;stealth.StealthUntilTurn=g.Turn+2;}break;
  case "PART_005":g.Freeze(c.Target);break;
  case "PART_006":LowCostRules.Apply(new EffectContext{Game=g,Seat=c.Seat,Card=new CardRecord{BaseId="EX1_059"},Target=c.Target});break;
  case "PART_007":g.Buff(c.Target,1,0);break;
 }}
 public static bool HasDeathrattle(MatchEngine g,BattleUnit u){return !u.Silenced&&MatchEngine.Has(g.Card(u.CardId),"DEATHRATTLE")||u.ForestDeathrattles>0||u.AncestralSpirits>0;}
 public static void Death(MatchEngine g,BattleUnit dead){switch(g.Card(dead.CardId).BaseId){
  case "GVG_076":foreach(var t in g.Characters(0,false).Concat(g.Characters(1,false)).ToArray())g.Damage(t,2);break;
  case "GVG_078":g.GiveGvgPart(dead.Owner);g.GiveGvgPart(1-dead.Owner);break;
  case "GVG_082":case "GVG_115":g.GiveGvgPart(dead.Owner);break;
  case "GVG_096":g.SummonGvgRandom(dead,card=>card.Type=="MINION"&&card.Cost==2);break;
  case "GVG_105":g.SummonGvgRandom(dead,card=>card.Type=="MINION"&&card.Cost==4);break;
  case "GVG_114":g.SummonGvgRandom(dead,card=>card.Type=="MINION"&&card.Rarity=="LEGENDARY");break;
  case "GVG_110t":var target=g.RandomTarget(g.Characters(1-dead.Owner));if(target!=null)g.Damage(target,1+g.RandomIndex(4));break;
 }}
}
public sealed partial class MatchEngine {
 public string CardPoolId="classic-2014";
 public CardRecord[] GenerationPool(){return Catalog.Deckable(Catalog.Pool(CardPoolId)).Where(Rules.Supports).ToArray();}
 public void GiveGvgPart(int seat){GiveNaxxCard(seat,GvgRules.Parts[random.Next(GvgRules.Parts.Length)]);}
 public void GenerateGvgCard(int seat,Func<CardRecord,bool> filter){var cards=GenerationPool().Where(filter).ToArray();if(cards.Length>0)GiveNaxxCard(seat,cards[random.Next(cards.Length)].Id);}
 public void RemoveDeckTop(int seat,int count){var p=Players[seat];for(int n=0;n<count&&p.Deck.Count>0;n++){string id=p.Deck[0];p.Deck.RemoveAt(0);RecordGraveyard(seat,seat,id,nextId++,GraveyardReason.RemovedFromDeck);Log.Add(PlayerLabel(seat)+" · 移除牌库卡牌："+Card(id).Name);}}
 public void DrawGvgExperiment(int seat){var p=Players[seat];int previous=nextId;Draw(seat,1);var drawn=p.Hand.FirstOrDefault(h=>h.Id>=previous);if(drawn!=null&&Card(drawn.CardId).Type=="MINION"){drawn.CardId="GVG_092t";drawn.CostAdjustment=0;drawn.Generated=true;}}
 public void TransformGvgRandom(MatchTarget target,int cost){var choices=GenerationPool().Where(c=>c.Type=="MINION"&&c.Cost==cost).ToArray();if(choices.Length>0)Transform(target,choices[random.Next(choices.Length)].Id);}
 public void SummonGvgRandom(BattleUnit dead,Func<CardRecord,bool> filter){if(Players[dead.Owner].Board.Count>=Mode.BoardSlots)return;var choices=GenerationPool().Where(filter).ToArray();if(choices.Length==0)return;int at=Players[dead.Owner].Board.FindIndex(u=>u.Id==dead.DeathAnchor);Summon(dead.Owner,choices[random.Next(choices.Length)].Id,at<0?Players[dead.Owner].Board.Count:at);}
 public void CombineGvgMechs(BattleUnit source){int seat=source.Owner;var all=Players[seat].Board.Where(u=>u.Health>0&&u.Race=="MECHANICAL").ToArray();if(all.Length<3)return;int at=Players[seat].Board.IndexOf(source);foreach(var mech in all)mech.DamageTaken=mech.MaxHealth;Cleanup();if(!Finished)Summon(seat,"GVG_111t",at);}
 public void StealGvgSecret(int seat){if(Players[seat].SecretCount>=5)return;var enemy=secrets.Where(s=>s.Seat==1-seat).ToArray();if(enemy.Length==0)return;var chosen=enemy[random.Next(enemy.Length)];if(SecretRequirement(seat,Card(chosen.CardId))!=null)return;chosen.Seat=seat;for(int n=0;n<2;n++)Players[n].SecretCount=secrets.Count(s=>s.Seat==n);EmitVisual("secret-stolen",seat,name:Card(chosen.CardId).Name);var notice=VisualEvents[VisualEvents.Count-1];notice.CardId=chosen.CardId;notice.RecipientSeat=seat;Log.Add(PlayerLabel(seat)+" · 夺取一张敌方奥秘");}
 public int MinionAttackLimit(BattleUnit unit){return !unit.Silenced&&Card(unit.CardId).BaseId=="GVG_111t"?4:HasWindfury(unit)?2:1;}
 public int GvgAttackBonus(BattleUnit unit){if(unit.Silenced)return 0;switch(Card(unit.CardId).BaseId){case "GVG_013":return Players[unit.Owner].Board.Any(u=>u.Health>0&&u.Race=="MECHANICAL")?2:0;case "GVG_095":return Players[1-unit.Owner].HandCount>=6?4:0;default:return 0;}}
 MatchTarget GvgAttackTarget(int seat,BattleUnit attacker,MatchTarget intended){bool ogre=attacker!=null&&(!attacker.Silenced&&new[]{"GVG_065","GVG_066","GVG_088"}.Contains(Card(attacker.CardId).BaseId)||Players.SelectMany(p=>p.Board).Any(u=>u.Health>0&&!u.Silenced&&Card(u.CardId).BaseId=="GVG_112"));ogre|=attacker==null&&Players[seat].WeaponId!=null&&Card(Players[seat].WeaponId).BaseId=="GVG_054";if(!ogre||random.Next(2)!=0)return intended;var others=Characters(1-seat).Where(t=>t.Seat!=intended.Seat||t.UnitId!=intended.UnitId).ToArray();return others.Length==0?intended:others[random.Next(others.Length)];}
 MatchTarget[] GvgCleaveTargets(BattleUnit attacker,MatchTarget target){if(attacker==null||attacker.Silenced||Card(attacker.CardId).BaseId!="GVG_113"||target.Hero)return new MatchTarget[0];var board=Players[target.Seat].Board;int i=board.FindIndex(u=>u.Id==target.UnitId);return new[]{i>0?board[i-1]:null,i>=0&&i+1<board.Count?board[i+1]:null}.Where(u=>u!=null&&u.Health>0).Select(u=>new MatchTarget(u.Owner,u.Id)).ToArray();}
}
}
