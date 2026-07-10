namespace SonicRelay.Windows.Presentation.Platform;

/// <summary>
/// Registers or unregisters the publisher to start when the user signs in to the
/// OS session. Implementations are per-user and must never require elevation:
/// Windows uses the HKCU Run key or the per-user Startup folder; Linux uses an
/// XDG autostart desktop entry (issue #32).
/// </summary>
public interface IAutoStartService
{
    /// <summary>Whether launch-at-login is currently registered for this user.</summary>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>Registers (true) or removes (false) launch-at-login for this user.</summary>
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
