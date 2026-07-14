# SonicRelay Windows Publisher

SonicRelay Windows Publisher is the Windows desktop application responsible for capturing system audio and publishing it to SonicRelay viewers with low latency. It is one part of the SonicRelay suite and will communicate with the separately maintained backend at [`vitorhugo-java/dotnet_SonicRelay`](https://github.com/vitorhugo-java/dotnet_SonicRelay).

## Non-admin support

Installing, configuring, and running the Windows Publisher must not require administrator privileges. Normal usage must work in locked-down user environments without services, drivers, machine-wide dependencies, firewall changes, or writes to protected system locations. Every implementation and dependency decision must satisfy the [non-admin checklist](docs/non-admin-checklist.md).

## Current status

This repository contains the .NET 10 publisher: a cross-platform **Avalonia** desktop shell (Windows today, Linux later — issue #32), typed backend HTTP clients, the authenticated WebSocket signaling client, WASAPI loopback capture, and WebRTC/Opus publishing.

## Prerequisites

- Windows 10 version 1809 or newer
- .NET 10 SDK 10.0.301 or a compatible later feature band
- Visual Studio 2026 with Windows application development tools, or Rider with equivalent MSBuild tooling

## Build locally

```powershell
dotnet restore SonicRelay.Windows.slnx
dotnet build SonicRelay.Windows.slnx --no-restore
```

Run the focused tests with:

```powershell
dotnet test tests/SonicRelay.Windows.Core.Tests/SonicRelay.Windows.Core.Tests.csproj
dotnet test tests/SonicRelay.Windows.ApiClient.Tests/SonicRelay.Windows.ApiClient.Tests.csproj
dotnet test tests/SonicRelay.Windows.Signaling.Tests/SonicRelay.Windows.Signaling.Tests.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Repository.Structure.Tests.ps1
```

## Continuous integration

GitHub Actions runs CI for every pull request, every push to `main`, every `v*` tag, and manual workflow dispatches. The workflow uses the SDK selected by `global.json`, restores dependencies, builds the complete solution in Release configuration, runs the repository structure test, runs all solution tests, and uploads available TRX test results as the `test-results` artifact.

After build and tests pass on non-PR runs, the workflow publishes Windows x64 release assets:

- `SonicRelay.WindowsPublisher-win-x64-<version>.zip`: portable folder distribution.
- `SonicRelay.WindowsPublisher-win-x64-<version>.exe`: portable single-file executable.
- `SonicRelay.WindowsPublisher-win-x64-<version>.msi`: per-user MSI installer under the current user's LocalAppData path, with a Start Menu shortcut for launching the app.
- `checksums-sha256.txt`: SHA-256 checksums for the release packages.

Package artifacts are uploaded back to the workflow run. Pushing a tag matching `v*` (for example, `v0.1.0`) or running the workflow manually also publishes those assets to GitHub Releases. Manual runs without a version create a prerelease tag named `dev-<run-number>`.

The separate **Release** workflow (`.github/workflows/release.yml`) publishes on `v*` tags and can also be dispatched manually. On a manual run you can set the optional `pr_number` input: the workflow then checks out **that pull request's head**, runs the full build and tests, and publishes the same ZIP/EXE/MSI assets as a **prerelease** tagged `v0.0.0-alpha.pr<number>.<run-number>` targeting the PR's commit — an alpha test build straight from a PR, without merging. A manual run without `pr_number` builds the selected branch as a `v0.0.0-manual.<run-number>` prerelease.

The package flow keeps the app unpackaged and per-user. It does not introduce services, drivers, firewall changes, machine-wide writes, or an administrator requirement for normal usage. The generated packages are currently unsigned.

The app is an unpackaged Avalonia executable. Select `SonicRelay.Windows.Desktop` as the startup project when launching it from an IDE.

## Download a release

Open the repository's [Releases page](https://github.com/vitorhugo-java/windows_SonicRelay/releases) and download the asset that matches how you want to run it:

- ZIP: extract it to a user-writable folder such as one under your profile and run `SonicRelay.Windows.App.exe` directly. Do not run it as administrator.
- EXE: run the portable single-file executable directly as the current user.
- MSI: install it as the current user, then launch `SonicRelay Windows Publisher` from the Start Menu. The installed files live under `%LOCALAPPDATA%\SonicRelay\WindowsPublisher`.

Before approving a release, run the [non-admin release smoke test](docs/release-smoke-test.md) from a clean standard-user environment. Every mandatory item is a release gate.

## User configuration and tokens

On first launch, the publisher creates editable configuration at `%LOCALAPPDATA%\SonicRelay\WindowsPublisher\appsettings.json`. Backend and signaling addresses must be absolute HTTP(S) or WebSocket URLs, and `defaultMaxViewers` must be greater than zero.

Authentication tokens are stored for the current user at `%LOCALAPPDATA%\SonicRelay\WindowsPublisher\tokens.dat` and protected with Windows DPAPI `CurrentUser`. If DPAPI is unavailable, token operations return a secure-storage error and no plaintext fallback is written. Neither configuration nor token storage requires administrator privileges.

## Backend HTTP client

The configured `backendBaseUrl` is used as the `HttpClient.BaseAddress`; no production address is compiled into the application. The typed clients implement login and refresh under `/auth`, current-user lookup, `windows_publisher` device registration/listing, and stream-session creation/active-list/end operations.

Authenticated requests load the current user's DPAPI-protected bearer token. A `401` with an available refresh token causes one refresh request and one retry, and the replacement tokens are saved back to the user-scoped store. HTTP, network, and backend failures are exposed as typed API errors. This layer uses outbound HTTP(S) only and requires no administrator privileges.

## WebSocket signaling client

The configured `signalingBaseUrl` is converted to WS(S) when needed and receives escaped `sessionId` and `deviceId` query parameters. The outbound handshake uses the current user-scoped bearer token. On connection the client sends `publisher.ready`, validates and dispatches supported control envelopes, answers `ping` with `pong`, and exposes connection/reconnection state.

Unexpected transport failures use a conservative 1/2/4-second reconnect sequence. Explicit closure, normal remote closure, cancellation, and `session.ended` close cleanly without reconnecting. Only one active connection is allowed for a session/device identity, while `viewerId` remains on each envelope for future per-viewer routing. SDP and ICE payloads are redacted from safe diagnostic output.

The WebSocket carries signaling control messages only. It does not carry audio; future audio transport belongs to one WebRTC connection per viewer. The client initiates outbound connections only, opens no local server port, changes no firewall rule, and requires no administrator privileges.

## Planned milestones

1. Repository and Windows application bootstrap.
2. Backend authentication and publisher-device registration.
3. Stream session lifecycle and WebSocket signaling.
4. WASAPI loopback capture and audio pipeline.
5. WebRTC/Opus publication with one peer connection per viewer.
6. Reliability, diagnostics, packaging, and release automation.

The desktop shell is a shared cross-platform Avalonia UI app (Windows today, Linux later) — see [the Avalonia desktop shell notes](docs/avalonia-desktop-shell.md) (issue #32). It replaced the original WinUI 3 shell once it reached functional parity.

See [the publisher specification](docs/windows-publisher.md), [architecture notes](docs/architecture.md), [non-admin checklist](docs/non-admin-checklist.md), and [release smoke test](docs/release-smoke-test.md) for the planned system boundaries and release gates.
