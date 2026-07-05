# WASAPI Loopback Audio Capture Design

## Scope

Implement the Windows Publisher audio boundary for default-render-device WASAPI loopback capture. The component captures system output in user mode, emits immutable audio frames and metering snapshots, exposes lifecycle and diagnostics, and fails without crashing when the default endpoint is absent or invalidated. It does not encode, transport, install drivers, change global audio settings, or require elevation.

## Approach

The Audio project calls native Core Audio COM interfaces directly. This avoids a new runtime dependency and gives explicit control over shared-mode loopback. A public `IAudioCaptureService` owns the stable application contract while an internal backend isolates platform interop and makes lifecycle tests deterministic.

Alternatives rejected were adding NAudio, which violates the repository instruction not to update dependencies, and media-capture APIs, which do not provide the required default system-output loopback contract as directly as WASAPI.

## Contracts and data flow

`AudioDeviceInfo` identifies the selected default render endpoint and its mix format. `AudioFrame` validates non-empty data, positive sample rate/channel count, supported PCM/IEEE-float format, and a non-negative timestamp. `AudioLevelSnapshot` reports normalized peak and RMS values. `AudioCaptureDiagnostics` combines state, selected device, last error, latest level, byte count, and frame count.

`IAudioCaptureService` exposes start, stop, pause, resume, current diagnostics, state changes, frames, and level changes. Start resolves the current default render endpoint, initializes an `IAudioClient` in shared loopback mode, reads packets through `IAudioCaptureClient`, copies owned frame bytes, calculates levels for float32 or PCM16 mix formats, and publishes diagnostics. Capture work runs off the UI thread.

## Lifecycle and failures

States are `Stopped`, `Starting`, `Capturing`, `Paused`, `Stopping`, and `Faulted`. Repeated start/stop/pause/resume operations are idempotent where the requested state is already active. Operations are serialized. No default endpoint, unsupported mix format, COM initialization failure, and device invalidation are mapped to concise diagnostic errors and `Faulted`; they are not allowed to terminate the process. Stop after a fault releases native resources and returns to `Stopped`.

## UI and documentation

The Audio page owns one capture service and displays state, selected device, mix format, live level, captured counters, and last error. Buttons invoke lifecycle methods asynchronously and are enabled according to state. Navigating away does not implicitly destroy capture; page unload stops and disposes it.

Documentation explains that WASAPI loopback captures the shared-mode default output mix, normally produces silence when nothing is playing, follows the endpoint chosen at start, may stop if the endpoint is removed or changed, and excludes protected content.

## Testing

Focused xUnit tests cover DTO validation, state transitions, idempotence, diagnostics/counters, backend error mapping, and fake capture behavior. Native integration is kept behind the backend because CI cannot assume an interactive Windows audio endpoint.

## Design self-review

The design covers every issue acceptance criterion and diagnostic field without placeholders. Native capture, service lifecycle, UI visibility, documentation, and tests remain within the audio-capture issue; WebRTC, Opus, WebSocket media, drivers, services, and administrator operations are explicitly excluded.
