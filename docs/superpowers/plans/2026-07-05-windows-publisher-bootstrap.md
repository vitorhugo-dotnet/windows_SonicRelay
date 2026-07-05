# Windows Publisher Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the buildable .NET 10 and WinUI 3 repository foundation specified by GitHub issue #1.

**Architecture:** A WinUI composition-root project references five capability-focused class libraries. Core and ApiClient have isolated xUnit test projects; documentation describes future runtime flows without implementing them.

**Tech Stack:** .NET 10, C# 14, WinUI 3, Windows App SDK, xUnit, MSBuild.

---

### Task 1: Establish a failing structural acceptance test

**Files:**
- Create: `tests/Repository.Structure.Tests.ps1`

- [ ] Write a PowerShell test that asserts the solution, six production projects, two test projects, root configuration, and two architecture documents exist.
- [ ] Run `pwsh -NoProfile -File tests/Repository.Structure.Tests.ps1` and confirm it exits nonzero because the solution is absent.

### Task 2: Create solution and project scaffolding

**Files:**
- Create: `SonicRelay.Windows.slnx`, `global.json`, `Directory.Build.props`
- Create: `src/SonicRelay.Windows.{Core,ApiClient,Signaling,Audio,WebRtc}/*.csproj`
- Create: `src/SonicRelay.Windows.App/*`
- Create: `tests/SonicRelay.Windows.{Core,ApiClient}.Tests/*`

- [ ] Generate .NET 10 class libraries and xUnit test projects without restoring dependencies.
- [ ] Create a minimal unpackaged WinUI 3 application with `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, and `MainWindow.xaml.cs`.
- [ ] Add all projects to `SonicRelay.Windows.slnx`, app-to-library references, and test-to-production references.
- [ ] Run the structural test and confirm it passes.

### Task 3: Add repository configuration and documentation

**Files:**
- Modify: `README.md`
- Create: `.editorconfig`, `.gitignore`
- Create: `docs/windows-publisher.md`, `docs/architecture.md`

- [ ] Configure C# formatting, .NET/Visual Studio/Rider/WinUI exclusions, nullable references, implicit usings, and deterministic builds.
- [ ] Document the publisher purpose, prerequisites, build commands, current status, milestones, planned architecture, dependency direction, non-goals, and Mermaid diagrams.
- [ ] Confirm no document claims that authentication, signaling, audio capture, or WebRTC already exists.

### Task 4: Verify, commit, publish, and close issue

**Files:**
- Verify all files from Tasks 1-3.

- [ ] Run `dotnet restore SonicRelay.Windows.slnx`.
- [ ] Run `dotnet build SonicRelay.Windows.slnx --no-restore`.
- [ ] Run only `dotnet test tests/SonicRelay.Windows.Core.Tests/SonicRelay.Windows.Core.Tests.csproj --no-build` and the equivalent ApiClient command.
- [ ] Run the structural test again and inspect `git diff --check`, `git status --short`, and the staged diff summary.
- [ ] Commit all scoped files on `main` with `feat: bootstrap Windows Publisher solution`.
- [ ] Push `main` to `origin` and close GitHub issue #1 as completed.
