param([string]$OutputName = 'Touchfish.exe',[switch]$Verification)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpfDir = Join-Path $frameworkDir 'WPF'
$compilerPath = Join-Path $frameworkDir 'csc.exe'
$buildArgs = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', ('/out:' + (Join-Path $taskRoot $OutputName)), ('/reference:' + (Join-Path $wpfDir 'PresentationFramework.dll')), ('/reference:' + (Join-Path $wpfDir 'PresentationCore.dll')), ('/reference:' + (Join-Path $wpfDir 'WindowsBase.dll')), '/reference:System.Xaml.dll', '/reference:System.Web.Extensions.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll','/reference:System.Xml.Linq.dll') + @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
if($Verification){$buildArgs+='/define:VERIFY';$buildArgs+=Join-Path $taskRoot 'tests/DynamicDecisionFixture.cs';$buildArgs+=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'tests/Wpf') -Filter '*.cs' | ForEach-Object {$_.FullName})}
& $compilerPath @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output ('Built ' + $OutputName)
if(!$Verification){
$updaterArgs=@('/nologo','/target:winexe','/optimize+','/platform:anycpu',('/out:'+(Join-Path $taskRoot 'Touchfish.Update.exe')),'/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Xml.Linq.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll',(Join-Path $taskRoot 'src\UpdateCore.cs'),(Join-Path $taskRoot 'updater\Program.cs'))
& $compilerPath @updaterArgs
if($LASTEXITCODE -ne 0){throw 'Updater build failed.'}
Write-Output 'Built Touchfish.Update.exe'
}
