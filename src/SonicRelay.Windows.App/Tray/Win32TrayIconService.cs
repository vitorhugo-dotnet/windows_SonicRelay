using System.Runtime.InteropServices;
using SonicRelay.Windows.Presentation;

namespace SonicRelay.Windows.App.Tray;

/// <summary>
/// Win32 <c>Shell_NotifyIcon</c> tray implementation. It creates a message-only
/// window whose <see cref="WndProc"/> receives the tray callback on the UI thread's
/// existing message pump, and shows the context menu with <c>TrackPopupMenu</c>.
/// All P/Invoke lives here so the streaming layer never depends on shell interop.
///
/// Must be constructed and used on the UI thread. Right-click opens the menu,
/// double-click raises <see cref="Activated"/>, and picking an entry raises
/// <see cref="CommandInvoked"/> with the mapped <see cref="TrayCommand"/>.
/// </summary>
public sealed class Win32TrayIconService : ITrayIconService
{
    private const uint CallbackMessage = WM_APP + 1;
    private const uint TrayIconId = 1;

    private readonly WndProcDelegate wndProc; // kept alive for the window's lifetime
    private readonly string className = "SonicRelayTrayWindow_" + Guid.NewGuid().ToString("N");
    private readonly string iconPath;
    private readonly nint hInstance;
    private nint hwnd;
    private nint hIcon;
    private ushort classAtom;
    private bool shown;
    private bool disposed;
    private IReadOnlyList<TrayMenuItem> menu = [];

    public event Action<TrayCommand>? CommandInvoked;
    public event Action? Activated;

    public Win32TrayIconService(string tooltip)
    {
        iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        hInstance = GetModuleHandle(null);
        wndProc = WndProc;
        CreateMessageWindow();
        LoadIcon();
        currentTooltip = tooltip;
    }

    private string currentTooltip;

    public void Show()
    {
        if (disposed || hwnd == 0) return;
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
        Shell_NotifyIcon(shown ? NIM_MODIFY : NIM_ADD, ref data);
        shown = true;
    }

    public void Hide()
    {
        if (!shown || hwnd == 0) return;
        var data = new NOTIFYICONDATA { cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = hwnd, uID = TrayIconId };
        Shell_NotifyIcon(NIM_DELETE, ref data);
        shown = false;
    }

    public void UpdateTooltip(string tooltip)
    {
        currentTooltip = tooltip ?? string.Empty;
        if (shown) Show();
    }

    public void UpdateMenu(IReadOnlyList<TrayMenuItem> items) => menu = items ?? [];

    public void ShowBalloon(string title, string message)
    {
        if (!shown || hwnd == 0) return;
        var data = CreateData(NIF_INFO);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(message, 255);
        data.dwInfoFlags = NIIF_NONE;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATA CreateData(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = hwnd,
        uID = TrayIconId,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = hIcon,
        szTip = Truncate(currentTooltip, 127),
    };

    private void CreateMessageWindow()
    {
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = hInstance,
            lpszClassName = className,
        };
        classAtom = RegisterClassEx(ref wc);
        // HWND_MESSAGE (-3) makes a message-only window: no UI, just a WndProc target.
        hwnd = CreateWindowEx(0, className, className, 0, 0, 0, 0, 0, HWND_MESSAGE, 0, hInstance, 0);
    }

    private void LoadIcon()
    {
        if (File.Exists(iconPath))
            hIcon = LoadImage(0, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        if (hIcon == 0)
            hIcon = LoadIcon(0, IDI_APPLICATION); // fall back to the generic app icon (MAKEINTRESOURCE)
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == CallbackMessage)
        {
            var mouse = (uint)(lParam & 0xFFFF);
            switch (mouse)
            {
                case WM_LBUTTONDBLCLK:
                    Activated?.Invoke();
                    break;
                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    ShowContextMenu();
                    break;
            }
            return 0;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var items = menu;
        if (items.Count == 0) return;

        var hMenu = CreatePopupMenu();
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var flags = MF_STRING | (item.Enabled ? 0u : MF_GRAYED | MF_DISABLED);
                // Command ids are 1-based indices into the current menu snapshot.
                AppendMenu(hMenu, flags, (nuint)(i + 1), item.Label);
            }

            GetCursorPos(out var point);
            // Documented TrackPopupMenu requirements: foreground the owner window,
            // and post a WM_NULL afterwards so the menu dismisses correctly.
            SetForegroundWindow(hwnd);
            var selected = TrackPopupMenu(
                hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, point.X, point.Y, 0, hwnd, 0);
            PostMessage(hwnd, WM_NULL, 0, 0);

            if (selected > 0 && selected <= items.Count)
                CommandInvoked?.Invoke(items[(int)selected - 1].Command);
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    private static string Truncate(string? value, int max)
    {
        value ??= string.Empty;
        return value.Length <= max ? value : value[..max];
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Hide();
        if (hwnd != 0) { DestroyWindow(hwnd); hwnd = 0; }
        if (classAtom != 0) { UnregisterClass(className, hInstance); classAtom = 0; }
    }

    #region Win32 interop

    private const uint WM_APP = 0x8000;
    private const uint WM_NULL = 0x0000;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;

    private const uint NIM_ADD = 0x0;
    private const uint NIM_MODIFY = 0x1;
    private const uint NIM_DELETE = 0x2;
    private const uint NIF_MESSAGE = 0x1;
    private const uint NIF_ICON = 0x2;
    private const uint NIF_TIP = 0x4;
    private const uint NIF_INFO = 0x10;
    private const uint NIIF_NONE = 0x0;

    private const uint MF_STRING = 0x0;
    private const uint MF_GRAYED = 0x1;
    private const uint MF_DISABLED = 0x2;
    private const uint TPM_RIGHTBUTTON = 0x2;
    private const uint TPM_RETURNCMD = 0x100;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x10;
    private const uint LR_DEFAULTSIZE = 0x40;
    private static readonly nint IDI_APPLICATION = 32512;

    private static readonly nint HWND_MESSAGE = -3;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string className, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(
        uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint hInstance, nint param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint hMenu, uint flags, nuint idNewItem, string newItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(nint hMenu, uint flags, int x, int y, int reserved, nint hWnd, nint rect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint hInstance, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll", EntryPoint = "LoadIconW")]
    private static extern nint LoadIcon(nint hInstance, nint name);

    #endregion
}
