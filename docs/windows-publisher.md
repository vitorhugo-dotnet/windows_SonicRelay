# Windows Publisher specification

## Purpose

The Windows Publisher will turn Windows system audio into a low-latency SonicRelay stream. It will be the publisher-side desktop client; playback clients and backend services live outside this repository.

The application shell, user-scoped configuration/token storage, typed backend HTTP clients, signaling client, and WASAPI loopback capture exist today. WebRTC media publication below remains target architecture.

## System context

```mermaid
flowchart LR
    Publisher[Windows Publisher] -->|HTTPS and WebSocket| Backend[SonicRelay backend]
    Publisher -->|WebRTC audio per viewer| ViewerA[Viewer A]
    Publisher -->|WebRTC audio per viewer| ViewerB[Viewer B]
    Audio[Windows system audio] -->|WASAPI loopback| Publisher
```

## Planned responsibilities

- Authenticate a user against the SonicRelay backend.
- Register the current machine as a `windows_publisher` device.
- Create and manage stream sessions.
- Maintain a WebSocket connection for signaling events.
- Capture system output with WASAPI loopback.
- Encode and publish audio through WebRTC with Opus.
- Maintain one `RTCPeerConnection` for each connected viewer.

The publisher will not host backend business rules, mix viewer playback, or expose a production endpoint of its own.

## Planned streaming flow

```mermaid
sequenceDiagram
    participant User
    participant Publisher as Windows Publisher
    participant Backend as SonicRelay backend
    participant Viewer

    User->>Publisher: Start publishing
    Publisher->>Backend: Authenticate and register device
    Publisher->>Backend: Create stream session
    Publisher->>Backend: Connect signaling WebSocket
    Viewer->>Backend: Join stream
    Backend-->>Publisher: Viewer signaling request
    Publisher->>Publisher: Create peer connection for viewer
    Publisher-->>Viewer: Negotiate WebRTC through signaling
    Publisher-->>Viewer: Publish Opus audio
```

## Constraints

- Backend addresses must come from future configuration; none are hardcoded.
- Viewer isolation requires a separate peer connection for every viewer.
- Audio capture and network work must not block the UI thread.
- Secrets and access tokens must not be written to logs.

## Implemented HTTP surface

The Windows client follows the backend's documented routes: `/auth/login`, `/auth/refresh`, `/auth/me`, `/api/devices/`, `/api/sessions/`, `/api/sessions/active`, and `/api/sessions/{sessionId}/end`. Device registration fixes the backend-required pair `windows_publisher`/`windows`. The backend base URL always comes from user configuration.

These clients attach the stored opaque bearer token, refresh and retry once after an unauthorized response when possible, and map authorization, validation, conflict, network, backend, and unknown failures into typed errors. They carry control-plane JSON only; no audio or WebSocket signaling passes through this layer.

## WASAPI loopback capture

The Audio capability opens the default Windows render endpoint in WASAPI shared loopback mode. This is a user-mode Core Audio API: it installs no driver, starts no service, changes no global device setting, and requires no administrator privilege. The Audio page can start, pause, resume, and stop capture while displaying the selected endpoint, native mix format, live peak activity, captured frame/byte counters, state, and the last mapped error.

Frames use the endpoint's native shared-mode mix format, currently IEEE float 32-bit or PCM 16-bit. Loopback normally yields silence when no application is playing audio. Capture follows the default endpoint selected at start; removing or invalidating that endpoint faults the capture cleanly, after which the user can stop and restart against the current default device. Windows may exclude protected content. This layer does not resample, encode Opus, create WebRTC peers, or transmit audio.

## Audio quality profiles

Captured audio is always encoded with Opus before it leaves over WebRTC; raw
PCM/Float32 is never sent on the wire. The Audio page exposes a **Stream quality**
selector so the user can trade bandwidth against fidelity:

