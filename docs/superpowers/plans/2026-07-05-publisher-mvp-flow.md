# Publisher MVP Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the WinUI publisher shell to the existing authenticated backend, device, session, signaling, and WASAPI services.

**Architecture:** Add a UI-independent presentation project containing one workflow controller and immutable state snapshots. Compose concrete services once in the WinUI app and let each page render and invoke the shared workflow asynchronously.

**Tech Stack:** C# 14, .NET 10, WinUI 3, xUnit, existing SonicRelay service projects

---

### Task 1: Test the publisher workflow contract

**Files:**
- Create: `tests/SonicRelay.Windows.Presentation.Tests/SonicRelay.Windows.Presentation.Tests.csproj`
- Create: `tests/SonicRelay.Windows.Presentation.Tests/PublisherWorkflowTests.cs`
- Modify: `SonicRelay.Windows.slnx`

- [ ] Create fakes implementing `IAuthApiClient`, `IDeviceApiClient`, `ISessionApiClient`, `ISignalingClient`, and `IAudioCaptureService`.
- [ ] Add tests proving required login fields are rejected, session/audio commands are gated, login resolves a publisher device, session creation connects signaling and exposes the code, failures remain visible, explicit end stops/close/ends, and disposal cleans local resources.
- [ ] Run `dotnet test tests/SonicRelay.Windows.Presentation.Tests/SonicRelay.Windows.Presentation.Tests.csproj --no-restore` and confirm compilation fails because the presentation types do not exist.

### Task 2: Implement the presentation controller

**Files:**
- Create: `src/SonicRelay.Windows.Presentation/SonicRelay.Windows.Presentation.csproj`
- Create: `src/SonicRelay.Windows.Presentation/PublisherSnapshot.cs`
- Create: `src/SonicRelay.Windows.Presentation/PublisherWorkflow.cs`
- Modify: `SonicRelay.Windows.slnx`

- [ ] Define the immutable snapshot with authentication, device, session code/id, signaling, audio level/state, viewer count, busy/error, and sanitized log state.
- [ ] Implement async login/device resolution, session creation/signaling connection, active-session viewer refresh, audio start/stop, explicit session end, error translation, event subscription, and idempotent disposal.
- [ ] Run the presentation tests and confirm all tests pass.

### Task 3: Compose runtime services

**Files:**
- Create: `src/SonicRelay.Windows.App/PublisherRuntime.cs`
- Modify: `src/SonicRelay.Windows.App/App.xaml.cs`
- Modify: `src/SonicRelay.Windows.App/MainWindow.xaml.cs`
- Modify: `src/SonicRelay.Windows.App/SonicRelay.Windows.App.csproj`

- [ ] Build `HttpClient`, API clients, user token store, signaling client, audio service, and workflow from the validated backend URL.
- [ ] Expose the runtime to pages, replace it safely when the backend URL changes, and dispose it when the window closes without blocking the UI thread.

### Task 4: Wire the MVP pages

**Files:**
- Modify: `src/SonicRelay.Windows.App/Pages/ConnectionPage.xaml`
- Modify: `src/SonicRelay.Windows.App/Pages/ConnectionPage.xaml.cs`
- Modify: `src/SonicRelay.Windows.App/Pages/SessionPage.xaml`
- Modify: `src/SonicRelay.Windows.App/Pages/SessionPage.xaml.cs`
- Modify: `src/SonicRelay.Windows.App/Pages/AudioPage.xaml`
- Modify: `src/SonicRelay.Windows.App/Pages/AudioPage.xaml.cs`
- Modify: `src/SonicRelay.Windows.App/Pages/DashboardPage.xaml`
- Modify: `src/SonicRelay.Windows.App/Pages/DashboardPage.xaml.cs`
- Modify: `src/SonicRelay.Windows.App/Pages/DiagnosticsPage.xaml`
- Modify: `src/SonicRelay.Windows.App/Pages/DiagnosticsPage.xaml.cs`
- Modify: `src/SonicRelay.Windows.App/MainWindow.xaml`

- [ ] Bind login inputs and status to the workflow, keeping password contents out of state/logs.
- [ ] Bind session creation/end, prominent code, signaling state, viewer count, and errors.
- [ ] Replace page-owned audio capture with workflow start/stop and shared diagnostics.
- [ ] Render shared dashboard, shell status, and sanitized activity log; unsubscribe page events on unload.

### Task 5: Verify and publish

**Files:**
- Review all files above with `git diff --check` and `git diff --stat`.

- [ ] Run the presentation test project.
- [ ] Build `src/SonicRelay.Windows.App/SonicRelay.Windows.App.csproj` with no restore.
- [ ] Confirm no sensitive values or unrelated changes appear in the diff.
- [ ] Commit all scoped changes on `main`, push `main`, close GitHub issue #9, and read back its closed state.
