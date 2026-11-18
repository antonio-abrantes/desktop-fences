using System.Runtime.InteropServices;

namespace DesktopFences.Native;

internal sealed class DesktopShellView : IDisposable
{
    private readonly IFolderView _view;

    private DesktopShellView(IFolderView view) => _view = view;

    public static DesktopShellView? Open()
    {
        object? windows = null;
        object? dispatch = null;
        object? browser = null;
        object? view = null;
        try
        {
            windows = new ShellWindows();
            object location = 0;
            object? root = null;
            if (((IShellWindows)windows).FindWindowSW(ref location, ref root, 8, out _, 1, out dispatch) != 0)
                return null;
            if (dispatch is not IShellServiceProvider services)
                return null;
            Guid service = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
            Guid browserId = typeof(IShellBrowser).GUID;
            if (services.QueryService(ref service, ref browserId, out browser) != 0 ||
                browser is not IShellBrowser shell || shell.QueryActiveShellView(out view) != 0 ||
                view is not IFolderView folderView)
                return null;
            view = null;
            return new DesktopShellView(folderView);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            Release(view);
            Release(browser);
            Release(dispatch);
            Release(windows);
        }
    }

    public bool IsManualArrangement => _view.GetAutoArrange() == 1;

    public NativeMethods.POINT Spacing =>
        _view.GetSpacing(out NativeMethods.POINT point) == 0 ? point : default;

    public List<ViewItem>? ReadItems()
    {
        const uint allViewItems = 2;
        if (_view.ItemCount(allViewItems, out int count) != 0)
            return null;
        var items = new List<ViewItem>(count);
        for (int index = 0; index < count; index++)
        {
            if (_view.Item(index, out IntPtr pidl) != 0 || pidl == IntPtr.Zero)
                break;
            if (_view.GetItemPosition(pidl, out NativeMethods.POINT point) != 0)
            {
                Marshal.FreeCoTaskMem(pidl);
                break;
            }
            string? path = ShellDesktopNamespace.GetShellName(pidl, 0x80058000)
                ?? ShellDesktopNamespace.GetShellName(pidl, 0x80028000);
            items.Add(new ViewItem(pidl, path, point));
        }
        if (items.Count == count)
            return items;
        ReleaseItems(items);
        return null;
    }

    public bool Place(ViewItem item, NativeMethods.POINT target, out NativeMethods.POINT position)
    {
        const uint positionItem = 0x80;
        position = default;
        return _view.SelectAndPositionItems(1, [item.Pidl], [target], positionItem) == 0
            && _view.GetItemPosition(item.Pidl, out position) == 0;
    }

    public bool Place(IReadOnlyList<(ViewItem Item, NativeMethods.POINT Point)> moves)
    {
        const uint positionItem = 0x80;
        return _view.SelectAndPositionItems((uint)moves.Count, moves.Select(move => move.Item.Pidl).ToArray(),
            moves.Select(move => move.Point).ToArray(), positionItem) == 0;
    }

    public static void ReleaseItems(IEnumerable<ViewItem> items)
    {
        foreach (ViewItem item in items)
            Marshal.FreeCoTaskMem(item.Pidl);
    }

    public void Dispose() => Release(_view);

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.ReleaseComObject(value);
    }

    internal sealed class ViewItem(IntPtr pidl, string? path, NativeMethods.POINT position)
    {
        public IntPtr Pidl { get; } = pidl;
        public string? Path { get; } = path;
        public NativeMethods.POINT Position { get; set; } = position;
    }

    [ComImport]
    [Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")]
    private class ShellWindows { }

    [ComImport]
    [Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IShellWindows
    {
        void GetCount();
        void Item();
        void NewEnum();
        void Register();
        void RegisterPending();
        void Revoke();
        void OnNavigate();
        void OnActivated();
        [PreserveSig] int FindWindowSW(
            [In, MarshalAs(UnmanagedType.Struct)] ref object location,
            [In, MarshalAs(UnmanagedType.Struct)] ref object? root,
            int windowClass, out int hwnd, int options,
            [MarshalAs(UnmanagedType.IDispatch)] out object dispatch);
    }

    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value);
    }

    [ComImport]
    [Guid("000214E2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void GetWindow();
        void ContextSensitiveHelp();
        void InsertMenusSB();
        void SetMenuSB();
        void RemoveMenusSB();
        void SetStatusTextSB();
        void EnableModelessSB();
        void TranslateAcceleratorSB();
        void BrowseObject();
        void GetViewStateStream();
        void GetControlWindow();
        void SendControlMsg();
        [PreserveSig] int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object view);
    }

    [ComImport]
    [Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFolderView
    {
        void GetCurrentViewMode();
        void SetCurrentViewMode();
        void GetFolder();
        [PreserveSig] int Item(int index, out IntPtr pidl);
        [PreserveSig] int ItemCount(uint flags, out int count);
        void Items();
        void GetSelectionMarkedItem();
        void GetFocusedItem();
        [PreserveSig] int GetItemPosition(IntPtr pidl, out NativeMethods.POINT point);
        [PreserveSig] int GetSpacing(out NativeMethods.POINT point);
        void GetDefaultSpacing();
        [PreserveSig] int GetAutoArrange();
        void SelectItem();
        [PreserveSig] int SelectAndPositionItems(uint count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] pidls,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] NativeMethods.POINT[] points, uint flags);
    }
}
