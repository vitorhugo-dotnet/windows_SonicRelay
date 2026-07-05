# User Configuration and Token Storage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load validated per-user publisher configuration and securely save, load, and delete authentication tokens without administrator privileges.

**Architecture:** Keep platform-neutral contracts and JSON configuration in Core. Implement the Windows user store behind small file-system and DPAPI boundaries so behavior is focused and testable without adding packages or machine-wide state.

**Tech Stack:** .NET 10, System.Text.Json, Windows CryptProtectData/CryptUnprotectData, xUnit.

---

### Task 1: Configuration model and loader

**Files:**
- Create: `src/SonicRelay.Windows.Core/Configuration/PublisherConfiguration.cs`
- Create: `src/SonicRelay.Windows.Core/Configuration/UserConfigurationLoader.cs`
- Test: `tests/SonicRelay.Windows.Core.Tests/UserConfigurationLoaderTests.cs`

- [ ] Write tests for valid JSON, invalid URLs, and invalid viewer limits.
- [ ] Run the focused test class and confirm it fails because the types do not exist.
- [ ] Implement the model, validation, default path, and JSON loading.
- [ ] Run the focused test class and confirm it passes.

### Task 2: Token contracts and test semantics

**Files:**
- Create: `src/SonicRelay.Windows.Core/Storage/ITokenStore.cs`
- Create: `src/SonicRelay.Windows.Core/Storage/TokenSet.cs`
- Create: `src/SonicRelay.Windows.Core/Storage/TokenStorageResult.cs`
- Test: `tests/SonicRelay.Windows.Core.Tests/TokenStoreContractTests.cs`

- [ ] Write a small in-memory implementation in tests and verify save/load/delete behavior through `ITokenStore`.
- [ ] Run the focused test class and confirm it fails because the contracts do not exist.
- [ ] Implement the minimal contracts.
- [ ] Run the focused test class and confirm it passes.

### Task 3: Secure user-scoped implementation

**Files:**
- Create: `src/SonicRelay.Windows.Core/Storage/UserScopedTokenStore.cs`
- Test: `tests/SonicRelay.Windows.Core.Tests/UserScopedTokenStoreTests.cs`

- [ ] Write tests using a temporary user directory and injectable protector for encrypted save/load/delete, unavailable secure storage, and non-leaking errors.
- [ ] Run the focused test class and confirm it fails because the implementation does not exist.
- [ ] Implement atomic user-scoped file persistence and Windows DPAPI CurrentUser protection, with no plaintext fallback.
- [ ] Run the focused test class and confirm it passes.

### Task 4: Startup and documentation

**Files:**
- Modify: `src/SonicRelay.Windows.App/App.xaml.cs`
- Modify: `README.md`

- [ ] Load validated configuration before window activation.
- [ ] Document configuration and token paths plus secure-storage fallback behavior.
- [ ] Build the solution and run only the Core test project.
- [ ] Review the diff, commit on `main`, push `origin/main`, and close issue #4.

