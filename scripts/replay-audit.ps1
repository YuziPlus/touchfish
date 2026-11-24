param([string]$Replay=(Join-Path $env:LOCALAPPDATA 'YuziPlus/Touchfish/replays/20261113-170614-6fe47b25.jsonl'),[string]$Output='work/replay-6fe47b25',[string]$SeedCenter)
$root=Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force $Output|Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$src=@((Get-Content (Join-Path $root 'tests/core-sources.json') -Raw|ConvertFrom-Json)|ForEach-Object {Join-Path $root "src/$_"})
$exe=Join-Path ([IO.Path]::GetFullPath($Output)) 'ReplayAudit.exe'
& $csc /nologo /target:exe /optimize+ "/out:$exe" /reference:System.Web.Extensions.dll @src (Join-Path $root 'src/TestReplay.cs') (Join-Path $root 'tools/ReplayAudit/ReplayAudit.cs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
$extra=@();if($SeedCenter){$extra+=$SeedCenter}
& $exe $root $Replay ([IO.Path]::GetFullPath($Output)) @extra
exit $LASTEXITCODE
