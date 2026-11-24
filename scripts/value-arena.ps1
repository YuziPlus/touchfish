param(
 [string]$Model='work/value-v1/value-v1.bin',
 [string]$Output='work/value-arena/smoke',
 [string]$Opponent='freeze',
 [int]$Begin=0,
 [int]$Count=4,
 [int]$Iterations=100,
 [string]$Modes='Heuristic,Shadow,Neural',
 [string]$EnemyDeck,
 [string]$OwnDeck
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$src=@((Get-Content (Join-Path $root 'tests/core-sources.json') -Raw|ConvertFrom-Json)|ForEach-Object {Join-Path $root "src/$_"})
$exe=Join-Path $root ('work/ValueArena-'+[Guid]::NewGuid().ToString('N')+'.exe')
& $compiler /nologo /target:exe /optimize+ "/out:$exe" /reference:System.Web.Extensions.dll @src (Join-Path $root 'tools/Value/ValueArena.cs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
$extra=@()
if($EnemyDeck){$extra+=$EnemyDeck}
if($OwnDeck){if(!$EnemyDeck){throw 'OwnDeck requires EnemyDeck'};$extra+=$OwnDeck}
& $exe $root $Model $Output $Opponent $Begin $Count $Iterations $Modes @extra
exit $LASTEXITCODE

