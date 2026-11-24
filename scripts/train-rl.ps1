param([string]$Output='work/rl',[int]$Games=200,[string[]]$TrainingOptions=@())
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$src=@((Get-Content (Join-Path $root 'tests/core-sources.json') -Raw|ConvertFrom-Json)|ForEach-Object {Join-Path $root "src/$_"})
New-Item -ItemType Directory -Force $Output | Out-Null
$exe=Join-Path ([IO.Path]::GetFullPath($Output)) 'Rollout.exe'
& $csc /nologo /main:Rollout /target:exe /optimize+ "/out:$exe" /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll @src (Join-Path $root 'tools/RL/Rollout.cs') (Join-Path $root 'tools/SelfPlay/SelfPlay.cs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
python (Join-Path $root 'tools/RL/iterate.py') $root ([IO.Path]::GetFullPath($Output)) --games $Games @TrainingOptions
exit $LASTEXITCODE



