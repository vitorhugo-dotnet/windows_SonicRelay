# Audio Quality Selector Design

Issue: #22

## Goal

Let the Windows publisher choose an Opus audio-quality profile so the user can
trade bandwidth against fidelity. Today the encoder is hard-wired to 128 kbps
stereo music in `SipSorceryPeerConnection`. This adds selectable presets (Voice /
Balanced / High / Custom), persists the choice, applies it to the next stream,
and shows the effective codec settings plus an estimated traffic figure. WASAPI
loopback capture stays as-is; only the Opus encode profile becomes configurable.
Sample rate stays fixed at 48 kHz for Opus/WebRTC compatibility.

## Constraints (from issue #22)

- Presets: Voice (mono, 32 kbps, 20 ms), Balanced (stereo, 96 kbps, 20 ms),
  High (stereo, 128 kbps, 20 ms), Custom (16–192 kbps, 1–2 ch, 10/20/40 ms).
- Persist locally; restore on startup. Apply before starting the stream. Block
  live changes while streaming (or require a restart).
- Show effective codec settings (Opus, bitrate, channels, frame duration, sample
  rate) and estimated traffic (kbps, MB/min, MB/hour).
- Never send raw PCM/Float32 over the network (already true — Opus only).
- No admin, no local listening port. Useful logs where they aid debugging.

## Model (`SonicRelay.Windows.Core/Audio`)

```csharp
public sealed record AudioQualityProfile(
    string Id, string DisplayName, int Channels,
    int OpusBitrateKbps, int FrameDurationMs, int SampleRateHz)
```

- `Validate()` enforces channels ∈ {1,2}, bitrate ∈ [16,192], frame ∈ {10,20,40},
  sample rate == 48000; throws `ArgumentException` otherwise.
- Static presets `Voice`, `Balanced`, `High`; `Presets` list; `Default => High`
  (preserves today's shipping quality and the existing offer-SDP test).
- `Custom(channels, bitrate, frame)` builds and validates a `"custom"` profile.
- `FromId(id)` resolves a preset by id (null for `custom`/unknown).
- `EstimateTraffic()` → `AudioTrafficEstimate(Kbps, MegabytesPerMinute,
  MegabytesPerHour)` computed from the Opus target bitrate (simple, media-payload
  based; documented as approximate).

`AudioQualityStore` (mirrors `RelayPreferenceStore`) persists the *whole* selected
profile as JSON in `audio-quality.json` next to the other preferences, caches the
current value in memory, defaults to `AudioQualityProfile.Default`, and tolerates
a missing/corrupt file. The WebRTC factory reads it when creating each connection.

## WebRTC application

- `SonicRelay.Windows.WebRtc` gains a project reference to `…Core`.
- `SipSorceryPeerConnection` takes an optional `AudioQualityProfile` (defaulting
  to `AudioQualityProfile.Default`). It drives:
  - the advertised Opus `AudioFormat` channel count + `stereo`/`sprop-stereo` and
    `maxaveragebitrate` fmtp values,
  - the `OpusEncoder` channel count, `Bitrate`, and `SignalType` (music for
    stereo, voice for mono),
  - the `OpusFrameAccumulator` channel count and frame duration,
  - `SamplesPerChannel` = 48000 × frameMs ÷ 1000 (480/960/1920).
- `OpusFrameAccumulator` gains an optional `frameDurationMs` (default 20) and
  generalizes its per-frame sample math; the divisibility guard becomes
  `(sampleRate × frameMs) % 1000 == 0` (still rejects 44101 @ 20 ms).
- `SipSorceryPeerConnectionFactory` gains an optional `Func<AudioQualityProfile>`
  read at creation time, so a settings change applies to the next viewer/stream.
- `PublisherRuntime` creates the `AudioQualityStore`, wires
  `() => store.CurrentProfile` into the factory, and exposes the store for the UI.

## UI (`AudioPage`)

- A quality selector (ComboBox of presets + Custom) with custom bitrate/channel/
  frame inputs shown only for Custom.
- An "effective settings" block: codec (Opus), bitrate, channels, frame duration,
  sample rate — and the estimated kbps / MB-min / MB-hour.
- The selector is disabled while capture is running; a hint says the change
  applies when capture is (re)started. Selecting a profile saves it immediately.

## Tests (`dotnet test`)

- `AudioQualityProfileTests`: presets have the specified values; `Validate` accepts
  valid and rejects out-of-range bitrate/channels/frame/sample-rate; `Custom`
  validates; `FromId` resolves presets and returns null for custom/unknown;
  `EstimateTraffic` math (e.g. 96 kbps ⇒ 12 000 B/s ⇒ 0.72 MB/min ⇒ 43.2 MB/h).
- `AudioQualityStoreTests`: default is High; round-trips a custom profile through a
  temp file; missing/corrupt file falls back to default.
- `OpusFrameAccumulatorTests`: a 10 ms stereo frame yields 480×2 samples (added);
  existing 20 ms tests stay green.
- `SipSorceryPeerConnection`: the default profile still emits `stereo=1` +
  `maxaveragebitrate=128000` (existing test unchanged); a mono voice profile emits
  `stereo=0` and its `maxaveragebitrate` (added).

## Acceptance criteria

- User can select Voice / Balanced / High / Custom; the choice persists and
  restores. The stream uses the selected Opus bitrate/channels/frame. UI shows the
  effective settings and estimated traffic. No raw PCM/Float32 on the wire. No
  admin. Start/stop capture flow unaffected. `dotnet build` + `dotnet test` pass.
