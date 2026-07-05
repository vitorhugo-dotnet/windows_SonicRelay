# Windows Publisher Bootstrap Design

## Scope

Bootstrap the SonicRelay Windows Publisher repository with a .NET 10 solution, a WinUI 3 application, focused supporting class libraries, two test projects, repository-wide build settings, and documentation. This issue establishes project boundaries only; it does not implement authentication, backend endpoints, signaling, audio capture, or WebRTC.

## Solution structure

The root solution uses the modern `.slnx` format and contains:

- `SonicRelay.Windows.App`: packaged WinUI 3 desktop application and composition root.
- `SonicRelay.Windows.Core`: domain types and application-independent rules.
- `SonicRelay.Windows.ApiClient`: future backend HTTP integration boundary.
- `SonicRelay.Windows.Signaling`: future WebSocket signaling boundary.
- `SonicRelay.Windows.Audio`: future WASAPI loopback capture boundary.
- `SonicRelay.Windows.WebRtc`: future peer-connection and Opus publishing boundary.
- `SonicRelay.Windows.Core.Tests`: unit tests for Core.
- `SonicRelay.Windows.ApiClient.Tests`: unit tests for ApiClient.

Supporting projects remain empty class libraries except for minimal marker types needed to prove references and test discovery. The app references the supporting libraries so invalid dependency wiring fails during the build. Test projects reference only the production projects they test.

## Build configuration

All projects target .NET 10. `Directory.Build.props` enables nullable reference types, implicit global usings, deterministic builds, and analyzers without treating warnings as errors. The WinUI project targets Windows explicitly and uses the Windows App SDK NuGet package. A pinned SDK declaration keeps local and CI builds aligned with .NET 10 while allowing later feature-band patches.

The repository includes Visual Studio, Rider, .NET, WinUI packaging, test-result, and user-specific exclusions. Generated folders are never committed.

## Application behavior

The initial app displays a minimal SonicRelay Windows Publisher shell and states that streaming is not implemented. It contains no configurable or hardcoded backend address and initiates no network or audio operations.

## Documentation

The README identifies this repository as the Windows Publisher, explains its place in the SonicRelay suite, documents prerequisites and build commands, reports the bootstrap-only status, and lists milestones.

`docs/windows-publisher.md` defines the intended publisher responsibilities and includes GitHub-compatible Mermaid context and future streaming-flow diagrams. `docs/architecture.md` records project dependency direction, planned runtime boundaries, and explicit non-goals. Future capabilities are consistently labeled as planned.

## Error handling

There is no production workflow requiring runtime error handling in this bootstrap. Build and package failures remain visible through standard MSBuild diagnostics. Future integration layers will define typed failure handling when their behavior is implemented.

## Verification

Verification is intentionally narrow:

1. A repository-structure test initially fails until the required solution projects and documentation exist.
2. Restore the solution.
3. Build the solution with `dotnet build` where Windows App SDK tooling permits.
4. Run only the two new test projects.
5. Inspect the resulting diff for out-of-scope implementation and generated artifacts.

## Acceptance mapping

- Visual Studio and Rider can open the root solution and discover all projects.
- The installed .NET 10 SDK can restore and build the solution on Windows.
- README and documentation distinguish current scaffolding from planned capabilities.
- Mermaid diagrams use supported GitHub syntax and uncomplicated node labels.
- No authentication, endpoint, WASAPI, WebRTC, or signaling behavior is present.
