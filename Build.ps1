$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'ScreenshotChat.cs'
$destination=Join-Path $env:LOCALAPPDATA 'ScreenshotToChat'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
& $compiler /nologo /target:winexe "/out:$destination\ScreenshotChat.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/reference:$wpf\UIAutomationClient.dll" "/reference:$wpf\UIAutomationTypes.dll" "/reference:$wpf\WindowsBase.dll" $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "$destination\ScreenshotChat.exe"
