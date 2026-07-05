# Diagnostics, Logging, and Status Dashboard Design

## Goal

Provide actionable Windows Publisher diagnostics without exposing credentials, signaling payloads, or personal data, and without requiring administrator privileges.

## Architecture

The Core project owns diagnostics contracts, redaction, structured JSON Lines logging, report rendering, and user-scoped paths. The App projects the current `PublisherSnapshot` into a diagnostics snapshot and displays it in the existing Diagnostics page. All untrusted strings pass through one redactor before entering logs or exported reports.

Logs and reports are written below `%LocalAppData%\SonicRelay\WindowsPublisher`; no Event Log, Program Files, registry, service, or machine-wide storage is used. Export produces Markdown suitable for attaching to a support request.

## Data and safety

`DiagnosticsSnapshot` reports backend, authentication, device, session, signaling, audio capture, and WebRTC peer status. Device/session identifiers are masked, the backend is reduced to scheme and host, and recent errors are sanitized. Redaction removes token/password values, JWT-like values, SDP bodies, ICE candidate bodies, email addresses, and sensitive URI query parameters.

## UI

The Diagnostics page shows application/runtime/OS versions, backend host, masked identifiers, signaling and viewer status, capture/device/level information, recent sanitized errors, and an Export button. Export feedback contains only the resulting user-scoped path or a sanitized failure.

## Verification

Focused Core tests prove redaction, masking, host-only URL formatting, report safety, and user-scoped export. The App project build verifies WinUI bindings and integration.
