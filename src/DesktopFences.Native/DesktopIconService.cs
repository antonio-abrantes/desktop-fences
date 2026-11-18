using System.Text;
using DesktopFences.Core;
using DesktopFences.Core.Models;

namespace DesktopFences.Native;

/// <summary>
/// Localiza a SysListView32 do desktop e lê/escreve ícones reais.
/// </summary>
public interface IDesktopSnapshotSource
{
    DesktopSnapshot Capture();
}

public sealed class DesktopIconService : IDesktopSnapshotSource
{
    private const int MaxNameChars = 260;
    private readonly DesktopVisibility _visibility = new();

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

    public IReadOnlyList<PixelRect> WorkAreasInClient()
    {
        if (!IsConnected)
            Capture();
        if (!IsConnected)
            return [];

        return MonitorWorkArea.WorkAreasInListViewClient(ListViewHandle);
    }

    public int HiddenCount => _visibility.Count;

    public DesktopConcealResult ConcealDesktopItem(
        Guid fenceId,
        string? path,
        string? displayName,
        string? originalPath) =>
        _visibility.Conceal(fenceId, path, displayName, originalPath);

    public bool TryRevealDesktopItem(
        string? path,
        string? displayName,
        string? originalPath,
        out string? restoredPath)
    {
        restoredPath = _visibility.Reveal(path, displayName, originalPath);
        return restoredPath is not null;
    }

    public bool RevealAllHidden() => _visibility.RevealAll();

    public void ForgetHidden(string? path, string? displayName, string? originalPath) =>
        _visibility.Forget(path, displayName, originalPath);

    public void FlushShell() => _visibility.FlushShell();

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

    public bool SetItemPositionAfterReveal(string nameOrPath, int clientX, int clientY)
    {
        DesktopSnapshot snap = Capture();
        if (!snap.Connected || ListViewHandle == IntPtr.Zero)
            return false;

        DesktopIcon? match = DesktopFences.Core.DesktopIconMatcher.Find(snap.Icons, nameOrPath);
        if (match is null)
            return false;

        SetItemPosition(match.Index, clientX, clientY);
        return true;
    }

    public bool IsManualArrangement()
    {
        using DesktopShellView? view = DesktopShellView.Open();
        return view?.IsManualArrangement == true;
    }

    public (int X, int Y)? PositionOf(string path)
    {
        using DesktopShellView? view = DesktopShellView.Open();
        List<DesktopShellView.ViewItem>? items = view?.ReadItems();
        if (items is null)
            return null;
        try
        {
            DesktopShellView.ViewItem? item = FindShellItem(items, path);
            return item is null ? null : (item.Position.X, item.Position.Y);
        }
        finally { DesktopShellView.ReleaseItems(items); }
    }

    public DesktopReturnReservation ReserveReturnPositions(IReadOnlyList<DesktopPlacement> placements)
    {
        if (placements.Count == 0)
            return DesktopReturnReservation.Empty;
        using DesktopShellView? view = DesktopShellView.Open();
        if (view is null || !view.IsManualArrangement)
            return DesktopReturnReservation.Empty;

        IntPtr listView = FindDesktopListView();
        var spacing = view.Spacing;
        IReadOnlyList<PixelRect> bounds = MonitorWorkArea.WorkAreasInListViewClient(listView);
        if (spacing.X <= 0 || spacing.Y <= 0 || bounds.Count == 0)
            return DesktopReturnReservation.Empty;

        List<DesktopShellView.ViewItem>? items = view.ReadItems();
        if (items is null)
            return DesktopReturnReservation.Empty;
        try
        {
            var occupied = items.Select(item => (item.Position.X, item.Position.Y)).ToArray();
            var requests = new List<((int X, int Y) Point, string Id, bool PreferExact)>();
            foreach (DesktopPlacement placement in placements)
            {
                if (FindShellItem(items, placement.NameOrPath) is not null)
                    continue;
                if (placement.ScreenX is int x && placement.ScreenY is int y)
                {
                    var point = new NativeMethods.POINT { X = x, Y = y };
                    if (NativeMethods.ScreenToClient(listView, ref point))
                        requests.Add(((point.X, point.Y), placement.NameOrPath, false));
                    continue;
                }
                if (placement.OriginalX is int originalX && placement.OriginalY is int originalY)
                    requests.Add(((originalX, originalY), placement.NameOrPath, true));
                else
                    requests.Add(((bounds[0].X, bounds[0].Y), placement.NameOrPath, false));
            }

            var areas = bounds.Select(area =>
            {
                var anchor = occupied.Where(point => area.Contains(point.X, point.Y))
                    .Concat(requests.Select(request => request.Point).Where(point => area.Contains(point.X, point.Y)))
                    .DefaultIfEmpty((area.X, area.Y)).First();
                return (area, anchor);
            }).ToArray();
            var cells = DesktopReturnGrid.Reserve(requests, occupied, areas, spacing.X, spacing.Y);
            var protection = CaptureProtection(items, [], bounds, spacing);
            return new DesktopReturnReservation(cells, requests, occupied, protection, areas, (spacing.X, spacing.Y));
        }
        finally { DesktopShellView.ReleaseItems(items); }
    }

