# WebRTC Publisher Skeleton Design

## Scope

Create an isolated `SonicRelay.Windows.WebRtc` orchestration layer for one peer connection per viewer. The layer consumes validated signaling envelopes, creates and routes offers, answers, and ICE candidates, exposes diagnostics, and accepts audio frames through an explicit boundary. It does not implement a media server, WebSocket audio, local listeners, firewall changes, drivers, UI integration, or privileged operations.

## Approach

`WebRtcPublisher` implements both `IWebRtcPublisher` and `ISignalingMessageHandler`. It delegates per-viewer lifecycle to `IPeerConnectionManager`, which creates `IWebRtcPeerConnection` instances through a factory and owns the viewer registry. A peer emits local ICE and state/diagnostic changes; the publisher converts local ICE into signaling envelopes for the matching viewer.

The initial implementation deliberately does not select a native WebRTC package. Concrete SDP generation, ICE transport, Opus encoding, candidate-pair inspection, and RTT collection remain behind `IWebRtcPeerConnectionFactory`. This keeps the public orchestration testable and avoids hiding a nonfunctional fake transport or adding an unapproved dependency.

## Contracts and flow

- `viewer.ready` requires a session and viewer ID, registers one peer, creates an offer, and sends `webrtc.offer`.
- `webrtc.answer` and `webrtc.ice_candidate` require a known viewer and route only to that viewer's peer.
- `session.left` removes the addressed viewer; `session.ended` removes every viewer.
- Duplicate viewer readiness is idempotent and does not create a second peer or offer.
- Local ICE emitted by a peer is sent as `webrtc.ice_candidate` with the active session/viewer routing metadata.
- Failures update `LastError` and are rethrown so callers can observe protocol or peer failures.

## Audio and diagnostics

`WebRtcAudioFrame` is an immutable transport-neutral PCM frame. `PushAudioFrameAsync` fans the frame out to registered peers. Concrete peers must convert capture output to interleaved signed 16-bit PCM, 48 kHz, one or two channels, normally in 10 ms or 20 ms packets before Opus encoding. Resampling, float-to-PCM conversion, channel remixing, buffering, and Opus encoding are intentionally not implemented by the orchestration layer.

Diagnostics expose viewer count, each viewer's ICE state, optional selected candidate pair, optional RTT, and the last WebRTC error. State changes are observable without referencing UI types.

## Testing

Focused tests use fake peers and signaling to verify peer registration, duplicate readiness, offer emission, answer/ICE routing, local ICE emission, audio fan-out, diagnostics, viewer cleanup, and session cleanup. No network sockets or administrator privileges are required.

## Design self-review

The design covers every issue acceptance criterion, keeps multi-viewer identity explicit, and contains no placeholders. The lack of a concrete WebRTC engine is stated as a known limitation rather than represented as completed media transport.
