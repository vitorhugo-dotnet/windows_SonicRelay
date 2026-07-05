# Architecture

## Project boundaries

```mermaid
flowchart TD
    App[SonicRelay.Windows.App] --> Core[SonicRelay.Windows.Core]
    App --> ApiClient[SonicRelay.Windows.ApiClient]
    App --> Signaling[SonicRelay.Windows.Signaling]
    App --> Audio[SonicRelay.Windows.Audio]
    App --> WebRtc[SonicRelay.Windows.WebRtc]
    CoreTests[SonicRelay.Windows.Core.Tests] --> Core
    ApiTests[SonicRelay.Windows.ApiClient.Tests] --> ApiClient
    SignalingTests[SonicRelay.Windows.Signaling.Tests] --> Signaling
    AudioTests[SonicRelay.Windows.Audio.Tests] --> Audio
```

- **App** is the WinUI 3 composition root and owns desktop lifecycle and presentation.
- **Core** will hold application-independent domain state and rules.
- **ApiClient** will implement typed backend HTTP communication.
- **Signaling** manages authenticated outbound WebSocket signaling messages, validated envelopes, connection state, and conservative reconnect behavior. It does not transport audio.
- **Audio** owns default-render-device WASAPI loopback capture, validated audio-frame delivery, activity metering, lifecycle state, and capture diagnostics.
- **WebRtc** will own peer connections, negotiation, and Opus publication.

Capability project references establish dependency direction: Signaling depends on Core for configuration and user-scoped tokens, while Audio remains independently testable and WebRtc remains an isolated placeholder until its requirements are implemented.

## Planned runtime data flow

```mermaid
flowchart LR
    Wasapi[WASAPI loopback] --> Frames[Audio frames]
    Frames --> WebRtc[WebRTC and Opus]
    Signaling[WebSocket signaling] --> WebRtc
    Api[Backend API] --> Session[Stream session state]
    Session --> Signaling
    WebRtc --> PeerA[Viewer peer connection]
    WebRtc --> PeerB[Viewer peer connection]
```

The UI will request operations through application-level orchestration added in later issues. Capability projects must not depend on the App project. Cross-cutting contracts should be introduced only when a concrete feature needs them.

## Error boundaries

Future external integrations will translate transport and platform failures into explicit results at their project boundaries. The UI will remain responsible for user-facing state. Unexpected failures will be logged without credentials, tokens, or raw sensitive signaling payloads.

## Deployment constraints

Non-admin operation is an architectural boundary, not a packaging preference. Installation, configuration, upgrades, and runtime must work for a standard Windows user:

- Deployment must be unpackaged and self-contained, per-user, or portable where practical and must not require elevation.
- Mutable application data must use user-scoped locations such as `%LOCALAPPDATA%` or `%APPDATA%`; Program Files and other protected system folders are read-only at runtime.
- Runtime configuration must not be stored in HKLM. User-scoped files or, when justified, HKCU are permitted.
- Normal usage must not require Windows services, custom audio drivers, kernel-mode components, or machine-wide dependencies.
- Network flows must be application-initiated outbound traffic for API, WebSocket signaling, WebRTC, TURN, and STUN. The app must not require inbound firewall ports or attempt to modify firewall rules.
- Dependency selection must include an elevation review. A dependency that requires administrator rights for normal usage is incompatible and must be rejected or replaced.

The canonical acceptance gate for future architecture and implementation work is the [non-admin checklist](non-admin-checklist.md).

## Non-goals for the bootstrap

- Authentication or token storage
- Backend URL configuration or production endpoints
- Device registration or stream-session behavior
- WebSocket connectivity
- WebRTC, SDP, ICE, or Opus behavior
- Installer and release packaging
