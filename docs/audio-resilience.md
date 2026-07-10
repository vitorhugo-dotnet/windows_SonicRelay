# Audio resilience: pacing, bounded buffering, Opus FEC and diagnostics

Implements issue #31. The goal is **no perceptible audio interruptions under
small, transient packet loss and jitter** — not a literal 0% UDP loss rate,
which no WebRTC deployment can guarantee.

## Publisher pipeline

```text
WASAPI capture (callback — never blocks)
  -> AudioFrameLatencyQueue        bounded by an audio-duration budget (150 ms)
  -> PCM normalization/resampling  (PcmAudioConverter + OpusFrameAccumulator)
  -> exact Opus frame accumulation (10/20/40 ms profiles)
  -> Opus encoding                 (OpusEncoderFactory — explicit FEC/VBR config)
  -> RtpPacketPacer                monotonic deadline per frame (budget 200 ms)
  -> SIPSorcery SendAudio
```

Media only ever lives in these in-memory buffers. Nothing is persisted to
Redis, a database, or disk, and nothing is buffered for disconnected or
non-negotiated peers (their accumulator and pacer backlog are cleared instead).

### RTP pacing (`RtpPacketPacer`)

The accumulator can yield several encoded frames from one capture callback, and
SIPSorcery's `SendAudio` advances RTP timestamps without pacing transmission.
Previously those frames left as a burst, which the receiver's jitter buffer
must absorb (latency) or drop (audible gap, reported as loss/late packets).

The pacer sends exactly one packet per frame deadline on the `Stopwatch`
monotonic clock. Deadlines are absolute — each advances by exactly one frame
duration from the previous deadline, so `Task.Delay` scheduler error does not
accumulate as drift. If the source stalls for more than a frame, the schedule
re-anchors and the next packet is sent immediately rather than replaying a
stale schedule.

### Latency budgets instead of frame counts

Both buffers bound *audio duration*, not an arbitrary item count:

| Buffer | Location | Budget | Overflow behavior |
| --- | --- | --- | --- |
| `AudioFrameLatencyQueue` | capture → encoder (`WebRtcAudioBridge`) | 150 ms (constructor-tunable) | drop oldest, count |
| `RtpPacketPacer` backlog | encoder → wire (per peer) | 200 ms | drop oldest, count |

Under overload (slow transport, CPU pressure) the oldest audio is discarded and
counted; latency cannot grow without bound.

## Opus packet-loss resilience

The SDP has always advertised `useinbandfec=1`, but that alone only tells the
remote decoder redundancy may be present. `OpusEncoderFactory` now configures
the Concentus encoder explicitly for every profile:

```csharp
UseVBR = true;
UseConstrainedVBR = true;   // bounds the bitrate spikes that aggravate loss
UseDTX = false;             // silence suppression reads as a dropout for system audio
UseInbandFEC = true;
PacketLossPercent = profile.ExpectedPacketLossPercent;  // default 10, 0–100
```

### Which profiles actually benefit from in-band FEC

Opus in-band FEC (LBRR) is produced **only by the SILK/hybrid coder**:

- **`voice` preset (mono, 32 kbps, 20 ms, VOIP application)** — SILK-capable;
  FEC produces real redundancy here. This preset doubles as the *resilient
  profile* for lossy networks.
- **`balanced`/`high` stereo music presets** — run in CELT mode at their
  bitrates; the FEC flag is accepted but yields little or no redundancy. These
  profiles rely on receiver-side concealment (PLC) plus the pacing and bounded
  buffering above. They are deliberately **not** downgraded: high-quality
  stereo playback remains the default, and users on a bad network can pick the
  voice/resilient preset (or a custom mono profile).

`ExpectedPacketLossPercent` is part of `AudioQualityProfile`, persists through
the existing audio-quality store, and is validated to 0–100.

## Diagnostics

Publisher-side counters (`PeerConnectionDiagnostics.AudioSend`, per viewer):

- `EncodedPacketsSent`, `PacedPacketsDropped`, `SendFailures`
- `PacingBacklogPackets` / `PacingBacklogDuration`
- `FrameDurationMs`, `OpusBitrateKbps`, `Channels`, `ProfileId`
- `InbandFecEnabled`, `ExpectedPacketLossPercent`

Capture-side counters (`WebRtcAudioBridge.Diagnostics`):

- `FramesCaptured`, and per queue: `FramesEnqueued`, `FramesDropped`, `Depth`,
  `QueuedDuration`, `MaxObservedDuration`

`PeerConnectionDiagnostics.SelectedCandidatePair` now reports the nominated ICE
pair as candidate *types only* (e.g. `host->relay`), enough to distinguish
Direct from Relay without exposing addresses or ports. No SDP bodies, full ICE
candidates, or media payloads are logged.

The Flutter viewer collects the receiver half (packetsLost, concealedSamples,
jitterBufferDelay, fecPacketsReceived, …) — see `flutter_SonicRelay`'s
`docs/receiver-webrtc-stats.md`. Correlating the two sides distinguishes:

- **real network loss** — receiver `packetsLost` grows, publisher drop counters flat;
- **late packets** — receiver `packetsDiscarded`/concealment grows with jitter, `packetsLost` mostly flat;
- **publisher bursts** — mitigated by the pacer; visible as jitter-buffer delay spikes if pacing regresses;
- **local drops** — publisher `FramesDropped`/`PacedPacketsDropped` grow (back-pressure or CPU);
- **TURN-only problems** — metrics degrade only when `SelectedCandidatePair` contains `relay`.

## Manual validation matrix

These need real hardware/networks and must be recorded per release (see also
`webrtc-e2e-test.md`):

1. **Direct UDP** — PC on Ethernet, phone on 5/6 GHz Wi-Fi. Expect
   `host->host`/`host->srflx`, near-zero loss, no audible cuts.
2. **TURN over UDP** — force relay, same LAN. Expect `relay` in the pair and
   comparable quality with slightly higher RTT.
3. **Controlled degradation** — inject ~1%, 3% and 5% loss plus jitter (e.g.
   `clumsy` on Windows or a router with netem). The stream may *report* loss,
   but must not produce frequent audible cuts at 1–3%; latency must stay flat.
4. **CPU pressure** — load the publisher CPU; local drop counters may grow but
   latency and the pacing backlog must stay within their budgets.

## Non-goals

- Mathematically zero UDP packet loss.
- Retransmitting old audio through Redis/database/filesystem.
- Replacing WebRTC with TCP to avoid loss.
- A second jitter buffer on top of libwebrtc's before its metrics are understood.
- Forcing TURN without evidence the direct pair is worse.
