# WASAPI Loopback Audio Capture Implementation Plan

**Goal:** Capture the default Windows system-output mix with native WASAPI loopback and expose safe lifecycle, frames, levels, diagnostics, UI controls, and focused tests.

**Architecture:** Public contracts and the service live in the Audio project. An internal capture backend separates state orchestration from native COM interop. The WinUI Audio page consumes only `IAudioCaptureService`.

**Tech Stack:** .NET 10, Core Audio COM/WASAPI, WinUI 3, xUnit 2.9.

### Task 1: Contracts and validation

- [ ] Create DTO and service-contract tests first and confirm they fail because the types are absent.
- [ ] Implement `AudioCaptureState`, format/device/frame/level/diagnostics DTOs, and `IAudioCaptureService`.
- [ ] Re-run the focused DTO tests.

### Task 2: Testable lifecycle service

- [ ] Add fake-backend tests for start, pause, resume, stop, idempotence, counters, and mapped failures; confirm red.
- [ ] Implement the serialized service lifecycle and event/diagnostic propagation.
- [ ] Re-run focused service tests and keep them green.

### Task 3: Native WASAPI backend

- [ ] Add Core Audio COM declarations and a backend that selects the default render endpoint, opens shared loopback capture, reads packets, emits owned frames, meters float32/PCM16 data, and cleans up deterministically.
- [ ] Map no-device and device-lost HRESULTs to stable capture errors.
- [ ] Build the Audio project to validate all interop signatures.

### Task 4: UI, docs, and wiring

- [ ] Replace the Audio placeholder with lifecycle buttons and diagnostic fields.
- [ ] Add the focused Audio test project to the solution.
- [ ] Update architecture and publisher documentation with behavior and limitations.

### Task 5: Verification and delivery

- [ ] Run only the Audio test project and targeted Audio/App builds.
- [ ] Inspect whitespace, status, and scoped diff.
- [ ] Commit on `main`, push `origin/main`, close issue 7, and read back remote state.

## Plan self-review

Every design requirement maps to an implementation or verification task. File/type names are consistent, no step defers behavior, and the plan does not add packages or unrelated refactors.
