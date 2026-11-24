param([string]$Model='work/policy-v1')
$root=Split-Path -Parent $PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$src=@((Get-Content (Join-Path $root 'tests/core-sources.json') -Raw|ConvertFrom-Json)|ForEach-Object {Join-Path $root "src/$_"})
$exe=Join-Path $root 'work/PolicyCheck.exe'
& $csc /nologo /target:exe /optimize+ "/out:$exe" /reference:System.Web.Extensions.dll @src (Join-Path $root 'tools/Policy/PolicyCheck.cs') (Join-Path $root 'tests/DynamicDecisionFixture.cs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& $exe $root $Model
exit $LASTEXITCODE
