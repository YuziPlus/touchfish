param(
 [string]$Config,
 [string]$Output,
 [switch]$Verify
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$folder=Join-Path $root 'tools/SelfPlay'
$bin=Join-Path $root 'work/self-play-bin'
New-Item -ItemType Directory -Force $bin | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$core=Get-Content (Join-Path $root 'tests/core-sources.json') -Raw | ConvertFrom-Json
$sources=@($core | ForEach-Object {Join-Path $root "src/$_"})+@(Join-Path $folder 'SelfPlay.cs')
$inputs=$sources+@(Get-ChildItem (Join-Path $root 'data') -File -Recurse | Sort-Object FullName | ForEach-Object FullName)
$sha=[Security.Cryptography.SHA256]::Create()
try {
 $hashes=($inputs | ForEach-Object {(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}) -join ''
 $fingerprint=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::ASCII.GetBytes($hashes))).Replace('-','').ToLowerInvariant()
} finally {$sha.Dispose()}
$exe=Join-Path $bin "SelfPlay-$fingerprint.exe"
if($Verify){
 $exe=Join-Path $bin ('Verify-'+[Guid]::NewGuid().ToString('N')+'.exe')
 $sources+=@(Join-Path $folder 'SelfPlayChecks.cs')+@(Join-Path $root 'tests/DynamicDecisionFixture.cs')
}
if($Verify -or !(Test-Path -LiteralPath $exe)){
 $temp=Join-Path $bin ([Guid]::NewGuid().ToString('N')+'.exe')
 $argsList=@('/nologo','/target:exe','/optimize+',('/out:'+$temp),'/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll')
 if($Verify){$argsList+='/define:SELFPLAY_VERIFY'}
 & $compiler @argsList @sources
 if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
 try {[IO.File]::Move($temp,$exe)} catch [IO.IOException] {if(!(Test-Path -LiteralPath $exe)){throw}; Remove-Item -LiteralPath $temp}
}
if($Verify){
 try {& $exe $root --verify; $result=$LASTEXITCODE} finally {Remove-Item -LiteralPath $exe}
 exit $result
}
if(!$Config){$Config=Join-Path $folder 'config.json'}
if(!$Output){$Output=Join-Path $root 'work/self-play'}
& $exe $root ([IO.Path]::GetFullPath($Config)) ([IO.Path]::GetFullPath($Output)) $fingerprint
exit $LASTEXITCODE
