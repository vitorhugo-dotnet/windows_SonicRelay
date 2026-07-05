# SonicRelay Windows Publisher

SonicRelay Windows Publisher is the Windows desktop application responsible for capturing system audio and publishing it to SonicRelay viewers with low latency. It is one part of the SonicRelay suite and will communicate with the separately maintained backend at [`vitorhugo-java/dotnet_SonicRelay`](https://github.com/vitorhugo-java/dotnet_SonicRelay).

## Non-admin support

Installing, configuring, and running the Windows Publisher must not require administrator privileges. Normal usage must work in locked-down user environments without services, drivers, machine-wide dependencies, firewall changes, or writes to protected system locations. Every implementation and dependency decision must satisfy the [non-admin checklist](docs/non-admin-checklist.md).

## Current status

This repository contains the .NET 10 and WinUI 3 foundation, typed backend HTTP clients, and the authenticated WebSocket signaling client. WASAPI loopback capture and WebRTC/Opus publishing remain planned.

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

GitHub Actions runs CI for every pull request and every push to `main` on a Windows runner. The workflow uses the SDK selected by `global.json`, restores dependencies, builds the complete solution in Release configuration, runs all solution tests plus the repository structure test, and uploads available TRX test results as the `test-results` artifact.

Normal CI requires no repository secrets and does not publish or deploy artifacts. It only validates the existing non-admin application runtime requirements; it introduces no runtime dependency or elevation requirement for users.

The app is an unpackaged WinUI 3 executable. Select `SonicRelay.Windows.App` as the startup project when launching it from an IDE.

## Download a portable release

Open the repository's [Releases page](https://github.com/vitorhugo-java/windows_SonicRelay/releases), download `SonicRelay.WindowsPublisher-win-x64-<version>.zip`, and extract it to a user-writable folder such as one under your profile. Run `SonicRelay.Windows.App.exe` directly from the extracted folder. Do not run it as administrator.

Pushing a tag matching `v*` (for example, `v0.1.0`) builds, tests, and publishes a release for that tag. The release workflow can also be run manually and creates a versioned development release from the selected commit. Each ZIP is self-contained for Windows x64 and includes `BUILD-INFO.txt` with its version, commit SHA, and runtime target.

There is currently no MSIX or installer. The portable ZIP is the supported distribution format so normal use remains per-user and does not require elevation or a machine-wide installation.

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

See [the publisher specification](docs/windows-publisher.md), [architecture notes](docs/architecture.md), and [non-admin checklist](docs/non-admin-checklist.md) for the planned system boundaries.
