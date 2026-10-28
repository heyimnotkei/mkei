# MKei

MKei is a Windows audio app for capturing and playing audio with mono processing, and routing audio from individual applications. The current app version is **MKei 3.1.0**.

## Requirements

- Windows 11, 64-bit. Per-app audio routing requires Windows build 21390 or later.
- [VB-AUDIO Virtual Cable](https://vb-audio.com/Cable/index.htm) for per-app routing.
- The published MKei executable is self-contained; a separate .NET runtime installation is not required.

## Install

1. Download VB-CABLE from the [official VB-Audio page](https://vb-audio.com/Cable/index.htm).
2. Extract the downloaded archive, run its setup program as Administrator, install the driver, and restart Windows.
3. Download the latest `MKei <10-digit-number>.exe` from the [GitHub Releases page](https://github.com/heyimnotkei/mkei/releases) and run it. If no binary is published there yet, build it from source using the steps below.
4. In MKei, choose **CABLE Output** as the input and your speakers or headphones as the output, then click **Start**.
5. Open **Current Apps**, select an app with an active audio session, and choose **Toggle Routing**. App audio is sent to **CABLE Input** and captured by MKei through **CABLE Output** for playback on your selected output.

VB-CABLE appears as two Windows audio devices: **CABLE Input** is a playback device, and **CABLE Output** is a recording device. It forwards audio sent to its input to its output.

## Build from source

Requirements: Windows and the .NET 8 SDK.

From the repository folder, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The script can also be started by its full path from another directory.

The script publishes a self-contained Windows x64 executable to `publish/` and archives every successful build in `releases/` as `MKei <10-digit-number>.exe`. Pass `-BuildId` to replace a specific archive, for example `powershell -ExecutionPolicy Bypass -File .\build.ps1 -BuildId 1983527565`. The `releases/` folder is ignored by Git.

## Source

Repository: [heyimnotkei/mkei](https://github.com/heyimnotkei/mkei).
