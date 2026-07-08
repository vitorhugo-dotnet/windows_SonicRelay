# Audio Output Device Selection — Implementation Plan

Spec: `docs/superpowers/specs/2026-07-07-audio-output-selection-design.md`
Issue: #24

## Step 1 — Contracts + probe

- `AudioContracts.cs`: add `AudioOutputDevice` record + `IAudioOutputDeviceProbe`
  + `NullOutputDeviceProbe`.
- `WasapiOutputDeviceProbe.cs`: COM enumeration of active render endpoints.

## Step 2 — Backend device selection

- `WasapiLoopbackBackend`: `Func<string?>? preferredDeviceId` ctor arg; open the
  selected endpoint via `GetDevice`, fall back to default on failure. Add COM
  members `GetDevice`, `IMMDeviceCollection`, `DEVICE_STATE_ACTIVE`.

## Step 3 — Service API (TDD)

- `IAudioCaptureService`/`AudioCaptureService`: `PreferredDeviceId`,
  `SelectOutputDevice`, `GetOutputDevices`; inject `IAudioOutputDeviceProbe`.
- Test `AudioCaptureServiceTests` additions.

## Step 4 — Persistence (TDD)

- `AudioOutputPreferenceStore` (Core) + `AudioOutputPreferenceStoreTests`.

## Step 5 — Wiring + UI

- `PublisherRuntime`: create store, apply on startup, expose service + store.
- `AudioPage`: Audio source section (list, default, refresh, fallback note),
  disabled while capturing, persists on change.

## Step 6 — Verify + docs

- `dotnet build`, `dotnet test`; document in `docs/windows-publisher.md`
  including the multi-device mixing limitation.
