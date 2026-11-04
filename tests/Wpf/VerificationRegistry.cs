using System;
using System.Collections.Generic;
namespace Touchfish {
public static class VerificationRun {public static int? FirstSeat=0;public static bool Network=true,Visual=true;}
public partial class PanelWindow {
 public void RunVerification(string scope,string folder){
  VerificationRun.Network=Environment.GetEnvironmentVariable("TOUCHFISH_VERIFY_NETWORK")!="0";VerificationRun.Visual=Environment.GetEnvironmentVariable("TOUCHFISH_VERIFY_VISUAL")!="0";
  var suites=new Dictionary<string,Action<string>>{
   {"gvg-class",PreviewGvgClassScope},{"gvg",PreviewGvgScope},{"naxx-cards",PreviewNaxxScope},{"secret-owner",PreviewSecretOwnerScope},{"sealed",PreviewSealedScope},{"miracle-ai",PreviewMiracleAiScope},{"card-sets",PreviewCardSetsScope},{"attack",PreviewAttackScope},{"effects-level",PreviewEffectsLevelScope},{"pyroblast",PreviewPyroblastScope},{"death-shatter",PreviewDeathShatterScope},{"zoo-ai",PreviewZooAiScope},{"ai",PreviewAiScope},{"names",PreviewNamesScope},{"dragons",PreviewDragonScope},{"high-cost",PreviewHighCostScope},{"mid-cost",PreviewMidCostScope},{"hunter",PreviewHunterScope},{"arena",PreviewArenaScope},{"low-cost",PreviewLowCostScope},{"heroes",PreviewHeroReplacementScope},{"paladin-shaman",PreviewPaladinShamanScope},{"mage",PreviewMageScope},{"priest",PreviewPriestScope},{"full",PreviewSuite},{"summon",PreviewSummonScope},{"turn-end",PreviewTurnEndScope},{"hero-visuals",PreviewHeroVisualScope},{"triggers",PreviewTriggerScope},{"deathrattle",PreviewDeathrattleScope},{"druid-spells",PreviewDruidSpellScope},{"emote-selection",PreviewEmoteSelectionScope},{"overload",PreviewOverloadScope},{"spell-cards",PreviewSpellCardsScope},{"opening-turn",PreviewOpeningTurnScope},{"deck-picker",PreviewDeckPickerScope},{"hero-freeze",PreviewHeroFreezeScope},{"combo",PreviewComboScope},{"warlock",PreviewWarlockScope},{"visual-polish",PreviewVisualPolishScope},{"graveyard",PreviewGraveyardScope},{"rogue",PreviewRogueScope},{"warrior",PreviewWarriorScope},{"secret-presentation",PreviewSecretPresentationScope},{"secrets",PreviewSecretScope},{"draw-visuals",PreviewDrawVisualScope}};
  Action<string> run;if(!suites.TryGetValue(scope,out run))throw new InvalidOperationException("未知检查模块："+scope);run(folder);
 }
}
}
