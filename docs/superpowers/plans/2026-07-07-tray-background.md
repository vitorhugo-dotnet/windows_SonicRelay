# Background Run + System Tray — Implementation Plan

Spec: `docs/superpowers/specs/2026-07-07-tray-background-design.md`
Issue: #26

## Step 1 — Decision core (TDD, Presentation)

- `TrayApplicationController` + `TrayMenuItem` / `TrayCommand` / `TrayCloseDecision`.
- `TrayApplicationControllerTests`.

## Step 2 — Settings store (TDD, Core)

- `TrayBackgroundPreferenceStore` + `TrayBackgroundPreferenceStoreTests`.

## Step 3 — Platform services (App)

- Interfaces `ITrayIconService`, `IAppLifetimeService`, `IBackgroundNotifier`.
- Win32 impls: `Win32TrayIconService` (Shell_NotifyIcon + message window +
  TrackPopupMenu), `AppLifetimeService` (AppWindow hide/show/quit),
  `AppNotificationBackgroundNotifier`.

## Step 4 — Wiring

- `MainWindow`: intercept `AppWindow.Closing`/minimize; route tray commands;
  update tray from `Workflow.StateChanged`; explicit quit disposes the runtime.

## Step 5 — Settings UI + docs

- Settings section for the three toggles; `docs/windows-publisher.md` + manual QA.

## Step 6 — Verify

- `dotnet build`, `dotnet test`, repository structure test.
