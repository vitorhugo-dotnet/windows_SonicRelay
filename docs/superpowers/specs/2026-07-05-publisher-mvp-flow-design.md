# Publisher MVP Flow Design

## Goal

Connect the existing API, signaling, token-storage, and WASAPI services to the WinUI shell so a non-admin user can log in, register or reuse a Windows publisher device, create a stream session, connect signaling, start and stop audio capture, and explicitly end the session.

## Architecture

`PublisherWorkflow` is a UI-independent presentation controller and the single owner of mutable publisher state. It receives the existing service abstractions, exposes an immutable `PublisherSnapshot`, publishes state changes, and serializes lifecycle operations. The WinUI application composes the concrete services once and shares the workflow across pages; pages contain only input handling and rendering.

The backend URL is validated before the runtime is created. Its origin is used for HTTP API calls and its `/signaling` endpoint for WebSocket signaling. Tokens remain in `UserScopedTokenStore` and no secret or signaling payload is copied into presentation state or logs.

## Flow and validation

1. Connection validates an absolute HTTP(S) backend URL, non-empty email, and non-empty password.
2. Login stores tokens through the existing authentication client, loads the current user, then reuses the first active `windows_publisher` device or registers the current machine.
3. Session creation is enabled only after authentication and device resolution. It creates the session, displays its code, opens signaling, and records the connected state.
4. Audio start is enabled only while a session exists and signaling is connected. Audio levels and diagnostics update the shared snapshot.
5. Ending a session stops audio, closes signaling, calls the backend end endpoint, and clears session state. App/window shutdown performs local cleanup; explicit end also calls the backend.
6. Viewer count starts at zero and is refreshed from the active-session endpoint after session creation and on demand when presentation state is refreshed.

## Errors and diagnostics

Expected validation and API/audio/signaling failures become concise actionable messages. State transitions are logged without passwords, access/refresh tokens, SDP, ICE candidates, or signaling payloads. Failures are visible and are never converted into successful state.

## UI

- Connection: backend URL, email, password, login button, authentication and device status.
- Session: create/end controls, prominent session code, signaling status, viewer count, and current error.
- Audio: start/stop controls, activity meter, capture diagnostics, gated by session readiness.
- Dashboard/top strip/diagnostics: shared summary and sanitized activity log.

## Testing

Unit tests use fakes for all existing service abstractions and verify validation gates, successful orchestration, viewer state, error visibility, explicit-end ordering, and shutdown cleanup. The smallest relevant verification is the presentation test project plus builds of the presentation and WinUI application projects.

## Scope boundaries

No admin operations, drivers, services, firewall changes, machine-wide storage, fake backend success, dependency upgrades, WebRTC expansion, or unrelated refactoring are included.
