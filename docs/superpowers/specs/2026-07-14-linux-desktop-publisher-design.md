# Linux Desktop Publisher — Design Spec (Issue #32, Phase 3–5)

## Provenance / Context

- **Date:** 2026-07-14
- **Repository:** `vitorhugo-dotnet/windows_SonicRelay`
- **Driven by:** GitHub issue [#32 — Add Linux support with a cross-platform architecture](https://github.com/vitorhugo-dotnet/windows_SonicRelay/issues/32)
- **Previous design:** [`2026-07-11-avalonia-desktop-shell-design.md`](./2026-07-11-avalonia-desktop-shell-design.md)
- **Current state:** the Avalonia shell has reached Windows parity and replaced the previous WinUI application on `main`.

The previous design intentionally stopped before Linux audio capture and packaging. This document defines the next implementation slice: run the existing Avalonia publisher as a real Linux application, capture a selected system-output sink through PipeWire, reuse the current authentication/signaling/WebRTC/Opus runtime, and publish supported Linux artifacts.

## Status and scope

This design covers the remaining Linux work from issue #32:

1. **Phase 3 — Linux audio adapter and headless validation**
2. **Phase 4 — Linux desktop integration using the existing Avalonia shell**
3. **Phase 5 — Linux CI, packaging, release assets, and documentation**

This is one feature initiative but should be implemented as three reviewable pull requests matching the phases above. Each PR must leave `main` buildable and must not regress the Windows publisher.

## Current codebase state

Relevant facts on `main`:

- `src/SonicRelay.Windows.Desktop` is the shipped Avalonia application.
- `App.axaml.cs` attaches a live `PublisherRuntime` only on Windows. Linux currently opens `MainWindowViewModel.CreatePreview()`.
- `PublisherRuntime.Create(Uri, IAudioCaptureService)` is already independent from a concrete capture technology, but it creates the token store and other platform-sensitive dependencies internally.
- `SonicRelay.Windows.Audio.AudioCaptureService` contains reusable lifecycle, diagnostics, retry, selected-device, and recovery behavior.
- The default `AudioCaptureService` constructor creates `WasapiLoopbackBackend` and `WasapiOutputDeviceProbe` internally and is marked Windows-only.
- The backend/probe seams needed by Linux exist but are internal implementation details.
- `UserScopedTokenStore` uses Windows DPAPI and a Windows-specific storage directory.
- The WebRTC layer uses managed `SIPSorcery` and `Concentus` packages and has no intentional WASAPI or WinUI dependency.
- CI and release packaging currently run only on Windows and publish `win-x64` ZIP, EXE, and MSI assets.
- The solution and project names still contain `Windows`. Renaming every project is unrelated churn and is not required for Linux functionality.

## Objective

A user on the initially supported Linux distribution must be able to:

1. install or extract SonicRelay without installing .NET;
2. launch the same Avalonia shell used on Windows;
3. authenticate against the existing SonicRelay API;
4. list available system-output sinks;
5. select the system default or a specific sink;
6. create a publisher session;
7. capture desktop audio through PipeWire without root;
8. stream through the existing WebRTC/Opus path in Direct or Relay mode;
9. see the existing real diagnostics and reconnect behavior;
10. minimize to tray when the desktop environment exposes a compatible tray implementation;
11. receive actionable errors when PipeWire or another required desktop service is unavailable.

## Initial support matrix

### Officially supported in this phase

- **Architecture:** `linux-x64`
- **Distribution:** Ubuntu 24.04 LTS Desktop
- **Desktop:** GNOME
- **Display sessions:** Ubuntu Wayland session and Ubuntu on Xorg
- **Audio server:** PipeWire with WirePlumber
- **Distribution artifacts:** self-contained `.deb` and portable `.tar.gz`

The application UI may run through Avalonia's Linux/X11 backend or XWayland under a Wayland desktop. Audio capture talks to the user's PipeWire session and is not coupled to X11 or Wayland.

### Best effort, not release-gating

- Debian 13 and compatible Debian-based distributions
- KDE Plasma where the required PipeWire and tray services are available
- Other x64 distributions using the portable archive

### Explicitly out of scope

- `linux-arm64`
- Flatpak, Snap, AppImage, or RPM packaging
- macOS
- native Wayland-only rendering as a requirement
- PulseAudio legacy capture as the primary implementation
- Wine
- support for every desktop environment or tray protocol
- renaming the repository or all `SonicRelay.Windows.*` projects
- autostart support in the first Linux release

## Selected approach

### Decision

Implement the first Linux capture adapter by supervising the official PipeWire command-line tools:

- `pw-dump` for machine-readable node discovery;
- `pw-record` for raw PCM capture from the selected target;
- `secret-tool` for Secret Service-backed token persistence when available.

The adapter starts `pw-record` directly with `ProcessStartInfo.ArgumentList`, reads raw PCM from standard output, converts the stream into the existing `AudioFrame` model, and maps process failures into the existing `AudioCaptureError` model.

### Why this approach

This is the smallest implementation that uses the installed PipeWire stack directly and preserves the existing managed audio/WebRTC pipeline. It avoids:

- maintaining custom `libpipewire` P/Invoke bindings;
- adopting GStreamer and its plugin/runtime matrix;
- depending on an unproven third-party .NET PipeWire wrapper;
- duplicating the existing capture lifecycle and recovery logic;
- coupling the application to PulseAudio compatibility APIs.

`pw-record` supports raw, rate/channel/format configuration, target selection, latency configuration, and streaming output to stdout. Those capabilities match SonicRelay's existing PCM ingestion boundary.

### Alternatives considered

#### A. Native `libpipewire` binding

**Advantages:** lowest process overhead, direct graph events, precise pause/resume and buffer control.

**Rejected for the initial release:** significantly higher unsafe interop complexity, ABI/resource-lifetime risk, and maintenance cost. It remains a future optimization only if profiling demonstrates that the supervised-process adapter cannot meet latency or reliability targets.

#### B. GStreamer with `pipewiresrc`

**Advantages:** mature media graph and many conversion facilities.

**Rejected:** introduces a large native dependency and plugin compatibility surface while SonicRelay already owns resampling/framing/Opus/WebRTC behavior. It solves far more media problems than this application has.

#### C. PulseAudio monitor capture

**Advantages:** broad historical compatibility and simple tools.

**Rejected as the primary path:** PipeWire is the target architecture from issue #32. PulseAudio compatibility may be investigated later as a fallback for unsupported systems, but it must not define the Linux implementation.

#### D. `xdg-desktop-portal` ScreenCast portal

**Rejected for audio-only capture:** the ScreenCast portal selects monitor/window/virtual screen sources and returns screencast PipeWire streams. It is not a general desktop-output audio selection API. Sandboxed packaging therefore remains a separate future design rather than being mixed into the initial `.deb` release.

## Target architecture

```text
SonicRelay.Windows.Desktop (existing Avalonia shell)
  |
  +-- DesktopRuntimeFactory
        |
        +-- WindowsPlatformComposition
        |     +-- AudioCaptureService + WasapiLoopbackBackend
        |     +-- WasapiOutputDeviceProbe
        |     +-- UserScopedTokenStore + DPAPI
        |
        +-- LinuxPlatformComposition
              +-- AudioCaptureService + PipeWireProcessBackend
              +-- PipeWireOutputDeviceProbe
              +-- SecretServiceTokenStore
                    +-- InMemoryTokenStore fallback

PublisherRuntime / Presentation / WebRTC / Signaling / API Client
  remain shared and platform-agnostic
```

### Project layout

Add:

```text
src/SonicRelay.Platform.Linux/
  Audio/
    PipeWireCommandLocator.cs
    PipeWireNode.cs
    PipeWireNodeParser.cs
    PipeWireOutputDeviceProbe.cs
    PipeWireProcessBackend.cs
    PipeWireProcessFactory.cs
    PcmFrameAssembler.cs
  Storage/
    SecretServiceTokenStore.cs
    SecretToolProcess.cs
  LinuxPlatformComposition.cs

tests/SonicRelay.Platform.Linux.Tests/
```

Keep the existing Windows implementation in `SonicRelay.Windows.Audio` during this phase. A broad namespace/project rename provides no user value and would make regression review unnecessarily difficult.

## Shared audio contract changes

The reusable capture orchestration already exists and must stay the single source of truth for start/stop/recovery/diagnostics.

Make the minimum seams public:

```csharp
public interface IAudioCaptureBackend : IAsyncDisposable
{
    AudioDeviceInfo? Device { get; }
    event Action<AudioFrame, AudioLevelSnapshot>? FrameAvailable;
    event Action<AudioCaptureException>? Faulted;
    Task StartAsync(CancellationToken cancellationToken);
    Task PauseAsync(CancellationToken cancellationToken);
    Task ResumeAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public interface IAudioOutputDeviceProbe
{
    IReadOnlyList<AudioOutputDevice> GetOutputDevices();
}
```

Expose a platform-neutral construction path:

```csharp
public static AudioCaptureService Create(
    IAudioCaptureBackend backend,
    IAudioOutputDeviceProbe deviceProbe,
    AudioRecoveryPolicy? recoveryPolicy = null);
```

The existing Windows constructor remains for compatibility, but `App.axaml.cs` must stop constructing it directly. Both platforms go through the same `DesktopRuntimeFactory`.

Move PCM level calculation into a shared pure helper so WASAPI and PipeWire do not maintain separate peak/RMS implementations.

Replace Windows-specific generic messages inside `AudioCaptureService` with platform-neutral text. Platform adapters remain responsible for specific actionable details.

## PipeWire device discovery

### Command

Run `pw-dump` without a shell and parse its JSON output.

### Node filtering

Return output devices from PipeWire nodes that represent audio sinks. Each `AudioOutputDevice` must contain:

- stable preference key: `node.name`;
- display name: `node.description`, then `device.description`, then `node.nick`, then `node.name`;
- platform: `linux`;
- default marker when the session manager metadata identifies it as default, when available.

Always prepend the existing `System default` option. Selecting it stores `null` and lets `pw-record` use automatic target selection.

### Persistence and hotplug

Persist `node.name`, not an ephemeral numeric object ID. Before each capture start:

1. re-run discovery;
2. resolve the saved `node.name` to the current node;
3. use its current `object.serial` when available, otherwise use `node.name`;
4. fall back to system default if the stored sink no longer exists;
5. emit a diagnostic event describing the fallback without logging secrets.

Discovery failures must return an empty device list plus an actionable platform diagnostic; they must not crash the settings page.

## PipeWire capture process

### Process invocation

For the system default sink:

```text
pw-record --raw --rate=48000 --channels=2 --format=s16 --latency=20ms -
```

For a selected sink, add:

```text
--target=<resolved object.serial or node.name>
```

Arguments must be passed through `ProcessStartInfo.ArgumentList`; never build a shell command string.

Configuration:

```text
sample rate: 48000 Hz
channels: 2
sample format: signed 16-bit little-endian PCM
requested latency: 20 ms
stdout: raw PCM
stderr: captured separately for bounded diagnostics
stdin: closed
shell execute: false
```

The initial adapter intentionally normalizes Linux capture to 48 kHz stereo PCM16. Existing downstream code remains responsible for the configured Opus profile and WebRTC transmission.

### Frame assembly

`PcmFrameAssembler` reads stdout continuously and emits exact 20 ms frames:

```text
48000 samples/sec × 0.020 sec × 2 channels × 2 bytes = 3840 bytes/frame
```

Requirements:

- tolerate arbitrary pipe read boundaries;
- never emit partial samples;
- keep at most one incomplete frame buffered;
- timestamp frames using a monotonic `Stopwatch` from capture start;
- calculate peak/RMS with the shared PCM level helper;
- stop promptly after cancellation;
- never block the UI thread.

### Pause and resume

The initial process adapter implements pause as a controlled process stop and resume as a fresh start against the same selected sink. This creates a small discontinuity but avoids Unix signal interop and keeps lifecycle behavior deterministic.

### Process supervision

`PipeWireProcessBackend` owns exactly one `pw-record` process.

- `StartAsync` succeeds only after the process remains alive and the first complete PCM frame is received, with a bounded startup timeout.
- Unexpected exit after startup raises `Faulted`.
- `StopAsync` cancels reads, requests process termination, waits for a bounded grace period, then kills the process tree if necessary.
- Stderr is bounded in memory and redacted before entering diagnostics.
- Disposal is idempotent.

### Error mapping

Map failures into existing errors:

| Linux failure | `AudioCaptureError` | User-facing behavior |
|---|---|---|
| `pw-record` or `pw-dump` missing | `PlatformFailure` | Explain that PipeWire tools are required and name the package dependency |
| PipeWire session unavailable | `PlatformFailure` | Ask the user to verify the user PipeWire service |
| selected sink missing | `NoDevice` | Fall back/retry through existing recovery policy |
| process exits during capture | `DeviceLost` | Trigger existing exponential recovery |
| permission/socket access denied | `AccessDenied` | Explain that the app must run as the desktop user, not root |
| malformed discovery JSON | `PlatformFailure` | Record a bounded diagnostic and keep the app usable |

A process failure must never take down signaling or the Avalonia UI.

## Platform composition

Add `DesktopRuntimeFactory` in the desktop project. It returns a result containing either a configured runtime or an actionable unsupported-platform error.

```csharp
public sealed record DesktopRuntimeDependencies(
    IAudioCaptureService AudioCapture,
    ITokenStore TokenStore,
    string DeviceName);
```

Change runtime composition to accept dependencies rather than constructing the token store internally:

```csharp
public static PublisherRuntime Create(
    Uri backendBaseUrl,
    DesktopRuntimeDependencies dependencies);
```

Platform selection:

```text
Windows -> WASAPI + DPAPI
Linux   -> PipeWire + Secret Service
Other   -> unsupported-platform state; no preview data
```

`App.axaml.cs` must no longer call `CreatePreview()` on Linux. Preview mode remains available only to the Avalonia designer and explicit visual tests.

Startup failures leave the real login shell visible with a platform error and retry action. They must not silently show representative fake metrics.

## Linux token storage

### Primary store

Implement `SecretServiceTokenStore` by invoking `secret-tool` directly:

- store the serialized token set through stdin;
- use fixed attributes such as `application=sonicrelay` and `purpose=publisher-token`;
- retrieve and clear through the same attributes;
- never pass token contents in command-line arguments;
- never log stdout containing token material;
- use bounded timeouts and cancellation;
- map unavailable/locked Secret Service to `SecureStorageUnavailable`.

### Fallback

When Secret Service or `secret-tool` is unavailable, use an in-memory token store for the current process and show a non-blocking warning:

```text
Secure session storage is unavailable. You can continue, but you will need to sign in again after restarting SonicRelay.
```

There is no plaintext-on-disk fallback.

Windows DPAPI behavior and existing token files must remain unchanged.

## Linux application data paths

Introduce a small platform path resolver in Core:

```text
config: $XDG_CONFIG_HOME/sonicrelay
        fallback ~/.config/sonicrelay
state:  $XDG_STATE_HOME/sonicrelay
        fallback ~/.local/state/sonicrelay
cache:  $XDG_CACHE_HOME/sonicrelay
        fallback ~/.cache/sonicrelay
```

Use the resolver for Linux preferences, diagnostics, and configuration. Preserve all current Windows paths to avoid migrating or losing existing Windows settings.

File permissions for Linux-created directories/files must rely on the current user's umask and must never intentionally grant group/world write access.

## Tray and window lifecycle

Keep the existing Avalonia tray integration, but make tray capability explicit.

- Tray initialization failure must not prevent launch.
- Close/minimize-to-tray is enabled only after a tray icon is successfully created.
- When no compatible tray implementation exists, closing the main window exits normally instead of leaving an invisible background process.
- The diagnostics page records whether tray support is active.
- Ubuntu 24.04 GNOME is the release-gating tray environment for this phase.

Autostart is deferred. It should be designed separately after the first Linux release proves stable.

## Desktop project changes

Make Windows-only MSBuild properties conditional and allow Linux publishing:

- use `Exe` for Linux and `WinExe` for Windows;
- apply `app.manifest` and `BuiltInComInteropSupport` only to Windows targets;
- keep the existing Avalonia package line unless a separate dependency update is required;
- add a reference to `SonicRelay.Platform.Linux`;
- keep `UsePlatformDetect()` in the Avalonia bootstrap.

The UI and view models must not reference `pw-record`, `pw-dump`, `secret-tool`, D-Bus, or Linux-specific paths directly.

## CI design

### Build and test matrix

Run solution build/tests on:

```text
windows-latest
ubuntu-24.04
```

The Ubuntu job installs only dependencies required to build/render tests. Unit tests for process adapters use fakes and must not require a live audio session in GitHub Actions.

### Linux-specific verification

Add tests for:

- `pw-dump` JSON parsing;
- sink filtering and display-name fallback;
- stable `node.name` preference resolution;
- command argument construction;
- raw PCM frame assembly across arbitrary read boundaries;
- peak/RMS calculation parity with WASAPI;
- startup timeout and cancellation;
- process exit/error mapping;
- device-loss recovery integration with `AudioCaptureService`;
- Secret Service process invocation without secret leakage;
- platform composition selection;
- desktop startup smoke test on Linux;
- repository structure expectations for the new project and release assets.

### Manual release gate

Before the first public Linux release, validate on a real Ubuntu 24.04 desktop:

1. Wayland session;
2. Xorg session;
3. default sink capture;
4. explicit HDMI/headset sink selection;
5. sink disconnect/reconnect;
6. Direct WebRTC;
7. forced TURN Relay;
8. minimize/restore from tray;
9. app restart with Secret Service available;
10. app restart with Secret Service unavailable;
11. `.deb` install, upgrade, and uninstall;
12. portable archive launch.

## Packaging and release

### Linux artifacts

Publish self-contained `linux-x64` output and create:

```text
SonicRelay-LinuxPublisher-linux-x64-<version>.tar.gz
SonicRelay-LinuxPublisher-linux-x64-<version>.deb
checksums-sha256.txt
```

Do not publish a Linux single-file binary in the first release. A folder-based publish is easier to inspect and avoids unnecessary native-library extraction behavior.

### Debian package layout

```text
/usr/lib/sonicrelay/                 self-contained publish output
/usr/bin/sonicrelay                  small exec wrapper
/usr/share/applications/sonicrelay.desktop
/usr/share/icons/hicolor/.../sonicrelay.png|svg
```

Package metadata must declare the Ubuntu/Debian packages that provide:

- PipeWire command-line tools (`pw-record`, `pw-dump`);
- Secret Service CLI (`secret-tool`);
- Avalonia Linux/X11 native dependencies;
- CA certificates and other required native runtime libraries.

Installing the `.deb` may require administrator authorization because it writes system application directories. Running SonicRelay must never require root.

### Desktop entry

The `.desktop` entry must:

- launch `sonicrelay`;
- use `Terminal=false`;
- use the SonicRelay icon;
- categorize the application under Audio/Network/Utility as appropriate;
- set a stable startup WM class when supported;
- avoid embedding environment-specific paths.

### Release workflow

Keep Windows assets unchanged. Extend release packaging with a Linux job that:

1. restores for `linux-x64`;
2. publishes self-contained output;
3. creates `BUILD-INFO.txt`;
4. builds `.tar.gz` and `.deb`;
5. calculates SHA-256 hashes;
6. uploads artifacts;
7. attaches Linux assets to the same GitHub Release as Windows assets.

A failure in either platform packaging must fail the release rather than publish a partial official release without an explicit manual override.

## Diagnostics and observability

Add Linux platform properties to the existing diagnostic report:

```text
osPlatform=linux
osDescription=<redacted OS description>
desktopSession=wayland|x11|unknown
pipeWireAvailable=true|false
pwRecordVersion=<version or unavailable>
pwDumpVersion=<version or unavailable>
secretServiceAvailable=true|false
trayAvailable=true|false
selectedAudioDevice=<friendly name; no token data>
```

Do not include:

- access/refresh tokens;
- complete Secret Service output;
- arbitrary environment variables;
- unbounded stderr;
- usernames/home-directory paths unless already redacted by the diagnostic layer.

## Security requirements

- Never run SonicRelay or PipeWire tools through `sudo`.
- Never invoke `/bin/sh -c` for platform commands.
- Use `ProcessStartInfo.ArgumentList` for every argument.
- Bound process startup, shutdown, and read operations.
- Kill child processes during cancellation/disposal.
- Never persist tokens in plaintext.
- Never place tokens in arguments, environment variables, logs, or diagnostics.
- Treat discovery JSON and process output as untrusted input.
- Cap parsed/output sizes before allocation.
- Keep the backend URL validation and existing HTTPS/WSS rules unchanged.
- Linux support must not weaken Windows DPAPI storage or Windows release behavior.

## Performance targets

On Ubuntu 24.04 x64 with a typical desktop workload:

```text
capture format: 48 kHz stereo PCM16
capture frame: 20 ms
additional capture-process startup target: <= 1.5 s
steady-state capture CPU target: <= 5% of one logical core
steady-state adapter memory growth: no unbounded growth
capture-to-existing-WebRTC-path latency regression: <= 40 ms versus equivalent Windows path, excluding network variance
```

These are acceptance targets, not claims about every Linux system. A failure to meet them must produce profiling evidence before replacing the process adapter with native interop.

## Implementation slices

### PR 1 — Linux capture adapter and shared seams

- expose the minimal backend/probe construction seams;
- centralize PCM level calculation;
- add `SonicRelay.Platform.Linux` and tests;
- implement `pw-dump` discovery;
- implement supervised `pw-record` PCM capture;
- validate frames through the existing WebRTC audio bridge using automated fakes and a real local Linux smoke test;
- keep the desktop Linux startup behavior unchanged until the adapter is proven.

### PR 2 — Linux desktop composition

- add `DesktopRuntimeFactory`;
- inject token store/runtime dependencies;
- add Secret Service store plus in-memory fallback;
- add XDG paths;
- attach the real runtime on Linux instead of preview mode;
- make tray capability explicit;
- validate authentication, sink selection, sessions, reconnect, Direct, and Relay on Ubuntu.

### PR 3 — CI, package, and release

- add Ubuntu build/test matrix;
- add Linux desktop smoke tests;
- publish `linux-x64`;
- build `.tar.gz` and `.deb`;
- add icons and `.desktop` entry;
- extend release notes/checksums;
- document installation, dependencies, supported systems, diagnostics, and limitations.

## Acceptance criteria

### Architecture

- [ ] Windows and Linux use the same Avalonia views and view models.
- [ ] Linux-specific code is isolated in `SonicRelay.Platform.Linux` and desktop composition.
- [ ] Presentation, signaling, API client, and WebRTC layers do not reference PipeWire tools.
- [ ] The existing shared `AudioCaptureService` owns lifecycle/recovery on both platforms.
- [ ] No broad project rename is required for the feature.

### Linux runtime

- [ ] Linux launches the real application state, not `CreatePreview()`.
- [ ] The application starts without root on Ubuntu 24.04 x64.
- [ ] Missing platform dependencies produce actionable UI errors.
- [ ] The user can authenticate with the existing backend.
- [ ] Tokens are stored through Secret Service when available.
- [ ] No plaintext token file is created.
- [ ] Session-only auth remains usable when Secret Service is unavailable.

### Audio

- [ ] `System default` capture works through PipeWire.
- [ ] Available output sinks are listed with human-readable names.
- [ ] A selected sink persists by stable `node.name`.
- [ ] Captured data is 48 kHz stereo PCM16 and reaches the existing Opus/WebRTC pipeline.
- [ ] The selected desktop output is captured, not the microphone.
- [ ] Sink loss triggers bounded recovery and does not crash the UI.
- [ ] Pause/resume and stop/dispose leave no orphan `pw-record` process.

### Streaming

- [ ] Session creation and signaling work unchanged.
- [ ] A Flutter viewer receives Linux desktop audio.
- [ ] Direct mode works when network conditions permit.
- [ ] forced TURN/Relay mode works.
- [ ] RTT, jitter, loss, bitrate, viewer count, ICE mode, and audio level remain real data.
- [ ] reconnection and session termination behave consistently with Windows.

### Desktop integration

- [ ] The Avalonia UI is usable on Ubuntu Wayland and Xorg sessions.
- [ ] Tray behavior works on the release-gating Ubuntu GNOME environment.
- [ ] If tray is unavailable, closing exits instead of hiding an unreachable process.
- [ ] settings and diagnostics use Linux/XDG paths.

### CI and distribution

- [ ] Windows and Ubuntu builds/tests are required checks.
- [ ] Windows release assets remain unchanged.
- [ ] Linux `.tar.gz` and `.deb` assets are generated from the same tag/commit.
- [ ] Linux artifacts include SHA-256 checksums and build metadata.
- [ ] `.deb` install/upgrade/uninstall are manually validated.
- [ ] normal application execution requires no administrator privileges.
- [ ] installation and known limitations are documented.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---:|---|
| PipeWire CLI output/behavior differs across distributions | Medium | Officially support one Ubuntu LTS first; parse JSON defensively; check tool versions in diagnostics |
| `pw-record` process adds latency or lifecycle edge cases | Medium | fixed 20 ms raw frames, bounded supervision, profiling targets, native interop only if evidence requires it |
| selected node identifiers change | Medium | persist `node.name`; resolve current `object.serial` on every start; fallback to default |
| tray unavailable on some Linux desktops | Medium | explicit capability; never hide the app without a reachable tray |
| Secret Service unavailable or locked | Low | no plaintext fallback; use in-memory tokens and warn that restart requires login |
| CI has no real user audio session | Medium | isolate process abstractions, use deterministic fakes, require real-desktop manual release gate |
| Flatpak sandbox blocks the chosen integration | Low for this phase | Flatpak is out of scope and requires a separate portal/sandbox design |
| Windows regresses while shared seams change | High | keep existing constructor/path compatibility; run full Windows CI and release smoke tests |

## Documentation changes required during implementation

Update:

- `README.md` — Windows and Linux support, downloads, quick start;
- `docs/architecture.md` — platform composition and Linux adapter;
- `docs/windows-publisher.md` — rename or split into desktop publisher documentation without breaking existing links;
- new `docs/linux-publisher.md` — installation, dependencies, device selection, troubleshooting, diagnostics, supported environments;
- release notes — platform-specific assets and limitations;
- issue #32 — phase checklist and final validation evidence.

## Final design decisions

```text
ADR-LINUX-001: Reuse the existing Avalonia shell; do not create a second Linux UI.
ADR-LINUX-002: Use PipeWire as the primary Linux audio system.
ADR-LINUX-003: Use supervised pw-dump/pw-record processes for the first supported release.
ADR-LINUX-004: Normalize capture to 48 kHz stereo PCM16, 20 ms frames.
ADR-LINUX-005: Persist sink preference by node.name and resolve the live target at start.
ADR-LINUX-006: Use Secret Service via secret-tool; never fall back to plaintext token storage.
ADR-LINUX-007: Officially support Ubuntu 24.04 x64 first.
ADR-LINUX-008: Ship .deb and portable .tar.gz; defer sandboxed packaging.
ADR-LINUX-009: Tray is capability-based; the app must never become invisibly unreachable.
ADR-LINUX-010: Keep historical Windows-prefixed project names during this phase to minimize churn.
```

## Primary technical references

- Avalonia Desktop Linux deployment: https://docs.avaloniaui.net/docs/deployment/linux
- Avalonia TrayIcon: https://docs.avaloniaui.net/controls/navigation/trayicon
- PipeWire `pw-cat` / `pw-record`: https://docs.pipewire.org/page_man_pw-cat_1.html
- XDG Desktop Portal ScreenCast: https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.ScreenCast.html
