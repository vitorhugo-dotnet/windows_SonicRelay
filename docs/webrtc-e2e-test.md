# Manual end-to-end test: Windows Publisher ↔ Flutter viewer (WebRTC)

This document validates the real WebRTC publication path (issue #15): offer/answer/ICE
exchange over the backend signaling WebSocket, and Opus audio playback on the Flutter
viewer. It is a **manual** test — it needs a running backend, a real Windows machine
playing audio, and a Flutter device/emulator.

## What is implemented

The publisher performs a real `RTCPeerConnection` negotiation per viewer (SipSorcery):

- One `RTCPeerConnection` **per viewer** (`PeerConnectionManager`, deduped by viewer id).
- Offer/answer/ICE over the backend signaling WebSocket, using the shared envelope and
  message types (`SonicRelay.Windows.Signaling.SignalingMessageTypes`):
  - `session.joined` / `viewer.ready` → publisher registers the viewer and sends
    `webrtc.offer` (`WebRtcPublisher.HandleViewerReadyAsync` → `CreateOfferAsync`).
  - `webrtc.answer` → `PeerConnectionManager.ApplyAnswerAsync`.
  - `webrtc.ice_candidate` → exchanged **both** ways (publisher emits local candidates via
    `LocalIceCandidateReady`; remote candidates go to `AddRemoteIceCandidateAsync`).
  - `session.left` / `session.ended` → tears the viewer's peer down.
- WASAPI loopback audio → S16 → Opus, pushed into every viewer's peer track
  (`WebRtcAudioBridge` → `PushAudioFrameAsync`). Raw PCM is never sent on the wire.
- Diagnostics: ICE state, selected path (Direct/Relay), estimated RTT and viewer count,
  surfaced on the Dashboard.

Message shapes are the backend's contract and match the Flutter viewer's expectations;
this test does **not** change the protocol.

## Prerequisites

- Backend reachable (default `https://sonicrelay-api.hugodotnet.dev`) with coturn
  configured; `GET /api/webrtc/ice-servers` returns STUN/TURN + short-lived TURN creds.
- A Windows machine with audio playing (music/video) so loopback capture is non-silent.
- The Flutter SonicRelay viewer built and pointed at the same backend.
- One user account (both apps can use the same account).

## Test A — direct/STUN path (same or permissive network)

1. **Publisher**: launch the Windows app, sign in, and confirm the Connection page shows
   *Signed in* and a `windows_publisher` device is prepared.
2. **Publisher**: create a session; note the session **code**. The Dashboard shows
   *Signaling: Connected* and *Session: Waiting*.
3. **Publisher**: start audio capture (Audio page) with music playing; the visualizer moves.
4. **Viewer**: sign in, enter the session code, and join.
5. **Observe on the publisher Dashboard**:
   - Viewer count goes to **1**.
   - WebRTC/ICE badge reaches **Connected**.
   - Connection mode shows **Direct** (or **Relay** if UDP was blocked).
   - Latency/RTT shows a real value.
6. **Viewer**: confirm **audio plays** and tracks the source (pause the source → silence).
7. **Verify offer/answer/ICE** actually happened (not just "connected"): on the publisher,
   Diagnostics → export report, and confirm ICE state transitions and a selected candidate
   pair are recorded (SDP/ICE bodies are redacted by design — presence of the state
   transitions is the signal).

**Pass:** viewer count 1, ICE Connected, audible audio on the viewer, RTT populated.

## Test B — relay/TURN path (UDP degraded or forced)

1. Repeat Test A, but first enable **Settings → Connection → Force relay (TURN only)** on
   the publisher (sets `iceTransportPolicy = relay`). This restricts ICE to relayed
   candidates through coturn.
2. Join from the viewer as before.
3. **Observe**: Connection mode shows **Relay**; audio still plays (higher RTT expected).

**Pass:** connection establishes via the relay and audio plays. This proves the TURN path
works when direct/STUN is unavailable.

## Test C — multiple viewers (isolation)

1. With a session active and audio capturing, join from a **second** viewer.
2. **Observe**: viewer count 2; both viewers hear audio; the Dashboard shows two peers.
3. Leave from one viewer → count drops to 1, the other viewer is unaffected (each viewer
   has its own `RTCPeerConnection`).

**Pass:** both viewers get independent audio; leaving one does not disturb the other.

## Known limitations (deferred)

- Per-peer **jitter, packet loss and bitrate** are not yet plumbed from WebRTC `getStats`;
  the Dashboard shows `—` for these. RTT and ICE state/selected path are real. Backend
  telemetry for these lives in dotnet_SonicRelay issue #21.
- Publishing a **mix of several output devices** as one track is out of scope; a single
  endpoint is captured.
- `turns:` (TLS) TURN is opt-in and requires a certificate mounted into coturn; the
  derived defaults use `turn:` UDP/TCP only.

## Troubleshooting

- **No audio, ICE Connected**: ensure something is actually playing on the publisher; WASAPI
  loopback yields silence when the endpoint is idle.
- **Stuck at Connecting**: check the backend and coturn are reachable and
  `GET /api/webrtc/ice-servers` returns servers; try **Force relay** to isolate NAT issues.
- **Viewer never appears**: confirm both apps target the same backend and the session code
  is current (codes rotate/expire).
