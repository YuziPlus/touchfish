param([string]$Output='work/policy-v1/budget-probe.json',[string]$Model='work/policy-v1/policy-v1.bin')
$root=Split-Path -Parent $PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$src=@((Get-Content (Join-Path $root 'tests/core-sources.json') -Raw|ConvertFrom-Json)|ForEach-Object {Join-Path $root "src/$_"})
$exe=Join-Path $root 'work/PolicyProbe.exe'
& $csc /nologo /target:exe /optimize+ "/out:$exe" /reference:System.Web.Extensions.dll @src (Join-Path $root 'tools/Policy/PolicyProbe.cs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& $exe $root $Output $Model
exit $LASTEXITCODE
