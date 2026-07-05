# SonicRelay Windows Publisher

SonicRelay Windows Publisher is the Windows desktop application responsible for capturing system audio and publishing it to SonicRelay viewers with low latency. It is one part of the SonicRelay suite and will communicate with the separately maintained backend at [`vitorhugo-java/dotnet_SonicRelay`](https://github.com/vitorhugo-java/dotnet_SonicRelay).

## Current status

This repository currently contains the .NET 10 and WinUI 3 foundation only. Authentication, device registration, stream sessions, WebSocket signaling, WASAPI loopback capture, and WebRTC/Opus publishing are planned but are not implemented.

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

## Planned milestones

1. Repository and Windows application bootstrap.
2. Backend authentication and publisher-device registration.
3. Stream session lifecycle and WebSocket signaling.
4. WASAPI loopback capture and audio pipeline.
5. WebRTC/Opus publication with one peer connection per viewer.
6. Reliability, diagnostics, packaging, and release automation.

See [the publisher specification](docs/windows-publisher.md) and [architecture notes](docs/architecture.md) for the planned system boundaries.
