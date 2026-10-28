# Handoff

## Planned release: v3.1.1

- Bump the application version to `3.1.1` in the project and README.
- Build the Windows single-file executable and replace `releases/MKei 1983527565.exe`.
- Commit the routing refresh, Auto-refresh, persisted settings, and version changes; push the branch and `v3.1.1` tag to `origin`.
- Publish GitHub Release `v3.1.1` with the rebuilt executable.

## Completed

- Project and README version are `3.1.1`.
- Release executable built successfully in `publish-v3.1.1` and copied over `releases/MKei 1983527565.exe` (189,100,872 bytes; file version `3.1.1.0`). The default `publish/MKei.exe` was locked by the running app, so the versioned staging folder was used.
- Committed as `1b74e17` and pushed to `origin/main`; annotated tag `v3.1.1` is pushed.
- Published [GitHub Release v3.1.1](https://github.com/heyimnotkei/mkei/releases/tag/v3.1.1) with the 189,100,872-byte executable. GitHub normalized the release asset name to `MKei.1983527565.exe`.

## Completed

- Tray **Refresh** is enabled and restores/reapplies each active CABLE route; auto-refresh uses the same route operation without opening the router window.
- Added a persisted Auto-refresh toggle and hours/minutes interval. Mono, volume, input/output devices, and minimize-to-tray preferences are saved in the current user's MKei registry key.
- Restoring the router window brings the existing window forward.

## Verification

- `dotnet publish` Release win-x64 completed successfully; `publish/MKei.exe` is 189,100,872 bytes.
- `git diff --check` passed. Publish emitted existing nullable/SDK warnings and NU1900 because NuGet vulnerability metadata could not be reached.
- Release API verification confirmed the release is published, tagged `v3.1.1`, and contains the executable asset.

## Release scope

- User authorized committing and pushing the updates and publishing the `v3.1.1` GitHub release; completed.
