# WebRTC Publisher Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a testable multi-viewer WebRTC publisher orchestration layer wired to existing signaling abstractions.

**Architecture:** `WebRtcPublisher` translates signaling envelopes into per-viewer operations owned by `PeerConnectionManager`. Concrete WebRTC engines implement `IWebRtcPeerConnectionFactory`; this issue supplies contracts and orchestration without adding a native dependency.

**Tech Stack:** .NET 10, C# 14, xUnit, existing SonicRelay signaling contracts.

---

### Task 1: Define the executable contract through tests

**Files:**
- Create: `tests/SonicRelay.Windows.WebRtc.Tests/SonicRelay.Windows.WebRtc.Tests.csproj`
- Create: `tests/SonicRelay.Windows.WebRtc.Tests/WebRtcPublisherTests.cs`
- Modify: `SonicRelay.Windows.slnx`

- [ ] Add the focal xUnit project referencing WebRtc and Signaling.
- [ ] Write fake signaling, factory, and peer implementations plus tests for registration, offer emission, answer/ICE routing, local ICE, cleanup, audio fan-out, and diagnostics.
- [ ] Run `dotnet test tests/SonicRelay.Windows.WebRtc.Tests/SonicRelay.Windows.WebRtc.Tests.csproj --no-restore` and verify compilation fails because the new WebRTC contracts do not exist.

### Task 2: Add contracts and peer manager

**Files:**
- Create: `src/SonicRelay.Windows.WebRtc/WebRtcContracts.cs`
- Create: `src/SonicRelay.Windows.WebRtc/PeerConnectionManager.cs`
- Modify: `src/SonicRelay.Windows.WebRtc/SonicRelay.Windows.WebRtc.csproj`

- [ ] Define `IWebRtcPublisher`, `IPeerConnectionManager`, `IWebRtcPeerConnection`, `IWebRtcPeerConnectionFactory`, `PeerConnectionState`, `ViewerPeer`, `WebRtcPublisherOptions`, immutable signaling/audio payload records, and diagnostics records.
- [ ] Implement a concurrency-safe viewer dictionary, idempotent registration, targeted answer/ICE routing, audio fan-out, diagnostics snapshots, and deterministic disposal.
- [ ] Run the focal test project and retain only failures requiring publisher orchestration.

### Task 3: Implement signaling orchestration

**Files:**
- Create: `src/SonicRelay.Windows.WebRtc/WebRtcPublisher.cs`

- [ ] Implement `viewer.ready`, answer, ICE, `session.left`, and `session.ended` routing with required identity validation.
- [ ] Serialize offer and candidate payloads through `JsonSerializer.SerializeToElement` and send them through `ISignalingClient`.
- [ ] Subscribe to manager local-candidate changes and expose diagnostics/error notifications.
- [ ] Run the focal test project and verify all tests pass.

### Task 4: Verify scope and publish

**Files:** all files listed above plus the design and plan documents.

- [ ] Run `dotnet test tests/SonicRelay.Windows.WebRtc.Tests/SonicRelay.Windows.WebRtc.Tests.csproj --no-restore`.
- [ ] Run `dotnet build src/SonicRelay.Windows.WebRtc/SonicRelay.Windows.WebRtc.csproj --no-restore`.
- [ ] Inspect `git diff --check`, `git status -sb`, and the scoped diff.
- [ ] Commit the explicit scoped files on `main`, push `origin main`, close issue #8 with a summary, and verify remote/issue state.

## Plan self-review

Every design requirement maps to a task. Names are consistent across tasks, commands are focal, and the plan contains no deferred implementation work beyond the explicitly documented native-engine boundary.
