param([string]$Model='work/value-v1')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$core=Get-Content (Join-Path $root 'tests/core-sources.json') -Raw | ConvertFrom-Json
$sources=@($core | ForEach-Object {Join-Path $root "src/$_"})
$exe=Join-Path $root 'work/ValueChecks.exe'
& $compiler /nologo /target:exe /optimize+ "/out:$exe" /reference:System.Web.Extensions.dll @sources (Join-Path $root 'tools/Value/ValueChecks.cs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& $exe $root ([IO.Path]::GetFullPath($Model))
exit $LASTEXITCODE


