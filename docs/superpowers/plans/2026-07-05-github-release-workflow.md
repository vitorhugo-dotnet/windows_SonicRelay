# GitHub Release Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, test, package, and publish a portable Windows x64 ZIP to GitHub Releases for version tags and manual dispatches.

**Architecture:** Add one release workflow that reuses the repository's CI commands, publishes the unpackaged app self-contained, archives it with build metadata, and publishes through GitHub CLI using the built-in token. Extend the existing repository structure test to guard the release contract and document user download/run steps.

**Tech Stack:** GitHub Actions, PowerShell, .NET 10, WinUI 3, GitHub CLI

---

### Task 1: Add the release workflow contract test

**Files:**
- Modify: `tests/Repository.Structure.Tests.ps1`
- Test: `tests/Repository.Structure.Tests.ps1`

- [ ] Add `.github/workflows/release.yml` to required paths and add regex assertions for tag/manual triggers, Windows runner, write permission, restore/build/tests, self-contained `win-x64` publish, metadata, ZIP naming, and `gh release create`.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Repository.Structure.Tests.ps1` and confirm it fails because `release.yml` is missing.

### Task 2: Implement release packaging and publication

**Files:**
- Create: `.github/workflows/release.yml`
- Test: `tests/Repository.Structure.Tests.ps1`

- [ ] Create the workflow with `push.tags: ['v*']` and `workflow_dispatch`, `contents: write`, and a Windows job.
- [ ] Restore and build the solution, then run the structure and solution test commands before publishing.
- [ ] Publish `SonicRelay.Windows.App.csproj` with `--runtime win-x64 --self-contained true`, write `BUILD-INFO.txt`, and archive the publish directory with versioned portable ZIP naming.
- [ ] Create the GitHub Release using `gh release create`, generated notes, build metadata notes, and the ZIP attachment.
- [ ] Re-run the focused structure test and confirm it passes.

### Task 3: Document portable release usage

**Files:**
- Modify: `README.md`

- [ ] Add instructions to download the `win-x64` ZIP from Releases, extract it into a user-writable folder, and launch without elevation.
- [ ] Document tag and manual release behavior plus the absence of an installer/MSIX.

### Task 4: Verify and publish

**Files:**
- Verify: `.github/workflows/release.yml`, `README.md`, `tests/Repository.Structure.Tests.ps1`

- [ ] Parse the workflow as YAML and run the focused repository structure test.
- [ ] Run a focused Release `dotnet publish` for `SonicRelay.Windows.App.csproj` with `win-x64` and self-contained output.
- [ ] Inspect `git diff --check`, stage only issue #12 files, commit on `main`, push `main`, and close issue #12 with a verification summary.
