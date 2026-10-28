# MKei

MKei is a Windows audio app for capturing and playing audio with mono processing, with an interface for routing audio from individual applications.

The current release is **MKei 3.0**.

## Build

Requirements: Windows and the .NET 8 SDK.

Run the publish script from the repository folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The script publishes a self-contained Windows x64 executable to `publish/` and copies each successful build to `releases/` with a unique timestamped filename. Existing release files are kept as build history.

## Source

This repository contains the MKei application source and build script: [heyimnotkei/mkei](https://github.com/heyimnotkei/mkei).
