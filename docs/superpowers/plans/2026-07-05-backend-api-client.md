# Backend API Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add tested HTTP clients for SonicRelay authentication, publisher devices, and stream-session lifecycle.

**Architecture:** Three public typed clients share an internal JSON transport that owns bearer attachment, one-shot refresh, token persistence, and typed error mapping. DTOs mirror the documented backend contract and remain independent of the UI.

**Tech Stack:** .NET 10, `HttpClient`, `System.Text.Json`, xUnit.

---

### Task 1: Public contracts and DTOs

**Files:**
- Create: `src/SonicRelay.Windows.ApiClient/Authentication/AuthContracts.cs`
- Create: `src/SonicRelay.Windows.ApiClient/Devices/DeviceContracts.cs`
- Create: `src/SonicRelay.Windows.ApiClient/Sessions/SessionContracts.cs`
- Modify: `src/SonicRelay.Windows.ApiClient/SonicRelay.Windows.ApiClient.csproj`
- Test: `tests/SonicRelay.Windows.ApiClient.Tests/ApiContractTests.cs`

- [ ] Write compile-time contract tests that instantiate login, device, and session requests and reference every interface operation.
- [ ] Run `dotnet test tests/SonicRelay.Windows.ApiClient.Tests/SonicRelay.Windows.ApiClient.Tests.csproj --filter FullyQualifiedName~ApiContractTests` and confirm missing-type failures.
- [ ] Add interfaces and immutable record DTOs with `CancellationToken` support, and reference Core for `ITokenStore` integration.
- [ ] Re-run the focused contract tests and confirm they pass.

### Task 2: Request construction and bearer authentication

**Files:**
- Create: `src/SonicRelay.Windows.ApiClient/ApiHttpClient.cs`
- Create: `src/SonicRelay.Windows.ApiClient/Authentication/AuthApiClient.cs`
- Create: `src/SonicRelay.Windows.ApiClient/Devices/DeviceApiClient.cs`
- Create: `src/SonicRelay.Windows.ApiClient/Sessions/SessionApiClient.cs`
- Test: `tests/SonicRelay.Windows.ApiClient.Tests/ApiRequestTests.cs`
- Test: `tests/SonicRelay.Windows.ApiClient.Tests/TestDoubles.cs`

- [ ] Add fake-handler tests asserting login, me, device, and session paths; camel-case JSON bodies; and bearer headers.
- [ ] Run the `ApiRequestTests` filter and confirm failures because implementations are missing.
- [ ] Implement the minimal shared sender and three clients using relative URIs and `System.Text.Json` web defaults.
- [ ] Re-run the focused tests and confirm they pass.

### Task 3: Typed errors and token refresh

**Files:**
- Create: `src/SonicRelay.Windows.ApiClient/Errors/ApiClientException.cs`
- Modify: `src/SonicRelay.Windows.ApiClient/ApiHttpClient.cs`
- Test: `tests/SonicRelay.Windows.ApiClient.Tests/ApiErrorTests.cs`
- Test: `tests/SonicRelay.Windows.ApiClient.Tests/TokenRefreshTests.cs`

- [ ] Add tests for 401, 403, 400/422, 409, network failure, timeout/5xx, unknown status, and single refresh/retry with token persistence.
- [ ] Run the two focused test-class filters and confirm expected failures.
- [ ] Implement `ApiErrorKind`, status/transport mapping, non-sensitive backend error extraction, and one-shot refresh using `/auth/refresh`.
- [ ] Re-run both focused test classes and confirm they pass.

### Task 4: Documentation and release

**Files:**
- Modify: `README.md`
- Modify: `docs/windows-publisher.md`

- [ ] Document the configured base URL, implemented routes, secure token reuse, and HTTP-only/non-admin boundary.
- [ ] Run the ApiClient test project and build only the ApiClient project.
- [ ] Review `git diff --check`, status, and the scoped diff.
- [ ] Commit all issue #5 files on `main`, push `origin/main`, close issue #5 with the commit reference, and verify remote issue state.