    public int PlaceReservedItems(DesktopReturnReservation reservation, IReadOnlyList<DesktopPlacement> placements,
        ISet<string> handled)
    {
        if (reservation.Cells.Count == 0)
            return handled.Count;
        using DesktopShellView? view = DesktopShellView.Open();
        if (view is null || !view.IsManualArrangement ||
            !reservation.Areas.Select(area => area.Bounds)
                .SequenceEqual(MonitorWorkArea.WorkAreasInListViewClient(FindDesktopListView())))
            return 0;
        var spacing = view.Spacing;
        if ((spacing.X, spacing.Y) != reservation.Spacing)
            return 0;
        List<DesktopShellView.ViewItem>? items = view.ReadItems();
        if (items is null)
            return 0;
        try
        {
            var returned = new Dictionary<string, DesktopShellView.ViewItem>(StringComparer.OrdinalIgnoreCase);
            var placementPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DesktopPlacement placement in placements)
            {
                if (!reservation.HasCell(placement.NameOrPath))
                    continue;
                placementPaths.Add(placement.NameOrPath);
                if (placement.VerifyFileIdentity &&
                    !DesktopFileIdentity.Matches(placement.NameOrPath, placement.ExpectedFileIdentity))
                    continue;
                if (FindShellItem(items, placement.NameOrPath) is { } item)
                    returned.TryAdd(placement.NameOrPath, item);
            }

            var owned = returned.Values.ToHashSet();
            var corrections = PlanProtection(reservation.Protection, items, owned, spacing);
            var corrected = corrections.ToDictionary(move => move.Item, move => move.Point);
            var externalPositions = items.Where(item => !owned.Contains(item))
                .Select(item =>
                {
                    var point = corrected.GetValueOrDefault(item, item.Position);
                    return (point.X, point.Y);
                }).ToArray();
            var requests = reservation.Requests.Where(request => placementPaths.Contains(request.Id)).ToArray();
            var targets = DesktopReturnGrid.ReserveAfterReturn(
                requests, reservation.ProtectedPositions, externalPositions, reservation.Areas, spacing.X, spacing.Y);
            var moves = corrections;
            handled.Clear();
            foreach (var (path, item) in returned)
            {
                if (!targets.TryGetValue(path, out var target))
                    continue;
                if ((item.Position.X, item.Position.Y) == target)
                    handled.Add(path);
                else
                    moves.Add((item, new NativeMethods.POINT { X = target.X, Y = target.Y }));
            }
            if (moves.Count != 0)
                ApplyPositionChanges(view, items, moves, spacing);
            else
                return ProtectionRestored(reservation.Protection, items) ? handled.Count : 0;

            // Re-read the view: Explorer may complete positioning asynchronously.
            if (view.ReadItems() is not { } after)
                return 0;
            try
            {
                handled.Clear();
                foreach (DesktopPlacement placement in placements)
                {
                    if (!targets.TryGetValue(placement.NameOrPath, out var target) ||
                        (placement.VerifyFileIdentity &&
                         !DesktopFileIdentity.Matches(placement.NameOrPath, placement.ExpectedFileIdentity)))
                        continue;
                    if (FindShellItem(after, placement.NameOrPath) is { } item &&
                        (item.Position.X, item.Position.Y) == target)
                        handled.Add(placement.NameOrPath);
                }
                return ProtectionRestored(reservation.Protection, after) ? handled.Count : 0;
            }
            finally { DesktopShellView.ReleaseItems(after); }
        }
        finally { DesktopShellView.ReleaseItems(items); }
    }

    public DesktopLayoutProtection? CaptureLayoutProtection(IEnumerable<string> affected)
    {
        using DesktopShellView? view = DesktopShellView.Open();
        if (view is null || !view.IsManualArrangement)
            return null;
        var spacing = view.Spacing;
        var areas = MonitorWorkArea.WorkAreasInListViewClient(FindDesktopListView());
        if (spacing.X <= 0 || spacing.Y <= 0 || areas.Count == 0 || view.ReadItems() is not { } items)
            return null;
        try { return CaptureProtection(items, affected, areas, spacing); }
        finally { DesktopShellView.ReleaseItems(items); }
    }

    public void PreserveDesktopLayout(DesktopLayoutProtection? protection, bool waitForRemoval = false)
    {
        if (protection is null)
            return;

        int consecutive = 0;
        for (int pass = 0; pass < 20; pass++)
        {
            using DesktopShellView? view = DesktopShellView.Open();
            if (view is null || !view.IsManualArrangement ||
                !protection.Areas.SequenceEqual(MonitorWorkArea.WorkAreasInListViewClient(FindDesktopListView())))
                return;
            var spacing = view.Spacing;
            if ((spacing.X, spacing.Y) != protection.Spacing)
                return;
            var items = view.ReadItems();
            if (items is not null)
            {
                try
                {
                    var moves = PlanProtection(protection, items, [], spacing);
                    if (moves.Count != 0)
                        ApplyPositionChanges(view, items, moves, spacing);
                    bool removalSettled = !waitForRemoval ||
                        !items.Any(item => item.Path is { } path && protection.Affected.Contains(path));
                    consecutive = removalSettled && moves.Count == 0 && ProtectionRestored(protection, items)
                        ? consecutive + 1 : 0;
                    if (consecutive >= 3)
                        return;
                }
                finally { DesktopShellView.ReleaseItems(items); }
            }
            if (pass < 19)
                Thread.Sleep(120);
        }
    }

    private static DesktopLayoutProtection CaptureProtection(
        IReadOnlyList<DesktopShellView.ViewItem> items, IEnumerable<string> affected,
        IReadOnlyList<PixelRect> areas, NativeMethods.POINT spacing)
    {
        var excluded = new HashSet<string>(affected.Select(NormalizeShellPath), StringComparer.OrdinalIgnoreCase);
        var positions = UniqueItems(items).Where(pair => !excluded.Contains(pair.Key))
            .Select(pair => (pair.Key, pair.Value.Position, Identity: IdentifyProtectedItem(pair.Key)))
            .Where(item => item.Identity is not null)
            .ToDictionary(item => item.Key, item => ((item.Position.X, item.Position.Y), item.Identity!),
                StringComparer.OrdinalIgnoreCase);
        return new DesktopLayoutProtection(positions, excluded, areas, (spacing.X, spacing.Y));
    }

    private static List<(DesktopShellView.ViewItem Item, NativeMethods.POINT Point)> PlanProtection(
        DesktopLayoutProtection protection, List<DesktopShellView.ViewItem> items,
        IReadOnlyCollection<DesktopShellView.ViewItem> returned, NativeMethods.POINT spacing)
    {
        var verified = VerifiedProtectedItems(protection, items);
        var occupants = items.Except(verified.Values).Except(returned)
            .Select(item => (item.Position.X, item.Position.Y)).ToArray();
        var saved = protection.Items.ToDictionary(pair => pair.Key, pair => pair.Value.Position,
            StringComparer.OrdinalIgnoreCase);
        var current = verified.ToDictionary(pair => pair.Key, pair => (pair.Value.Position.X, pair.Value.Position.Y),
            StringComparer.OrdinalIgnoreCase);
        return DesktopReturnGrid.PreserveExisting(saved, current, occupants, spacing.X, spacing.Y)
            .Select(pair => (verified[pair.Key], new NativeMethods.POINT { X = pair.Value.X, Y = pair.Value.Y }))
            .ToList();
    }

    private static bool ProtectionRestored(DesktopLayoutProtection protection, List<DesktopShellView.ViewItem> items) =>
        VerifiedProtectedItems(protection, items).All(pair =>
            (pair.Value.Position.X, pair.Value.Position.Y) == protection.Items[pair.Key].Position);

    private static Dictionary<string, DesktopShellView.ViewItem> VerifiedProtectedItems(
        DesktopLayoutProtection protection, IEnumerable<DesktopShellView.ViewItem> items) =>
        UniqueItems(items).Where(pair => protection.Items.TryGetValue(pair.Key, out var original) &&
            ((pair.Value.Position.X, pair.Value.Position.Y) == original.Position ||
             string.Equals(original.Identity, IdentifyProtectedItem(pair.Key), StringComparison.Ordinal)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, DesktopShellView.ViewItem> UniqueItems(IEnumerable<DesktopShellView.ViewItem> items) =>
        items.Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .GroupBy(item => NormalizeShellPath(item.Path!), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

    private static string NormalizeShellPath(string path) =>
        DesktopFences.Core.Occupancy.DesktopHide.IsCanonicalNamespaceKey(path)
            ? DesktopFences.Core.Occupancy.DesktopHide.ToShellParsingName(path) : path;

    private static string? IdentifyProtectedItem(string path) =>
        System.IO.Path.IsPathFullyQualified(path) ? DesktopFileIdentity.Read(path)
            : DesktopCustodyBatch.GetDropNamespaceKey(path);

    private static void ApplyPositionChanges(DesktopShellView view, List<DesktopShellView.ViewItem> items,
        List<(DesktopShellView.ViewItem Item, NativeMethods.POINT Point)> moves, NativeMethods.POINT spacing)
    {
        // Keep swaps in one operation. Never target the cell of a stationary item.
        int removed;
        do
        {
            var moving = moves.Select(move => move.Item).ToHashSet();
            removed = moves.RemoveAll(move => items.Any(other =>
                !moving.Contains(other) && Overlaps(move.Point, other.Position, spacing)));
        } while (removed > 0);
        if (moves.Count != 0)
            view.Place(moves);
    }

    private static bool Overlaps(NativeMethods.POINT first, NativeMethods.POINT second, NativeMethods.POINT spacing) =>
        Math.Abs(first.X - second.X) < spacing.X && Math.Abs(first.Y - second.Y) < spacing.Y;

    public int PlaceRevealedItems(IReadOnlyList<DesktopPlacement> placements, ISet<string>? handled = null)
    {
        if (placements.Count == 0)
            return 0;
        using DesktopShellView? view = DesktopShellView.Open();
        if (view is null || !view.IsManualArrangement)
            return 0;

        IntPtr listView = FindDesktopListView();
        IReadOnlyList<PixelRect> workAreas = MonitorWorkArea.WorkAreasInListViewClient(listView);
        NativeMethods.POINT spacing = view.Spacing;
        if (workAreas.Count == 0 || spacing.X <= 0 || spacing.Y <= 0)
            return 0;
        List<DesktopShellView.ViewItem>? items = view.ReadItems();
        if (items is null)
            return 0;

        handled ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (DesktopPlacement placement in placements)
            {
                if (handled.Contains(placement.NameOrPath))
                    continue;
                if (placement.VerifyFileIdentity
                    && !DesktopFileIdentity.Matches(placement.NameOrPath, placement.ExpectedFileIdentity))
                    continue;
                DesktopShellView.ViewItem? item = FindShellItem(items, placement.NameOrPath);
                if (item is null)
                    continue;

                NativeMethods.POINT desired = new()
                {
                    X = placement.OriginalX ?? item.Position.X,
                    Y = placement.OriginalY ?? item.Position.Y
                };
                if (placement.ScreenX is int x && placement.ScreenY is int y)
                {
                    desired = new NativeMethods.POINT { X = x, Y = y };
                    if (!NativeMethods.ScreenToClient(listView, ref desired))
                        continue;
                }
                NativeMethods.POINT? target = FindFreeCell(desired, item, items, workAreas, spacing);
                if (target is not { } point)
                    continue;
                if (point.X == item.Position.X && point.Y == item.Position.Y)
                {
                    handled.Add(placement.NameOrPath);
                    continue;
                }
                if (view.Place(item, point, out NativeMethods.POINT actual))
                {
                    item.Position = actual;
                    handled.Add(placement.NameOrPath);
                }
            }
            return handled.Count;
        }
        finally { DesktopShellView.ReleaseItems(items); }
    }

    private static DesktopShellView.ViewItem? FindShellItem(IEnumerable<DesktopShellView.ViewItem> items, string identity)
    {
        string? key = DesktopCustodyBatch.GetDropNamespaceKey(identity);
        if (key is null && DesktopFences.Core.Occupancy.DesktopHide.IsCanonicalNamespaceKey(identity))
            identity = DesktopFences.Core.Occupancy.DesktopHide.ToShellParsingName(identity);
        else if (key is null && !System.IO.Path.IsPathFullyQualified(identity))
            return null;

        DesktopShellView.ViewItem[] matches = items
            .Where(item => string.Equals(item.Path, identity, StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static NativeMethods.POINT? FindFreeCell(
        NativeMethods.POINT desired, DesktopShellView.ViewItem target, List<DesktopShellView.ViewItem> items,
        IReadOnlyList<PixelRect> workAreas, NativeMethods.POINT spacing)
    {
        // Keep candidates on the Explorer grid established by the materialized icon.
        int gridX = target.Position.X + (int)Math.Round((double)(desired.X - target.Position.X) / spacing.X) * spacing.X;
        int gridY = target.Position.Y + (int)Math.Round((double)(desired.Y - target.Position.Y) / spacing.Y) * spacing.Y;
        for (int radius = 0; radius <= 3; radius++)
        {
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                    continue;
                int candidateX = gridX + x * spacing.X;
                int candidateY = gridY + y * spacing.Y;
                if (!workAreas.Any(area => area.Contains(candidateX, candidateY) &&
                    area.Contains(candidateX + spacing.X - 1, candidateY + spacing.Y - 1)))
                    continue;
                var cell = new PixelRect(candidateX, candidateY, spacing.X, spacing.Y);
                if (!items.Any(item => !ReferenceEquals(item, target) &&
                    cell.Intersects(item.Position.X, item.Position.Y, spacing.X, spacing.Y)))
                    return new NativeMethods.POINT { X = candidateX, Y = candidateY };
            }
        }
        return null;
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

public sealed record DesktopPlacement(
    string NameOrPath,
    int? OriginalX,
    int? OriginalY,
    int? ScreenX,
    int? ScreenY,
    string? ExpectedFileIdentity = null,
    bool VerifyFileIdentity = false);

public sealed class DesktopReturnReservation
{
    internal static DesktopReturnReservation Empty => new(
        new Dictionary<string, (int X, int Y)>(), [], [], DesktopLayoutProtection.Empty, [], default);

    internal DesktopReturnReservation(
        IReadOnlyDictionary<string, (int X, int Y)> cells,
        IReadOnlyList<((int X, int Y) Point, string Id, bool PreferExact)> requests,
        IReadOnlyList<(int X, int Y)> protectedPositions,
        DesktopLayoutProtection protection,
        IReadOnlyList<(PixelRect Bounds, (int X, int Y) Origin)> areas,
        (int X, int Y) spacing)
    {
        Cells = cells;
        Requests = requests;
        ProtectedPositions = protectedPositions;
        Protection = protection;
        Areas = areas;
        Spacing = spacing;
    }

    internal IReadOnlyDictionary<string, (int X, int Y)> Cells { get; }
    internal IReadOnlyList<((int X, int Y) Point, string Id, bool PreferExact)> Requests { get; }
    internal IReadOnlyList<(int X, int Y)> ProtectedPositions { get; }
    internal DesktopLayoutProtection Protection { get; }
    internal IReadOnlyList<(PixelRect Bounds, (int X, int Y) Origin)> Areas { get; }
    internal (int X, int Y) Spacing { get; }

    public bool HasCell(string path) => Cells.ContainsKey(path);
}

public sealed class DesktopLayoutProtection
{
    internal static DesktopLayoutProtection Empty { get; } = new(
        new Dictionary<string, ((int X, int Y) Position, string Identity)>(),
        new HashSet<string>(), [], default);

    internal DesktopLayoutProtection(
        IReadOnlyDictionary<string, ((int X, int Y) Position, string Identity)> items,
        IReadOnlySet<string> affected, IReadOnlyList<PixelRect> areas, (int X, int Y) spacing)
    {
        Items = items;
        Affected = affected;
        Areas = areas;
        Spacing = spacing;
    }

    internal IReadOnlyDictionary<string, ((int X, int Y) Position, string Identity)> Items { get; }
    internal IReadOnlySet<string> Affected { get; }
    internal IReadOnlyList<PixelRect> Areas { get; }
    internal (int X, int Y) Spacing { get; }
}
