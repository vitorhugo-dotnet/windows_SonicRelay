# Fluent Windows 11 UI Shell Design

## Goal

Provide the SonicRelay publisher with a native WinUI 3 shell that clearly exposes future product areas without implementing backend, login, audio, signaling, or WebRTC behavior.

## Design

Use the WinUI `NavigationView` as the left navigation surface and a `Frame` as the content host. Dashboard, Connection, Session, Audio, Diagnostics, and Settings are separate pages so future behavior can evolve without growing `MainWindow` code-behind. The dashboard presents seven explicit mock-state cards; no state implies a successful connection.

Application resources define spacing, corner radii, typography, card styling, and semantic status brushes. Theme resources provide light and dark values using the system theme. The window requests Mica through `SystemBackdrop`; unsupported systems or initialization errors retain the root solid theme brush.

`MainWindow` contains only window/backdrop setup and UI navigation routing. No domain or transport project is called.

## Error handling

Backdrop activation is best-effort and catches unsupported/runtime failures. Navigation ignores unknown tags and keeps the current page.

## Testing

A focused PowerShell contract test checks the required pages, tokens, placeholder states, navigation entries, and fallback/backdrop setup. The app project build validates XAML compilation.

## Alternatives considered

- Inline content switching: fewer files, but tightly couples every feature surface to the window.
- Full MVVM framework: useful once interactive state exists, but unnecessary dependency and abstraction for static placeholders.

## Scope boundaries

No network calls, authentication, capture, signaling, WebRTC, dependency updates, or fabricated success state.