| Profile | Channels | Opus bitrate | Frame | Use |
|---|---|---|---|---|
| Voice / Economy | Mono | 32 kbps | 20 ms | calls, voice |
| Balanced | Stereo | 96 kbps | 20 ms | general use |
| High quality | Stereo | 128 kbps | 20 ms | music, media (default) |
| Custom | 1–2 | 16–192 kbps | 10/20/40 ms | advanced |

The sample rate stays fixed at 48 kHz for Opus/WebRTC compatibility. WASAPI
loopback still captures the endpoint's native mix format; the accumulator
down/upmixes and resamples to the selected channel count and frame size before
the encoder. The selected profile is persisted per user in
`%LocalAppData%\SonicRelay\WindowsPublisher\audio-quality.json` (via
`AudioQualityStore`) and restored on startup. It is read when each viewer's peer
connection is created, so a change applies to the next stream; the selector is
disabled while capture is running and a hint asks the user to restart capture to
apply a different profile. The page shows the effective codec settings (codec,
bitrate, channels, frame duration, sample rate) and an approximate traffic
estimate (kbps, MB/min, MB/hour) derived from the Opus bitrate.

## Audio source selection

By default the publisher captures the current Windows **default** render endpoint.
The Audio page's **Audio source** section lets the user instead pick a specific
output device: it lists the active render endpoints (via Core Audio / MMDevice,
read-only, no elevation), marks the current default, and offers a "System default"
option that follows whatever Windows chooses. The selection is persisted per user
in `%LocalAppData%\SonicRelay\WindowsPublisher\audio-output.json`
(`AudioOutputPreferenceStore`) and restored on startup via
`AudioCaptureService.SelectOutputDevice`.

The chosen device is opened at the next capture start (WASAPI loopback on that
endpoint), so a change applies when capture is (re)started; the picker is disabled
while capturing. If a previously saved device is no longer present when capture
starts, the capture layer **falls back safely to the system default** and the page
shows a note explaining the fallback. Enumeration is entirely defensive — any COM
failure simply yields the default-only behaviour.

**Limitation (deferred):** publishing a *mix* of several output devices as one
track is out of scope for now; a single output endpoint is captured. The capture
layer is kept isolated so per-application or multi-device filtering can be added
later without reworking the WebRTC/Opus path.

## Non-admin requirement

The Windows Publisher must install, configure, and run as a standard Windows user without elevation. Its normal operation must not depend on an administrator-approved installer, Windows service, custom audio driver, kernel-mode component, machine-wide runtime, inbound firewall rule, HKLM configuration, or runtime writes to protected locations such as Program Files.

Distribution should be unpackaged and self-contained, per-user, or portable where practical. Configuration, tokens, logs, and other mutable data must use user-scoped folders. API, signaling, WebRTC, TURN, and STUN traffic must be initiated outbound by the application; the publisher must not assume it can open an inbound port or modify firewall rules.

Dependencies that require elevation for normal usage are incompatible and must be rejected. Future implementation and release work must be reviewed against the [non-admin checklist](non-admin-checklist.md).

## Diagnostics and safe sharing

The Diagnostics page shows the current backend, authentication, device, session, signaling, audio capture, and WebRTC status. Structured JSON Lines logs are stored per user in `%LocalAppData%\SonicRelay\WindowsPublisher\logs`; exported Markdown reports are stored in the adjacent `diagnostics` folder. Neither operation requires elevation or writes to Windows Event Log.

Exported diagnostic reports are designed to be safe to attach to a support request: identifiers are masked, backend URLs contain only scheme/host/port, and credentials, tokens, passwords, email addresses, SDP bodies, and ICE candidates are redacted. Do not share `tokens.dat`, `appsettings.json`, raw signaling captures, memory dumps, or any manually collected SDP/ICE payload even when sharing an exported report.

## Current deliverable

The bootstrap provides a WinUI 3 application, capability-oriented class libraries, focused test projects, shared build settings, and documentation. It deliberately contains no simulated endpoints or placeholder production behavior.
