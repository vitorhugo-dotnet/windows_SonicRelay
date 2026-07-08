# Audio Output Device Selection Design

Issue: #24

## Goal

Let the Windows publisher choose *which* Windows output (render) endpoint is
captured and published, instead of always capturing the system default. Keep the
default as the initial behaviour, list active render devices, persist the choice
per user, and fall back safely to the default when a saved device disappears.

## Scope decision

The issue's scope mentions selecting "one or more" devices and mixing them into a
single track. The acceptance criteria, however, are about single-source
selection, listing, persistence, and safe fallback. Multi-device capture + mixing
is a much larger effort against the raw-WASAPI backend and is **explicitly
deferred** (documented as a limitation): the capture layer stays isolated so a
future per-app/multi-device filter can be added. This change delivers single
output-device selection end to end.

## Design

### Contracts (`SonicRelay.Windows.Audio`)

- `AudioOutputDevice(string Id, string Name, bool IsDefault)` — a listable render
  endpoint for the picker.
- `IAudioOutputDeviceProbe { IReadOnlyList<AudioOutputDevice> GetOutputDevices(); }`
  — enumerates active render endpoints. Windows impl `WasapiOutputDeviceProbe`
  uses `IMMDeviceEnumerator.EnumAudioEndpoints(Render, ACTIVE)` and flags the one
  matching `GetDefaultAudioEndpoint`. Fully defensive: returns `[]` on any COM
  failure. A `NullOutputDeviceProbe` returns `[]` (non-Windows/tests).

### Capture backend

- `WasapiLoopbackBackend` gains a `Func<string?>? preferredDeviceId` ctor arg,
  read at each `StartAsync`. When it yields an id, the capture loop opens that
  endpoint via `IMMDeviceEnumerator.GetDevice(id)`; if that fails (device gone),
  it **falls back** to `GetDefaultAudioEndpoint`. `Device` always reflects the
  endpoint actually captured. New COM members: `GetDevice`, `IMMDeviceCollection`,
  `DEVICE_STATE_ACTIVE`.

### Service (`AudioCaptureService` / `IAudioCaptureService`)

- New members: `string? PreferredDeviceId { get; }`,
  `void SelectOutputDevice(string? deviceId)` (null = system default; applies to
  the next capture start), and `IReadOnlyList<AudioOutputDevice> GetOutputDevices()`.
- The public (Windows) ctor builds `new WasapiLoopbackBackend(() => _preferredDeviceId)`
  and a `WasapiOutputDeviceProbe`. The internal ctor takes an injectable
  `IAudioOutputDeviceProbe` (default: null probe) so selection/enumeration are
  unit-testable without COM. The `IAudioCaptureBackend` interface is unchanged, so
  existing test fakes keep working.

### Persistence (`SonicRelay.Windows.Core`)

- `AudioOutputPreferenceStore` (mirrors `RelayPreferenceStore`) persists the
  selected device id (+ friendly name for display) in `audio-output.json`.
  Missing/corrupt → default (null). `null` id means "system default".

### Wiring + UI

- `PublisherRuntime` creates the store, calls
  `audio.SelectOutputDevice(store.SelectedDeviceId)` at startup, and exposes the
  capture service + store to the UI.
- `AudioPage` gains an **Audio source** section: a device list (default marked),
  a "System default" option, a Refresh button, and a note when a saved device is
  missing (detected by comparing `PreferredDeviceId` to the captured device id).
  The picker is disabled while capturing; selecting persists immediately and
  applies on the next capture start.

## Tests (`dotnet test`)

- `AudioOutputPreferenceStoreTests`: default null; round-trips id+name; corrupt →
  default.
- `AudioCaptureServiceTests`: `SelectOutputDevice` updates `PreferredDeviceId`;
  `GetOutputDevices` returns the injected probe's list.

## Acceptance criteria

- An audio-source section exists; the current default is shown; active render
  devices are listed; selection persists per user; works without admin; a missing
  saved device falls back safely to default; docs explain behaviour + the
  multi-device limitation. `dotnet build` + `dotnet test` pass.
