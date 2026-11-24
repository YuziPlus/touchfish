param([string]$Output='work/p531',[switch]$CheckModel)
$root=Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force $Output | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$src=@((Get-Content (Join-Path $root 'tests/core-sources.json') -Raw|ConvertFrom-Json)|ForEach-Object {Join-Path $root "src/$_"})
$exe=Join-Path ([IO.Path]::GetFullPath($Output)) 'TacticalProbe.exe'
& $csc /nologo /target:exe /optimize+ "/out:$exe" /reference:System.Web.Extensions.dll @src (Join-Path $root 'tools/Representation/TacticalProbe.cs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
if($CheckModel){
 & $exe $root (Join-Path $Output 'model-parity.json') (Join-Path $Output 'tactical-v2.bin')
}else{
 & $exe $root (Join-Path $Output 'scenarios.json')
}
exit $LASTEXITCODE
