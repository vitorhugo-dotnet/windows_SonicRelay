# SonicRelay Windows Publisher

SonicRelay Windows Publisher is the Windows desktop application responsible for capturing system audio and publishing it to SonicRelay viewers with low latency. It is one part of the SonicRelay suite and will communicate with the separately maintained backend at [`vitorhugo-java/dotnet_SonicRelay`](https://github.com/vitorhugo-java/dotnet_SonicRelay).

## Non-admin support

Installing, configuring, and running the Windows Publisher must not require administrator privileges. Normal usage must work in locked-down user environments without services, drivers, machine-wide dependencies, firewall changes, or writes to protected system locations. Every implementation and dependency decision must satisfy the [non-admin checklist](docs/non-admin-checklist.md).

## Current status

This repository contains the .NET 10 and WinUI 3 foundation plus the typed backend HTTP client layer. WebSocket signaling, WASAPI loopback capture, and WebRTC/Opus publishing remain planned.

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
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Repository.Structure.Tests.ps1
```

The app is an unpackaged WinUI 3 executable. Select `SonicRelay.Windows.App` as the startup project when launching it from an IDE.

## User configuration and tokens

On first launch, the publisher creates editable configuration at `%LOCALAPPDATA%\SonicRelay\WindowsPublisher\appsettings.json`. Backend and signaling addresses must be absolute HTTP(S) or WebSocket URLs, and `defaultMaxViewers` must be greater than zero.

Authentication tokens are stored for the current user at `%LOCALAPPDATA%\SonicRelay\WindowsPublisher\tokens.dat` and protected with Windows DPAPI `CurrentUser`. If DPAPI is unavailable, token operations return a secure-storage error and no plaintext fallback is written. Neither configuration nor token storage requires administrator privileges.

## Backend HTTP client

The configured `backendBaseUrl` is used as the `HttpClient.BaseAddress`; no production address is compiled into the application. The typed clients implement login and refresh under `/auth`, current-user lookup, `windows_publisher` device registration/listing, and stream-session creation/active-list/end operations.

Authenticated requests load the current user's DPAPI-protected bearer token. A `401` with an available refresh token causes one refresh request and one retry, and the replacement tokens are saved back to the user-scoped store. HTTP, network, and backend failures are exposed as typed API errors. This layer uses outbound HTTP(S) only and requires no administrator privileges.

## Planned milestones

1. Repository and Windows application bootstrap.
2. Backend authentication and publisher-device registration.
3. Stream session lifecycle and WebSocket signaling.
4. WASAPI loopback capture and audio pipeline.
5. WebRTC/Opus publication with one peer connection per viewer.
6. Reliability, diagnostics, packaging, and release automation.

See [the publisher specification](docs/windows-publisher.md), [architecture notes](docs/architecture.md), and [non-admin checklist](docs/non-admin-checklist.md) for the planned system boundaries.
