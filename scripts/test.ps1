param([string]$Scope='auto',[ValidateSet('rules','network','ui','all')][string]$Layer='rules',[switch]$PlanOnly)
$ErrorActionPreference='Stop'
$Scope=$Scope -replace '\s+',','
$taskRoot=Split-Path -Parent $PSScriptRoot
$testRoot=Join-Path $taskRoot 'work/test-bin'
$ruleNames=@('gvg-1','gvg-2','gvg-3','gvg','secret-owner','naxx-cards','sealed','naxx-library','miracle-ai','target-resolution','effects-level','pyroblast','zoo-ai','ai','names','dragons','high-cost','mid-cost','hunter','arena','low-cost','heroes','paladin','shaman','mage','priest','opening','shadowstep','warlock','druid-spells','triggers','engine','library','modes','enchantments','privacy','resolution','warrior','secrets','rogue','deathrattle','combo','overload')
$wpfNames=@('gvg-class','gvg','secret-owner','naxx-cards','sealed','miracle-ai','card-sets','attack','effects-level','pyroblast','death-shatter','zoo-ai','ai','names','dragons','high-cost','mid-cost','hunter','arena','low-cost','heroes','paladin-shaman','mage','priest','full','summon','turn-end','hero-visuals','triggers','deathrattle','druid-spells','emote-selection','overload','spell-cards','opening-turn','deck-picker','hero-freeze','combo','warlock','visual-polish','graveyard','rogue','warrior','secret-presentation','secrets','draw-visuals')
if($Scope -eq 'auto'){
 $paths=@(git -C $taskRoot diff --name-only HEAD)+@(git -C $taskRoot ls-files --others --exclude-standard)
 $selected=@();$selectedWpf=@()
 foreach($path in $paths){
  if($path -notmatch '^(src/|tests/|data/|scripts/test.ps1|build.ps1)'){continue}
  if($path -match '(?i)(Gvg|goblins)'){$selected+=@('gvg','gvg-1','gvg-2','gvg-3');$selectedWpf+=@('gvg','gvg-class');continue}
  if($path -match '(?i)(SecretUi|SecretOwner)'){$selected+='secret-owner';$selectedWpf+='secret-owner';continue}
  if($path -match '(?i)Sealed'){$selected+='sealed';$selectedWpf+='sealed';continue}
  if($path -match '(?i)(NaxxRules|NaxxCardChecks)'){$selected+='naxx-cards';$selectedWpf+='naxx-cards';continue}
  if($path -match '(?i)Naxx'){$selected+='naxx-library';$selectedWpf+='card-sets';continue}
  if($path -match '(?i)Miracle'){$selected+='miracle-ai';$selectedWpf+='miracle-ai';continue}
  if($path -match '(?i)AttackEffects'){$selected+='engine';$selectedWpf+='attack';continue}
  if($path -match '(?i)TargetResolution'){$selected+='target-resolution';$selectedWpf+='mid-cost';continue}
  if($path -match '(?i)(EffectSettings|EffectLevel)'){$selected+='effects-level';$selectedWpf+='effects-level';continue}
  if($path -match '(?i)(Pyroblast|SpellAnimationRules)'){$selected+='pyroblast';$selectedWpf+='pyroblast';continue}
  if($path -match '(?i)(DeathShatter|MatchVisualEvents|MatchVisualEffects)'){$selected+=@('engine','deathrattle');$selectedWpf+='death-shatter';continue}
  if($path -match "(?i)(ZooWarlock|ZooAi)"){$selected+='zoo-ai';$selectedWpf+='zoo-ai';continue}
  if($path -match "(?i)MatchAgent"){$selected+=@('ai','zoo-ai','miracle-ai');$selectedWpf+=@('ai','zoo-ai','miracle-ai');continue}
  if($path -match "(?i)FreezeMage"){$selected+='ai';$selectedWpf+='ai';continue}
  $mapped=$false
  foreach($name in @('ai','names','dragons','high-cost','mid-cost','hunter','arena','low-cost','heroes','paladin','shaman','mage','priest','warrior','secrets','rogue','deathrattle','combo','overload','warlock','druid-spells','triggers')){if($path -match "(?i)$($name -replace 'low-cost','low[-]?cost' -replace 'mid-cost','mid[-]?cost' -replace 'high-cost','high[-]?cost' -replace 'heroes','HeroReplacement' -replace '-spells$','' -replace 's$','')"){$selected+=$name;$selectedWpf+=if($name -in @('paladin','shaman')){'paladin-shaman'}else{$name};$mapped=$true}}
  if(!$mapped){$selected+=$ruleNames;$selectedWpf+='full'}
 }
 if($selected.Count -eq 0){$selected=@('engine','library')}
 $selected=@($selected|Select-Object -Unique)
 if($Layer -ne 'rules'){$selected=if($selectedWpf.Count -eq 0 -or 'full' -in $selectedWpf){@('full')}else{@($selectedWpf|Select-Object -Unique)}}
}else{$selected=@($Scope.Split(',')|Select-Object -Unique);$selectedWpf=$selected}
if($Scope -eq 'full' -and $Layer -eq 'rules'){$selected=@('all')}
Write-Output ("Test plan: layer=$Layer; modules="+($selected -join ','))
if($PlanOnly){return}
New-Item -ItemType Directory -Force -Path $testRoot|Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if($Layer -eq 'rules' -or $Layer -eq 'all'){
 $rules=if($Layer -eq 'all'){'all'}else{$selected -join ','}
 foreach($name in $rules.Split(',')){if($name -ne 'all' -and $name -notin $ruleNames){throw "Unknown rules module: $name"}}
 $ruleExe=Join-Path $testRoot 'Touchfish.RuleTests.exe'
 if($true){$sources=Get-Content (Join-Path $taskRoot 'tests/core-sources.json') -Raw|ConvertFrom-Json;$argsList=@('/nologo','/target:exe','/optimize+',('/out:'+$ruleExe),'/reference:System.Web.Extensions.dll')+@($sources|ForEach-Object {Join-Path $taskRoot ('src/'+$_)})+@(Join-Path $taskRoot 'tests/GvgChecks.cs')+@(Join-Path $taskRoot 'tests/GvgRuleChecks.cs')+@(Join-Path $taskRoot 'tests/SecretOwnerChecks.cs')+@(Join-Path $taskRoot 'tests/NaxxCardChecks.cs')+@(Join-Path $taskRoot 'tests/NaxxLibraryChecks.cs')+@(Join-Path $taskRoot 'tests/SealedRuleChecks.cs')+@(Join-Path $taskRoot 'tests/RuleTests.cs')+@(Join-Path $taskRoot 'tests/LegacyRuleChecks.cs')+@(Join-Path $taskRoot 'tests/PriestRuleChecks.cs')+@(Join-Path $taskRoot 'tests/MageRuleChecks.cs')+@(Join-Path $taskRoot 'tests/PaladinShamanRuleChecks.cs')+@(Join-Path $taskRoot 'tests/HeroReplacementRuleChecks.cs')+@(Join-Path $taskRoot 'tests/LowCostRuleChecks.cs')+@(Join-Path $taskRoot 'tests/HunterRuleChecks.cs')+@(Join-Path $taskRoot 'tests/MidCostRuleChecks.cs')+@(Join-Path $taskRoot 'tests/HighCostRuleChecks.cs')+@(Join-Path $taskRoot 'tests/DragonRuleChecks.cs')+@(Join-Path $taskRoot 'tests/PlayerNameRuleChecks.cs')+@(Join-Path $taskRoot 'tests/FreezeMageAiChecks.cs')+@(Join-Path $taskRoot 'tests/ZooAiChecks.cs')+@(Join-Path $taskRoot 'tests/PyroblastRuleChecks.cs')+@(Join-Path $taskRoot 'tests/EffectLevelChecks.cs')+@(Join-Path $taskRoot 'tests/TargetResolutionChecks.cs')+@(Join-Path $taskRoot 'tests/MiracleAiChecks.cs');& $compiler @argsList;if($LASTEXITCODE -ne 0){throw 'Rules test build failed'}}
 & $ruleExe $taskRoot $rules;if($LASTEXITCODE -ne 0){throw 'Rules checks failed'}
}
if($Layer -ne 'rules'){
 if($Layer -eq 'all'){$selected=@('gvg','full','effects-level','pyroblast','death-shatter','zoo-ai','ai','names','dragons','high-cost','mid-cost','hunter','arena','low-cost','heroes','paladin-shaman','mage','priest','warrior','secrets','rogue')}
 foreach($name in $selected){if($name -notin $wpfNames){throw "Unknown WPF module: $name"}}
 if($true){& (Join-Path $taskRoot 'build.ps1') -OutputName 'work/test-bin/Touchfish.Tests.exe' -Verification;Copy-Item -LiteralPath (Join-Path $taskRoot 'data') -Destination $testRoot -Recurse -Force};Get-ChildItem -LiteralPath (Join-Path $testRoot 'data/cardsets') -Filter '*.json' -File | Where-Object {-not (Test-Path -LiteralPath (Join-Path $taskRoot ('data/cardsets/'+$_.Name)))} | ForEach-Object {Remove-Item -LiteralPath $_.FullName}
 $oldNetwork=$env:TOUCHFISH_VERIFY_NETWORK;$oldVisual=$env:TOUCHFISH_VERIFY_VISUAL
 try{
  $env:TOUCHFISH_VERIFY_NETWORK=if($Layer -eq 'ui'){'0'}else{'1'};$env:TOUCHFISH_VERIFY_VISUAL=if($Layer -eq 'network'){'0'}else{'1'}
  foreach($name in $selected){$folder=Join-Path $taskRoot ('previews/tests/'+$Layer+'/'+$name);New-Item -ItemType Directory -Force -Path $folder|Out-Null;$process=Start-Process -FilePath (Join-Path $testRoot 'Touchfish.Tests.exe') -ArgumentList '--preview',$folder,'--scope',$name -WindowStyle Hidden -PassThru;$process.WaitForExit();if($process.ExitCode -ne 0){if(Test-Path (Join-Path $folder 'error.txt')){Get-Content (Join-Path $folder 'error.txt')};throw "$name failed"};Write-Output "PASS $Layer/$name"}
 }finally{$env:TOUCHFISH_VERIFY_NETWORK=$oldNetwork;$env:TOUCHFISH_VERIFY_VISUAL=$oldVisual}
}
