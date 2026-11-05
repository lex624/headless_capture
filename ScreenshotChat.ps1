param([switch]$Check)
$ErrorActionPreference='Stop'
$exe=Join-Path $env:LOCALAPPDATA 'ScreenshotToChat\ScreenshotChat.exe'
if($Check -or -not (Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
if(-not $Check) { Start-Process -FilePath $exe }