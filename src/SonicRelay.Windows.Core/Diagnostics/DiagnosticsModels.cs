namespace SonicRelay.Windows.Core.Diagnostics;

public enum DiagnosticStatus { Unknown, Healthy, Degraded, Unavailable }

public sealed record BackendDiagnosticStatus(DiagnosticStatus Status, string Host);
public sealed record AuthDiagnosticStatus(DiagnosticStatus Status, bool IsAuthenticated);
public sealed record DeviceDiagnosticStatus(DiagnosticStatus Status, string MaskedId);
public sealed record SessionDiagnosticStatus(DiagnosticStatus Status, string MaskedId, int ViewerCount);
public sealed record SignalingDiagnosticStatus(DiagnosticStatus Status, string ConnectionState);
public sealed record AudioCaptureDiagnosticStatus(DiagnosticStatus Status, string CaptureState, string OutputDevice, double Level);
public sealed record WebRtcPeerDiagnosticStatus(DiagnosticStatus Status, int PeerCount);

public sealed record DiagnosticsSnapshot(
    string AppVersion,
    string RuntimeVersion,
    string OsVersion,
    BackendDiagnosticStatus Backend,
    AuthDiagnosticStatus Auth,
    DeviceDiagnosticStatus Device,
    SessionDiagnosticStatus Session,
    SignalingDiagnosticStatus Signaling,
    AudioCaptureDiagnosticStatus AudioCapture,
    WebRtcPeerDiagnosticStatus WebRtc,
    IReadOnlyList<string> LastErrors);
