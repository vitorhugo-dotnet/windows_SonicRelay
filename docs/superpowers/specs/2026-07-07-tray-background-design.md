# Background Run + System Tray Design

Issue: #26

## Goal

Keep the Windows publisher running (signaling, WebRTC, audio, stream status) when
the main window is minimized or closed, and add a system-tray experience to
control the stream without the window open. Explicit "Quit" tears everything down
cleanly. No admin, no driver, no Windows Service, no local port.

## Foundation that already holds

The publisher pipeline is **not** owned by the main window: `PublisherRuntime`
(signaling + `PeerConnectionManager` + audio bridge + workflow) is owned by `App`
and exposed via `App.Runtime`. The window only observes `Workflow.StateChanged`.
So hiding/closing the window already leaves the stream running — today the gap is
that `MainWindow.OnClosed` disposes the runtime. This issue changes *when* teardown
happens (only on explicit quit) and adds the tray UX.

## Decision core (Presentation, fully unit-tested)

`TrayApplicationController` holds no UI/interop — it decides behaviour and derives
the tray menu, so the logic is testable and lives outside XAML.

- `TrayCloseDecision DecideOnClose(bool streamActive)` — `Hide` when the
  "keep running in tray" setting is on **or** a stream is active; otherwise `Quit`.
- `TrayCloseDecision DecideOnMinimize()` — `Hide` when "keep running in tray" is on
  (minimize hides to tray), else `MinimizeNormally`.
- `IReadOnlyList<TrayMenuItem> BuildMenu(PublisherSnapshot? state)` — produces:
  `Open`, a disabled status header, `Start stream`/`Stop stream` (per session +
  audio state), `Copy session code` (only when a code exists),
  `Reconnect signaling` (only when disconnected/reconnecting with a session),
  and `Quit`.
- `string TooltipFor(PublisherSnapshot? state)` — short status text.
- `bool ShouldNotify(...)` helpers for viewer connect/disconnect and stream end so
  normal reconnect churn does not spam notifications.

`TrayMenuItem(TrayCommand Command, string Label, bool Enabled)` and enums
`TrayCloseDecision { Hide, Quit, MinimizeNormally }`,
`TrayCommand { Open, Status, StartStream, StopStream, CopySessionCode,
ReconnectSignaling, Quit }`.

## Settings (Core, unit-tested)

`TrayBackgroundPreferenceStore` (mirrors `RelayPreferenceStore`, `tray.json`):
`KeepRunningInTray` (default **true**), `StartMinimized` (default false),
`ShowNotifications` (default true). Corrupt/missing → defaults.

## Platform services (App, behind interfaces)

- `ITrayIconService` — `Show/Hide`, `UpdateMenu(items)`, `UpdateTooltip`, events
  `CommandInvoked(TrayCommand)` and `Activated` (double-click). Win32 impl
  `Win32TrayIconService` uses `Shell_NotifyIcon` + a message-only window +
  `TrackPopupMenu`, isolated so the streaming layer never sees interop details.
- `IAppLifetimeService` — `HideToTray()`, `ShowFromTray()` (restore + focus),
  `QuitAsync()` (dispose runtime + exit). Impl over `AppWindow`/`OverlappedPresenter`.
- `IBackgroundNotifier` — `Notify(title, message)`; impl over Windows App SDK
  `AppNotificationManager`, guarded by the ShowNotifications setting.

## Wiring

- `MainWindow` hooks `AppWindow.Changed`/`Closing` and `OverlappedPresenter` state:
  - `Closing`: cancel the default close and `HideToTray()` when
    `DecideOnClose(streamActive)` is `Hide`; otherwise let quit proceed.
  - Minimize: hide to tray when the setting says so.
  - Copy-session-code uses the clipboard; other commands call the workflow
    (`StartAudioAsync`/`StopAudioAsync`/session) or lifetime (`Show`/`Quit`).
- The tray icon is created on first launch and updated from `Workflow.StateChanged`.
- Explicit `Quit` disposes the runtime (audio → WebRTC → signaling) then exits;
  the app also disposes the tray icon so it never lingers after exit.
- No duplicate sessions/devices: hiding/showing never re-runs login or
  session-create; it only toggles window visibility.

## Notifications

Emit on: minimized-to-tray (first time), viewer connected/disconnected, stream
started/stopped, and stream ended by auth/session error. Suppressed during normal
reconnect loops and when ShowNotifications is off.

## Tests (`dotnet test`)

- `TrayApplicationControllerTests`: close/minimize decisions across the setting and
  stream-active combinations; menu contents for idle / signed-in / streaming /
  disconnected / has-code states; tooltip text; notify gating.
- `TrayBackgroundPreferenceStoreTests`: defaults, round-trip, corrupt fallback.

The Win32 tray/lifetime/notification impls are verified by `dotnet build` and the
documented manual QA (they are interop and not exercised by `dotnet test`).

## Manual QA (documented in PR)

Minimize → stream continues; close during a stream → app stays in tray; tray icon
visible; tray menu reopens / stops the stream / quits; explicit quit closes audio,
WebRTC, signaling, and the process; viewer stays connected while hidden; reopening
shows the right state; no admin/port; no duplicate device/session.

## Acceptance criteria

As listed in the issue; `dotnet build` + `dotnet test` pass.
