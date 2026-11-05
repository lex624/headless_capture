# Platform support

The current implementation is a **Windows desktop application**. macOS can store, review, and push this source repository, but cannot build or run the app natively.

Windows dependencies are built into Windows: Windows Forms, .NET Framework, Windows UI Automation, and user32 APIs. `Build.ps1` compiles the executable into `%LOCALAPPDATA%\ScreenshotToChat`. `Launch.cmd` builds on first use and launches it. Rebuild after modifying the source.

macOS support would require a native implementation for window/monitor capture, global shortcuts, clipboard images, accessibility focus, and a menu-bar UI. macOS Screen Recording and Accessibility permission would be required. No macOS implementation is included yet.

## Validation

On Windows, close the utility before rebuilding:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test.ps1
```

The scheduler checks cover five- and ten-second intervals, skipping busy captures, session timeout, stop, and restart. Live Windows tests verified monitor capture, window-only capture, and automatic image submission to Codex using accessibility focus without pointer movement. Other chat apps and source browsers must be validated individually.
