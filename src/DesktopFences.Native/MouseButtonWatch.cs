using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// WH_MOUSE_LL: o trabalho pesado NÃO roda no callback (o Windows derruba o hook).
/// Só enfileira coordenadas no SynchronizationContext da UI.
/// </summary>
public sealed class MouseButtonWatch : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int HcAction = 0;

    private readonly LowLevelMouseProc _proc;
    private readonly SynchronizationContext? _ui;
    private readonly IntPtr _hook;
    private bool _disposed;
    private bool _leftDown;

    public event Action<int, int>? LeftDown;
    public event Action<int, int>? LeftMove;
    public event Action<int, int>? LeftUp;

    public MouseButtonWatch()
    {
        _ui = SynchronizationContext.Current;
        _proc = Hook;
        IntPtr module = GetModuleHandle(null);
        _hook = SetWindowsHookEx(WhMouseLl, _proc, module, 0);
    }

    public bool IsActive => _hook != IntPtr.Zero;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_hook != IntPtr.Zero)
            UnhookWindowsHookEx(_hook);
        GC.KeepAlive(_proc);
    }

    private IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HcAction && lParam != IntPtr.Zero)
        {
            int msg = wParam.ToInt32();
            if (msg == WmLButtonDown)
                _leftDown = true;
            else if (msg == WmLButtonUp)
                _leftDown = false;

            if (msg is WmLButtonDown or WmLButtonUp || (msg == WmMouseMove && _leftDown))
            {
                var info = Marshal.PtrToStructure<MsllHookStruct>(lParam);
                int x = info.Pt.X;
                int y = info.Pt.Y;
                Action<int, int>? handler = msg switch
                {
                    WmLButtonDown => LeftDown,
                    WmLButtonUp => LeftUp,
                    _ => LeftMove
                };
                if (handler is not null)
                    Post(handler, x, y);
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void Post(Action<int, int> handler, int x, int y)
    {
        if (_ui is null)
            return;
        _ui.Post(_ => handler(x, y), null);
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public NativeMethods.POINT Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
