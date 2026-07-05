# Diagnostics, Logging, and Status Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add safe structured diagnostics, a status dashboard, and user-scoped report export.

**Architecture:** Put reusable diagnostics and redaction in Core, then adapt the existing publisher snapshot in the WinUI app. Persist only sanitized JSON Lines and Markdown under the current user's LocalAppData.

**Tech Stack:** .NET 10, C# 14, WinUI 3, xUnit, System.Text.Json

---

### Task 1: Redaction and diagnostics contracts

**Files:**
- Create: `src/SonicRelay.Windows.Core/Diagnostics/DiagnosticRedactor.cs`
- Create: `src/SonicRelay.Windows.Core/Diagnostics/DiagnosticsModels.cs`
- Test: `tests/SonicRelay.Windows.Core.Tests/DiagnosticRedactorTests.cs`

- [ ] Write tests asserting removal of credentials, JWTs, SDP, ICE candidates, email addresses, query secrets, and identifier masking.
- [ ] Run `dotnet test tests/SonicRelay.Windows.Core.Tests/SonicRelay.Windows.Core.Tests.csproj --filter DiagnosticRedactorTests` and confirm failure because the API does not exist.
- [ ] Implement `DiagnosticRedactor.Redact`, `MaskIdentifier`, and `BackendHost` plus the seven requested status models.
- [ ] Re-run the focused tests and confirm success.

### Task 2: Structured logging and report export

**Files:**
- Create: `src/SonicRelay.Windows.Core/Diagnostics/DiagnosticLog.cs`
- Create: `src/SonicRelay.Windows.Core/Diagnostics/DiagnosticReportExporter.cs`
- Test: `tests/SonicRelay.Windows.Core.Tests/DiagnosticReportTests.cs`

- [ ] Write tests proving report output is sanitized and export stays in a supplied user directory.
- [ ] Run the report tests and confirm the missing implementation failure.
- [ ] Implement bounded in-memory structured events, sanitized JSON Lines writes, Markdown rendering, and atomic user-scoped export.
- [ ] Re-run the report tests and confirm success.

### Task 3: Runtime and Diagnostics page

**Files:**
- Modify: `src/SonicRelay.Windows.App/PublisherRuntime.cs`
- Modify: `src/SonicRelay.Windows.App/Pages/DiagnosticsPage.xaml`
- Modify: `src/SonicRelay.Windows.App/Pages/DiagnosticsPage.xaml.cs`

- [ ] Expose backend and diagnostics services from `PublisherRuntime`.
- [ ] Project `PublisherSnapshot` into safe diagnostic models.
- [ ] Render all acceptance-criteria fields and add report export.
- [ ] Build only `src/SonicRelay.Windows.App/SonicRelay.Windows.App.csproj`.

### Task 4: Documentation and verification

**Files:**
- Modify: `docs/windows-publisher.md`

- [ ] Document log/report location and exactly what is safe to share.
- [ ] Run focused Core tests and the App build.
- [ ] Inspect `git diff --check` and the scoped diff before committing.
