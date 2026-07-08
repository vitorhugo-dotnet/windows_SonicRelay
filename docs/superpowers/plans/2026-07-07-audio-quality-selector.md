# Audio Quality Selector — Implementation Plan

Spec: `docs/superpowers/specs/2026-07-07-audio-quality-selector-design.md`
Issue: #22

## Step 1 — Core model (TDD)

- `src/SonicRelay.Windows.Core/Audio/AudioQualityProfile.cs` — record, presets,
  `Validate`, `Custom`, `FromId`, `EstimateTraffic` + `AudioTrafficEstimate`.
- Test `tests/SonicRelay.Windows.Core.Tests/AudioQualityProfileTests.cs`.

## Step 2 — Persistence (TDD)

- `src/SonicRelay.Windows.Core/Audio/AudioQualityStore.cs` (mirror
  `RelayPreferenceStore`; JSON file; default High; corrupt-safe).
- Test `tests/SonicRelay.Windows.Core.Tests/AudioQualityStoreTests.cs`.

## Step 3 — WebRTC application

- WebRtc csproj → ProjectReference to Core.
- `OpusFrameAccumulator` — optional `frameDurationMs` (default 20); generalize
  frame math + divisibility guard. Add a 10 ms test.
- `SipSorceryPeerConnection` — optional `AudioQualityProfile` (default High);
  drive fmtp/encoder/accumulator/frame size from it. Add a mono-voice SDP test.
- `SipSorceryPeerConnectionFactory` — optional `Func<AudioQualityProfile>`.

## Step 4 — App wiring

- `PublisherRuntime` — create `AudioQualityStore`, wire into the factory, expose
  `AudioQuality` for the UI.

## Step 5 — UI

- `AudioPage.xaml`/`.cs` — selector + custom inputs + effective settings +
  estimate; disabled while capturing; persists on change.

## Step 6 — Verify + docs

- `dotnet build`, `dotnet test`; document the selector in `docs/windows-publisher.md`.
