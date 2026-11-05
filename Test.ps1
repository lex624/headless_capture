$ErrorActionPreference='Stop'
$exe=Join-Path $env:LOCALAPPDATA 'ScreenshotToChat\ScreenshotChat.exe'
$assembly=[Reflection.Assembly]::LoadFrom($exe)
$type=$assembly.GetType('CaptureSchedule',$true)
$type.GetMethod('SelfTest').Invoke($null,@())
