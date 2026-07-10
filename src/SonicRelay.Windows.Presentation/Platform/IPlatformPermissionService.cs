namespace SonicRelay.Windows.Presentation.Platform;

/// <summary>Outcome of a platform permission/portal request.</summary>
public enum PlatformPermissionStatus
{
    /// <summary>The platform grants this capability without asking (e.g. WASAPI loopback).</summary>
    NotRequired,
    Granted,
    Denied,
    /// <summary>The permission mechanism itself is unavailable (e.g. missing portal).</summary>
    Unavailable
}

/// <summary>
/// Requests the OS-level permissions the publisher needs before capturing. WASAPI
/// loopback needs none, so the Windows implementation answers
/// <see cref="PlatformPermissionStatus.NotRequired"/>; Wayland sessions go through
/// xdg-desktop-portal and may prompt the user or be denied (issue #32). View models
/// consume only this contract and never talk to portals or COM directly.
/// </summary>
public interface IPlatformPermissionService
{
    /// <summary>Ensures system-audio capture is permitted, prompting if the platform requires it.</summary>
    Task<PlatformPermissionStatus> EnsureAudioCaptureAccessAsync(CancellationToken cancellationToken = default);
}
