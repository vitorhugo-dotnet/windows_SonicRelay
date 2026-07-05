# Fluent UI Shell Implementation Plan

1. Add a failing structural contract test for required shell artifacts and labels.
2. Add global light/dark design resources and reusable card/status styles.
3. Add six isolated WinUI pages, including dashboard mock-state cards.
4. Replace the bootstrap window with NavigationView, top status strip, Frame, and bottom log placeholder.
5. Add best-effort Mica setup with a theme-solid fallback and navigation-only code-behind.
6. Run the focused contract test and build the app project.
7. Review the diff, commit on `main`, push `origin/main`, and close issue #2.
