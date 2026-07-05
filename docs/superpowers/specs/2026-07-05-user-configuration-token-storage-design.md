# User Configuration and Token Storage Design

## Scope

Add the configuration and token-storage foundations required by issue #4. This work does not add login UI, backend calls, or machine-wide state.

## Configuration

`PublisherConfiguration` contains absolute HTTP(S) backend and signaling URLs, a positive default maximum viewer count, and an optional development-mode flag. `UserConfigurationLoader` reads JSON from `%LOCALAPPDATA%/SonicRelay/WindowsPublisher/appsettings.json`, creates a documented template when the file is absent, and validates values before returning them. Invalid JSON, URLs, or viewer counts produce a configuration exception without exposing secrets.

## Token storage

`ITokenStore` exposes asynchronous save, load, and delete operations using `TokenSet` and `TokenStorageResult`. `UserScopedTokenStore` writes only below the current user's local application-data directory. It serializes tokens in memory and encrypts them with Windows DPAPI using `CurrentUser` scope before atomically replacing the token file.

If Windows DPAPI is unavailable or encryption/decryption fails, the store returns a clear `SecureStorageUnavailable` or `Failed` result. It never falls back to plaintext. Result messages and exceptions identify operations and paths only; token values are never included.

## Startup and tests

The WinUI application loads configuration during startup before creating its main window. Focused Core tests cover valid/invalid configuration, URL rejection, test-store save/load/delete semantics, user-store encrypted persistence, and the absence of token values from errors/results. Documentation identifies the user-scoped paths.

