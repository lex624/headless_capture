# Screenshot to Chat (Windows)

Double-click the desktop **Screenshot to Chat** shortcut, or `Launch.cmd`. The launcher builds the standalone Windows executable on first run using the .NET Framework compiler included with Windows. No administrator access is needed. Source is in `ScreenshotChat.cs`; rebuild with `Build.ps1` after closing the utility.

1. Choose a monitor. The non-primary monitor is selected by default.
2. Click **Select chat message box**, then click directly inside this Codex chat's composer within 30 seconds.
3. Return to your activity. Press and release **Ctrl+Shift+S** to capture, focus the saved composer, paste, wait 2.5 seconds, and press Enter.
4. The utility returns keyboard focus to the original activity. It does not move the pointer. It stays minimized; restore it from the taskbar to change settings.

Only one copy of the executable runs. Select the message box again after moving it, switching chats, or restarting the utility. Keep the destination chat open. Existing composer text is sent along with the image. Uncheck automatic paste/send for clipboard-only capture. An optional capture delay is available.

## Verified and limitations

A live 1920x1080 second-monitor image was captured, automatically pasted and submitted into this chat using the shortcut from a local window. Actual arrival was verified. The same synthetic shortcut inside the active full-screen Remote Desktop session was intercepted and did not trigger capture, including after keyboard-hook changes. Do not treat operation within Remote Desktop as verified. A physical-keyboard test may behave differently.

This implementation briefly switches focus to Codex to paste and send. It cannot guarantee uninterrupted focus in the source activity. Sending waits 2.5 seconds rather than inspecting upload readiness, so slow attachments may need manual submission. Windows focus or integrity restrictions can still prevent automated input; errors appear in the utility.

Screenshots replace clipboard contents and are not saved by the utility. The selected chat app handles uploads. PNG and bitmap clipboard formats are provided. Diagnostics record stages and monitor geometry (no screenshot pixels) in `%LOCALAPPDATA%\ScreenshotToChat\activity.log`. Windows packaged-app file redirection may place these files under the Codex package's LocalCache folder.
## Automatic capture

Set **Interval (seconds)** to 5, 10, or another value from 5 to 3600. Set **Stop after (minutes)** for an optional session timeout; 0 means run until stopped. The capture delay still applies to each capture. Busy captures are skipped without building a queue.

Default shortcuts:
- **Ctrl+Shift+F6** starts automatic capture, with an immediate first capture.
- **Ctrl+Shift+F7** stops automatic capture and cancels a pending capture/send.
- **Ctrl+Shift+S** remains a single capture.

Choose different F1-F12 keys in the Start/Stop dropdowns (Ctrl+Shift stays fixed). Start and stop must differ. Shortcut changes are disabled while running. The window minimizes during automatic mode. Its tray menu provides Show controls, Start automatic, Stop automatic, and Exit. Closing the utility stops capture. Automatic mode never starts on launch.

Select the monitor and chat composer after opening the updated app. With Paste/Enter checked, each capture is sent to that composer. Stopping cannot retract a message already sent; an image pasted before stopping may remain unsent. Changing chats or moving the composer requires selecting it again. The focus-switching and Remote Desktop shortcut limitations above still apply.

Validation: deterministic scheduler checks cover 5s/10s cadence, busy-skip, timeout, stop and restart. A live clipboard-only test captured the second monitor repeatedly at five-second intervals while minimized, then stopped via Ctrl+Shift+F7 from File Explorer. Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test.ps1` after building for scheduler checks.
## Window capture and stationary pointer (current version)

The **Capture source** dropdown now contains **Monitor:** and **Window:** entries. Use **Refresh** after opening a new window, then choose the browser window you want. This captures that window's bounds, so other windows alongside it are excluded. It captures a window, not a specific browser tab or selected rectangular region. Keep the source window open and not minimized. Windows rendering support varies: some accelerated/protected applications may return blank content; select a monitor if needed. There is no silent full-monitor fallback.

Capture, paste, sending and restoration no longer call mouse movement or click APIs. Select the destination message box once, leaving it focused for a moment so the utility can bind the actual editable field. Subsequent captures use Windows accessibility to focus that same field. Re-select it after changing chats or if the field is recreated. Unsupported/inaccessible inputs stop the operation with an error rather than falling back to a mouse click.

Keyboard focus still briefly moves to the chat and back. A stationary pointer does not make the tool invisible to monitoring software.

Live validation: a 933x826 File Explorer window image was captured and automatically received in the configured Codex chat using the new accessibility focus path. Its successful submission was verified from the attachment; capture/send no longer invokes pointer movement. Browser-specific window rendering remains dependent on the source browser. Automatic mode was left stopped.