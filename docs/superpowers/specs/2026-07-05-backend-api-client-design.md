# Backend API Client Design

## Scope

Implement issue #5 as an HTTP-only client layer in `SonicRelay.Windows.ApiClient`. The layer covers login, token refresh, current-user lookup, publisher-device registration/listing, and stream-session creation/listing/ending. It does not add UI, WebSocket signaling, audio capture, or WebRTC.

## Backend contract

The implementation follows the current `dotnet_SonicRelay` protocol and endpoint source:

- `POST /auth/login?useCookies=false`, `POST /auth/refresh`, and `GET /auth/me`;
- `POST /api/devices/` and `GET /api/devices/`;
- `POST /api/sessions/`, `GET /api/sessions/active`, and `POST /api/sessions/{sessionId}/end`.

JSON uses the backend's camel-case field names. Device registration always sends `type: "windows_publisher"` and `platform: "windows"`. Base addresses are supplied through `HttpClient`; no production URL is embedded.

## Architecture

`IAuthApiClient`, `IDeviceApiClient`, and `ISessionApiClient` expose domain-specific asynchronous operations and DTOs isolated from UI code. Their implementations delegate transport work to a shared internal `ApiHttpClient`, which serializes JSON, attaches bearer tokens loaded from `ITokenStore`, maps failures, and deserializes successful responses.

For authenticated calls, a `401` triggers at most one refresh attempt when a refresh token exists. The refreshed token set is persisted through `ITokenStore`, then the original request is replayed once. Login and refresh calls never recursively trigger refresh.

## Errors

Transport and HTTP failures become `ApiClientException` with an `ApiErrorKind`: `Unauthorized`, `Forbidden`, `Validation`, `Conflict`, `NetworkUnavailable`, `BackendUnavailable`, or `Unknown`. Statuses 401, 403, 400/422, and 409 map directly. Connection/DNS failures map to network unavailable; 5xx and timeouts map to backend unavailable; all other non-success statuses map to unknown. Error messages may include backend problem text but never tokens.

## Verification

Focused xUnit tests use a fake `HttpMessageHandler` and in-memory `ITokenStore`. They verify routes, camel-case bodies, bearer headers, refresh-and-retry behavior, refreshed-token persistence, and every required error category. Documentation records backend URL configuration and the implemented HTTP surface.
