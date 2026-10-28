# MKei Source Handoff

**Updated:** 2026-09-30  
**Project:** MKei / Mono APP  
**Platform:** Windows 11  
**Stack:** C#, WinForms, .NET 8, NAudio 2.2.1

## Project purpose

MKei provides Windows audio device capture and playback with mono processing, alongside a per-application audio routing interface. The source namespace is `MKei`. Treat the current source files as authoritative if older notes disagree.

## Project configuration

- Project: `MKei.csproj`
- Target: `net8.0-windows10.0.22621.0`
- Output: Windows GUI application (`WinExe`)
- App icon: `mono.ico` (`ApplicationIcon`)
- Audio dependency: NAudio 2.2.1
- Build: `dotnet build MKei.csproj`
- Publish helper: `build.ps1` (inspect its current parameters before publishing)

## Main source files

- `MainForm.cs`: capture/playback lifecycle, mono downmix, volume, tray behavior, and close preference.
- `MainForm_Designer.cs`: main window layout and controls.
- `UiControls.cs`: themed controls and ComboBox styling.
- `AppRouterForm.cs`: running application/audio-session UI and per-app routing controls.
- `AudioRouter.cs`, `AudioPolicyConfig.cs`: routing/policy implementation.
- `Processloopbackcapture.cs`: process loopback capture implementation.
- `Theme.cs`, `WindowChrome.cs`: visual theme and window chrome.
- `Program.cs`: WinForms startup.

## Latest completed UI changes

- The input and output device ComboBoxes now use native WinForms popup painting. Their previous owner-drawn rows were associated with text flicker while moving the pointer across the dropdown list.
- A Settings button appears below the in-window heading. Its menu lets the user choose what the window close button does:
  - **Minimize to system tray** (default)
  - **Fully close MKei**
- The selected preference is stored at `HKCU\Software\MKei`, DWORD `MinimizeToTray` (`1` = hide to tray; `0` = exit on user close).
- The form uses the executable's associated icon. The project embeds `mono.ico` as the application icon. The Windows title bar caption is blank; the in-window heading remains “MKei.”
- The main form was made taller and its content card moved down to fit Settings.

## Build status

On 2026-09-30, `dotnet build MKei.csproj` succeeded with **0 errors**. Existing nullable-reference and SDK warnings remain. The initial sandbox build could not read the user NuGet configuration; the build succeeded when run with approved access.

No runtime UI test was performed for this change. Confirm these behaviors when launching the app:

1. Dropdown text remains visible while moving the pointer across device items.
2. Settings changes remain selected after restarting MKei.
3. Close-to-tray hides the main window and the tray Open action restores it.
4. Fully close exits the application and stops audio.
5. The title bar shows the `mono.ico` icon and no “MKei” caption.

## Existing implementation notes

- `MainForm.IsCaptureRunning` indicates whether the capture/playback loop is active.
- Closing to tray is limited to `CloseReason.UserClosing`; selecting Fully close allows the form to close normally.
- The tray menu provides Open and Exit. Exit sets the internal closing flag to bypass hide-to-tray behavior.
- Main capture/playback uses NAudio `WasapiCapture`, `WasapiOut`, and `BufferedWaveProvider`.
- App routing/session enumeration has had prior NAudio collection and process-ID type issues; consult the current `AppRouterForm.cs` before changing that code.

## Next work

Perform the runtime checks above on the target Windows installation. Continue audio-session and per-process routing work only after reviewing current source and confirming the app starts. Keep routing, startup, and UI changes isolated where practical, and build after meaningful changes.
