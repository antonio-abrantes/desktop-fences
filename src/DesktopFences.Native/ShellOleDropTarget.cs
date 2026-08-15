using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using IComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace DesktopFences.Native;

/// <summary>
/// IDropTarget nativo no HWND da fence + IDropTargetHelper.
/// O WPF (AllowsTransparency / UpdateLayeredWindow) registra um alvo OLE que
/// devolve NONE e não encaminha a imagem do Explorer — daí o cursor de
/// proibido e o ícone sumindo. O helper é o contrato da Shell para manter
/// o thumbnail visível sobre a janela de destino.
/// </summary>
public sealed class ShellOleDropTarget : IDisposable
{
    public event Action<IReadOnlyList<string>>? FilesDropped;
    public event Action? DragEntered;
    public event Action? DragLeft;

    private readonly DropTarget _com;
    private readonly List<IntPtr> _hwnds = [];
    private IntPtr _helperHwnd;

    public ShellOleDropTarget()
    {
        _com = new DropTarget(this);
    }

    public void Attach(IntPtr hwnd)
    {
        _ = OleInitialize(IntPtr.Zero);
        _helperHwnd = hwnd;
        Register(hwnd);
    }

    public void Register(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        _ = RevokeDragDrop(hwnd);
        int hr = RegisterDragDrop(hwnd, _com);
        if (hr is 0 or 1 && !_hwnds.Contains(hwnd))
            _hwnds.Add(hwnd);
    }

    public void Detach()
    {
        foreach (IntPtr hwnd in _hwnds)
            _ = RevokeDragDrop(hwnd);
        _hwnds.Clear();
        _helperHwnd = IntPtr.Zero;
    }

    public void Dispose() => Detach();

    private void OnEnter() => DragEntered?.Invoke();

    private void OnLeave() => DragLeft?.Invoke();

