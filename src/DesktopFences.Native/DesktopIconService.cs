using System.Text;
using DesktopFences.Core.Models;

namespace DesktopFences.Native;

/// <summary>
/// Localiza a SysListView32 do desktop e lê/escreve ícones reais.
/// </summary>
public sealed class DesktopIconService
{
    private const int MaxNameChars = 260;

    public IntPtr ListViewHandle { get; private set; }

    public bool IsConnected => ListViewHandle != IntPtr.Zero;

    public DesktopSnapshot Capture()
    {
        ListViewHandle = FindDesktopListView();
        if (ListViewHandle == IntPtr.Zero)
        {
            return DesktopSnapshot.Failed(
                "Não encontrei a SysListView32 (Progman/WorkerW). Veja docs/SPEC.md §1.");
        }

        try
        {
            var icons = ReadIcons(ListViewHandle);
            return new DesktopSnapshot(true, $"0x{ListViewHandle.ToInt64():X}", icons, null);
        }
        catch (Exception ex)
        {
            return DesktopSnapshot.Failed($"ListView encontrada, mas a leitura falhou: {ex.Message}");
        }
    }

    public void SetItemPosition(int index, int x, int y)
    {
        EnsureConnected();
        int lParam = NativeMethods.MakeLParam(x, y);
        NativeMethods.SendMessage(
            ListViewHandle,
            NativeMethods.LVM_SETITEMPOSITION,
            (IntPtr)index,
            (IntPtr)lParam);
    }

    public void HideIcons(IEnumerable<DesktopIcon> icons, HiddenIconTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        EnsureConnected();

        foreach (DesktopIcon icon in icons)
        {
            tracker.Remember(icon);
            SetItemPosition(icon.Index, NativeMethods.HiddenIconX, NativeMethods.HiddenIconY);
        }
    }

    public void Restore(HiddenIconTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        if (!IsConnected)
            Capture();
        if (!IsConnected)
            return;

        foreach (var (index, x, y) in tracker.Snapshot())
            SetItemPosition(index, x, y);

        tracker.Clear();
    }

    public DesktopIcon? HitTestScreen(int screenX, int screenY)
    {
        DesktopSnapshot snap = Capture();
        if (!snap.Connected || ListViewHandle == IntPtr.Zero)
            return null;

        var origin = new NativeMethods.POINT { X = 0, Y = 0 };
        if (!NativeMethods.ClientToScreen(ListViewHandle, ref origin))
            return null;

        return DesktopFences.Core.Occupancy.IconOccupancy.HitOrNearest(
            snap.Icons,
            screenX - origin.X,
            screenY - origin.Y);
    }

    /// <summary>
    /// Ícones atualmente selecionados no desktop (Ctrl/Shift/lasso).
    /// Ler no início do arraste, não no WM_LBUTTONDOWN — o Explorer ainda não atualizou a seleção.
    /// </summary>
    public IReadOnlyList<DesktopIcon> GetSelectedIcons()
    {
        DesktopSnapshot snap = Capture();
        if (!snap.Connected || ListViewHandle == IntPtr.Zero || snap.Icons.Count == 0)
            return [];

        var selected = new List<DesktopIcon>();
        int index = -1;
        while (true)
        {
            index = (int)NativeMethods.SendMessage(
                ListViewHandle,
                NativeMethods.LVM_GETNEXTITEM,
                (IntPtr)index,
                (IntPtr)NativeMethods.LVNI_SELECTED);
            if (index < 0)
                break;

            DesktopIcon? match = snap.Icons.FirstOrDefault(i => i.Index == index);
            if (match is not null)
                selected.Add(match);
        }

        return selected;
    }

    public bool PlaceIconAtScreen(string nameOrPath, int screenX, int screenY, int? originalX, int? originalY)
    {
        DesktopSnapshot snap = Capture();
        if (!snap.Connected || ListViewHandle == IntPtr.Zero)
            return false;

        DesktopIcon? match = DesktopFences.Core.DesktopIconMatcher.Find(snap.Icons, nameOrPath);
        if (match is null)
            return false;

        var pt = new NativeMethods.POINT { X = screenX, Y = screenY };
        if (NativeMethods.ScreenToClient(ListViewHandle, ref pt))
            SetItemPosition(match.Index, pt.X, pt.Y);
        else
            SetItemPosition(match.Index, originalX ?? match.X, originalY ?? match.Y);

        return true;
    }

    public static (int X, int Y) CursorScreen()
    {
        NativeMethods.GetCursorPos(out NativeMethods.POINT pt);
        return (pt.X, pt.Y);
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            Capture();
        if (!IsConnected)
            throw new InvalidOperationException("SysListView32 não encontrada.");
    }

    public static IntPtr FindDesktopListView()
    {
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        IntPtr listView = FindListViewUnder(progman);
        if (listView != IntPtr.Zero)
            return listView;

        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            IntPtr defView = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero)
                return true;

            IntPtr lv = NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
            if (lv == IntPtr.Zero)
                return true;

            found = lv;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private static IntPtr FindListViewUnder(IntPtr parent)
    {
        if (parent == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr defView = NativeMethods.FindWindowEx(parent, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
            return IntPtr.Zero;

        return NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
    }

    private static IReadOnlyList<DesktopIcon> ReadIcons(IntPtr listView)
    {
        int count = (int)NativeMethods.SendMessage(
            listView, NativeMethods.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);

        if (count <= 0)
            return [];

        using RemoteProcessMemory? remote = RemoteProcessMemory.OpenFromWindow(listView);
        if (remote is null)
            return [];

        int lvItemSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LVITEM>();
        int pointSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.POINT>();
        int textBytes = MaxNameChars * 2;

        IntPtr remoteLvItem = remote.Alloc(lvItemSize);
        IntPtr remoteText = remote.Alloc(textBytes);
        IntPtr remotePoint = remote.Alloc(pointSize);

        var result = new List<DesktopIcon>(count);
        for (int i = 0; i < count; i++)
        {
            NativeMethods.SendMessage(
                listView, NativeMethods.LVM_GETITEMPOSITION, (IntPtr)i, remotePoint);
            NativeMethods.POINT point = remote.Read<NativeMethods.POINT>(remotePoint);

            var lvItem = new NativeMethods.LVITEM
            {
                mask = NativeMethods.LVIF_TEXT,
                iItem = i,
                iSubItem = 0,
                pszText = remoteText,
                cchTextMax = MaxNameChars
            };
            remote.Write(remoteLvItem, lvItem);
            NativeMethods.SendMessage(
                listView, NativeMethods.LVM_GETITEMTEXTW, (IntPtr)i, remoteLvItem);

            byte[] textBuffer = remote.ReadBytes(remoteText, textBytes);
            string text = Encoding.Unicode.GetString(textBuffer);
            int nullIndex = text.IndexOf('\0');
            if (nullIndex >= 0)
                text = text[..nullIndex];

            result.Add(new DesktopIcon(i, text, point.X, point.Y));
        }

        return result;
    }
}
