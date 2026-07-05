# Windows Publisher specification

## Purpose

The Windows Publisher will turn Windows system audio into a low-latency SonicRelay stream. It will be the publisher-side desktop client; playback clients and backend services live outside this repository.

Only the application shell and project boundaries exist today. Every runtime interaction below is a target architecture, not current behavior.

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

## Current deliverable

The bootstrap provides a WinUI 3 application, capability-oriented class libraries, focused test projects, shared build settings, and documentation. It deliberately contains no simulated endpoints or placeholder production behavior.