    private void OnDrop(IReadOnlyList<string> paths) => FilesDropped?.Invoke(paths);

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class DropTarget : IDropTarget
    {
        private const int Ok = 0;
        private const int DropEffectCopy = 1;
        private const int DropEffectMove = 2;
        private const int DropEffectLink = 4;
        private const short CfHdrop = 15;

        private readonly ShellOleDropTarget _owner;
        private readonly IDropTargetHelper? _helper;

        public DropTarget(ShellOleDropTarget owner)
        {
            _owner = owner;
            try
            {
                _helper = (IDropTargetHelper)new DragDropHelper();
            }
            catch
            {
                _helper = null;
            }
        }

        public int DragEnter(IComDataObject pDataObj, int grfKeyState, PointL pt, ref int pdwEffect)
        {
            pdwEffect = ChooseEffect(pdwEffect);
            NotifyHelperEnter(pDataObj, pt, pdwEffect);
            _owner.OnEnter();
            return Ok;
        }

        public int DragOver(int grfKeyState, PointL pt, ref int pdwEffect)
        {
            pdwEffect = ChooseEffect(pdwEffect);
            if (_helper is not null)
            {
                PointL point = pt;
                try
                {
                    _helper.DragOver(ref point, pdwEffect);
                    _helper.Show(false);
                }
                catch { }
            }

            return Ok;
        }

        public int DragLeave()
        {
            try { _helper?.DragLeave(); }
            catch { }
            _owner.OnLeave();
            return Ok;
        }

        public int Drop(IComDataObject pDataObj, int grfKeyState, PointL pt, ref int pdwEffect)
        {
            pdwEffect = ChooseEffect(pdwEffect);
            if (_helper is not null)
            {
                PointL point = pt;
                try { _helper.Drop(pDataObj, ref point, pdwEffect); }
                catch { }
            }

            IReadOnlyList<string> paths = ExtractPaths(pDataObj);
            _owner.OnDrop(paths);
            _owner.OnLeave();
            return Ok;
        }

        private void NotifyHelperEnter(IComDataObject data, PointL pt, int effect)
        {
            if (_helper is null)
                return;

            IntPtr hwnd = _owner._helperHwnd;
            PointL point = pt;
            try
            {
                _helper.DragEnter(hwnd, data, ref point, effect);
                _helper.Show(false);
            }
            catch { }
        }

        private static int ChooseEffect(int allowed)
        {
            const int wanted = DropEffectCopy | DropEffectMove | DropEffectLink;
            int available = allowed & wanted;
            if ((available & DropEffectCopy) != 0)
                return DropEffectCopy;
            if ((available & DropEffectLink) != 0)
                return DropEffectLink;
            if ((available & DropEffectMove) != 0)
                return DropEffectMove;
            return DropEffectCopy;
        }

        private static IReadOnlyList<string> ExtractPaths(IComDataObject data)
        {
            var paths = new List<string>();
            TryHdrop(data, paths);
            if (paths.Count == 0)
                TryFormat(data, "FileNameW", paths);
            if (paths.Count == 0)
                TryFormat(data, "FileName", paths);
            return paths;
        }

        private static void TryHdrop(IComDataObject data, List<string> paths)
        {
            FORMATETC fmt = new()
            {
                cfFormat = CfHdrop,
                dwAspect = DVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = TYMED.TYMED_HGLOBAL
            };

            STGMEDIUM medium = new();
            try
            {
                if (data.QueryGetData(ref fmt) != 0)
                    return;
                data.GetData(ref fmt, out medium);
                IntPtr hdrop = medium.unionmember;
                if (hdrop == IntPtr.Zero)
                    return;

                uint count = DragQueryFile(hdrop, 0xFFFFFFFF, null, 0);
                var buffer = new StringBuilder(520);
                for (uint i = 0; i < count; i++)
                {
                    buffer.Clear();
                    uint len = DragQueryFile(hdrop, i, buffer, (uint)buffer.Capacity);
                    if (len > 0)
                        paths.Add(buffer.ToString());
                }
            }
            catch
            {
                // payload atrasado / formato incompatível
            }
            finally
            {
                if (medium.unionmember != IntPtr.Zero || medium.pUnkForRelease is not null)
                    ReleaseStgMedium(ref medium);
            }
        }

        private static void TryFormat(IComDataObject data, string formatName, List<string> paths)
        {
            short format = (short)RegisterClipboardFormat(formatName);
            if (format == 0)
                return;

            FORMATETC fmt = new()
            {
                cfFormat = format,
                dwAspect = DVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = TYMED.TYMED_HGLOBAL
            };

            STGMEDIUM medium = new();
            try
            {
                if (data.QueryGetData(ref fmt) != 0)
                    return;
                data.GetData(ref fmt, out medium);
                IntPtr locked = GlobalLock(medium.unionmember);
                if (locked == IntPtr.Zero)
                    return;
                try
                {
                    string value = Marshal.PtrToStringUni(locked) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        paths.Add(value.Trim().Trim('"'));
                }
                finally
                {
                    GlobalUnlock(medium.unionmember);
                }
            }
            catch { }
            finally
            {
                if (medium.unionmember != IntPtr.Zero || medium.pUnkForRelease is not null)
                    ReleaseStgMedium(ref medium);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointL
    {
        public int X;
        public int Y;
    }

    [ComImport]
    [Guid("00000122-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDropTarget
    {
        [PreserveSig]
        int DragEnter(IComDataObject pDataObj, int grfKeyState, PointL pt, ref int pdwEffect);

        [PreserveSig]
        int DragOver(int grfKeyState, PointL pt, ref int pdwEffect);

        [PreserveSig]
        int DragLeave();

        [PreserveSig]
        int Drop(IComDataObject pDataObj, int grfKeyState, PointL pt, ref int pdwEffect);
    }

    [ComImport]
    [Guid("4657278B-411B-11D2-839A-00C04FD918D0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDropTargetHelper
    {
        void DragEnter(IntPtr hwndTarget, IComDataObject dataObject, ref PointL pt, int effect);
        void DragLeave();
        void DragOver(ref PointL pt, int effect);
        void Drop(IComDataObject dataObject, ref PointL pt, int effect);
        void Show([MarshalAs(UnmanagedType.Bool)] bool fShow);
    }

    [ComImport]
    [Guid("4657278A-411B-11D2-839A-00C04FD918D0")]
    private class DragDropHelper
    {
    }

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern int RegisterDragDrop(IntPtr hwnd, IDropTarget pDropTarget);

    [DllImport("ole32.dll")]
    private static extern int RevokeDragDrop(IntPtr hwnd);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
