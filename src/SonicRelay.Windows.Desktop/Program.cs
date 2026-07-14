using Avalonia;

namespace SonicRelay.Windows.Desktop;

/// <summary>
/// Entry point for the shared Avalonia desktop shell (issue #32, phase 2). This is the
/// cross-platform host: today it ships the new Windows shell side-by-side with the WinUI
/// app; the same shell will drive Linux once the PipeWire adapter lands (phase 3+).
/// </summary>
internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant
    // code before AppMain is called: things aren't initialized yet and stuff might break.
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
