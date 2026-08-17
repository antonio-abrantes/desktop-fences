using DesktopFences.Core;
using DesktopFences.Core.Occupancy;
using Microsoft.Win32;

namespace DesktopFences.Native;

/// <summary>
/// Tira ícones do desktop sem coordenadas: move ficheiros para
/// %LocalAppData%\DesktopFences\Items\{fenceId}; CLSID via HideDesktopIcons.
/// </summary>
public sealed class DesktopVisibility
{
    private const string NewStartPanel =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
    private const string ClassicStartMenu =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu";

    private readonly List<AppliedItem> _applied = [];
    private bool _dirty;
    private bool _needsAssocNotify;

    public int Count => _applied.Count;

    public DesktopConcealResult Conceal(
        Guid fenceId,
        string? path,
        string? displayName,
        string? originalPath)
    {
        if (FindItem(path, displayName, originalPath) is not null)
            return new DesktopConcealResult(true, null, originalPath);

        DesktopHidePlan plan = PlanFor(path, displayName, originalPath);
        if (plan.Kind == DesktopHideKind.None)
            return default;

        if (plan.Kind == DesktopHideKind.NamespaceIcon)
        {
            if (!ApplyNamespaceHidden(plan.Key))
                return default;
            _applied.Add(new AppliedItem(plan.Kind, plan.Key, null, null, plan.Key));
            _dirty = true;
            _needsAssocNotify = true;
            return new DesktopConcealResult(true, null, null);
        }

        string source = plan.Key;
        if (!Exists(source))
            return default;

        string folder = FenceItemStore.LegacyFolderForFence(fenceId);
        string fileName = Path.GetFileName(source);
        if (string.IsNullOrEmpty(fileName))
            fileName = displayName ?? "item";

        string dest = Path.Combine(folder, fileName);
        if (!SamePath(source, dest))
        {
            dest = FenceItemStore.UniqueDestination(folder, fileName);
            if (!ShellFileMove.Move(source, folder, Path.GetFileName(dest)))
                return default;
        }

        string restoreFrom = originalPath ?? source;
        if (FenceItemStore.IsUnderRoot(restoreFrom))
            restoreFrom = originalPath ?? source;

        string rememberedOriginal = RememberedOriginal(originalPath, source);
        _applied.Add(new AppliedItem(
            DesktopHideKind.MoveToStore, dest, dest, rememberedOriginal, fileName));
        _dirty = true;
        return new DesktopConcealResult(true, dest, rememberedOriginal);
    }

    public string? Reveal(string? path, string? displayName, string? originalPath)
    {
        AppliedItem? item = FindItem(path, displayName, originalPath);
        if (item is null)
            return null;

        if (!TryRestore(item, out string? restored))
            return null;

        _applied.Remove(item);
        _dirty = true;
        if (item.Kind == DesktopHideKind.NamespaceIcon)
            _needsAssocNotify = true;
        return restored ?? "";
    }

    public bool RevealAll()
    {
        if (_applied.Count == 0)
            return true;

        bool all = true;
        bool assoc = false;
        foreach (AppliedItem item in _applied.ToList())
        {
            if (!TryRestore(item, out _))
            {
                all = false;
                continue;
            }

            _applied.Remove(item);
            _dirty = true;
            if (item.Kind == DesktopHideKind.NamespaceIcon)
                assoc = true;
        }

        if (assoc)
            _needsAssocNotify = true;
        return all;
    }

    public void Forget(string? path, string? displayName, string? originalPath)
    {
        AppliedItem? item = FindItem(path, displayName, originalPath);
        if (item is not null)
            _applied.Remove(item);
    }

    public void FlushShell()
    {
        if (!_dirty)
            return;
        _dirty = false;

        foreach (string folder in DesktopPaths.FolderList())
        {
            if (!Directory.Exists(folder))
                continue;

            NativeMethods.SHChangeNotify(
                NativeMethods.SHCNE_UPDATEDIR,
                NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSHNOWAIT,
                folder,
                IntPtr.Zero);
        }

        if (!_needsAssocNotify)
            return;

        _needsAssocNotify = false;
        NativeMethods.SHChangeNotify(
            NativeMethods.SHCNE_ASSOCCHANGED,
            NativeMethods.SHCNF_IDLIST | NativeMethods.SHCNF_FLUSHNOWAIT,
            IntPtr.Zero,
            IntPtr.Zero);
    }

    private static string RememberedOriginal(string? originalPath, string source)
    {
        if (!string.IsNullOrWhiteSpace(originalPath) && !FenceItemStore.IsUnderRoot(originalPath))
            return originalPath.Trim();
        if (!FenceItemStore.IsUnderRoot(source))
            return source;
        return originalPath?.Trim() ?? source;
    }

    private DesktopHidePlan PlanFor(string? path, string? displayName, string? originalPath)
    {
        IReadOnlyList<string> folders = DesktopPaths.FolderList();
        string? resolved = FirstExisting(path, displayName, originalPath);
        string? parsing = null;
        if (resolved is null || !Exists(resolved) || FenceItemStore.IsUnderRoot(resolved))
        {
            if (resolved is null || !Exists(resolved))
            {
                parsing = ShellDesktopNamespace.GetParsingName(
                    FirstNonEmpty(path, displayName) ?? "");
            }
        }

        DesktopHidePlan plan = DesktopHide.For(resolved, folders, parsing);
        if (plan.Kind == DesktopHideKind.MoveToStore && !Exists(plan.Key))
            return new DesktopHidePlan(DesktopHideKind.None, plan.Key);

        return plan;
    }

    private AppliedItem? FindItem(string? path, string? displayName, string? originalPath)
    {
        foreach (string? candidate in Candidates(path, displayName, originalPath))
        {
            if (candidate is null)
                continue;
            AppliedItem? exact = _applied.FirstOrDefault(item =>
                KeysMatch(item, candidate));
            if (exact is not null)
                return exact;
        }

        string stem = Path.GetFileNameWithoutExtension(FirstNonEmpty(displayName, path) ?? "");
        if (string.IsNullOrEmpty(stem))
            return null;

        AppliedItem? match = null;
        foreach (AppliedItem item in _applied)
        {
            if (!StemMatches(item, stem))
                continue;
            if (match is not null)
                return null;
            match = item;
        }

        return match;
    }

    private static bool KeysMatch(AppliedItem item, string candidate) =>
        (!string.IsNullOrEmpty(item.StoragePath)
         && item.StoragePath.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrEmpty(item.OriginalPath)
            && item.OriginalPath.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        || item.Key.Equals(candidate, StringComparison.OrdinalIgnoreCase);

    private static bool StemMatches(AppliedItem item, string stem) =>
        Path.GetFileNameWithoutExtension(item.FileName).Equals(stem, StringComparison.OrdinalIgnoreCase)
        || Path.GetFileNameWithoutExtension(item.StoragePath ?? "").Equals(stem, StringComparison.OrdinalIgnoreCase)
        || Path.GetFileNameWithoutExtension(item.OriginalPath ?? "").Equals(stem, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string?> Candidates(string? path, string? displayName, string? originalPath)
    {
        yield return path;
        yield return originalPath;
        yield return displayName;
        if (!string.IsNullOrWhiteSpace(path))
        {
            yield return Path.GetFileName(path);
            yield return Path.GetFileNameWithoutExtension(path);
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            yield return Path.GetFileName(displayName);
            yield return Path.GetFileNameWithoutExtension(displayName);
        }
    }

    private bool TryRestore(AppliedItem item, out string? restoredPath)
    {
        restoredPath = null;
        if (item.Kind == DesktopHideKind.NamespaceIcon)
        {
            RestoreNamespaceVisible(item.Key);
            return true;
        }

        string? source = item.StoragePath;
        if (string.IsNullOrEmpty(source) || !Exists(source))
            return false;

        IReadOnlyList<string> folders = DesktopPaths.FolderList();
        string destFolder = FenceItemStore.RestoreDirectory(item.OriginalPath, folders);
        string fileName = Path.GetFileName(item.OriginalPath)
                          ?? item.FileName
                          ?? Path.GetFileName(source);
        string dest = FenceItemStore.UniqueDestination(destFolder, fileName);
        if (!ShellFileMove.Move(source, destFolder, Path.GetFileName(dest)))
            return false;

        restoredPath = dest;
        return true;
    }

    internal static bool SetNamespaceHidden(string clsid, bool hidden) =>
        SetNamespaceHidden(clsid, hidden, out _);

    internal static bool SetNamespaceHidden(string clsid, bool hidden, out bool changed)
    {
        changed = false;
        if (!DesktopHide.TryNamespaceKey(clsid, out string canonical))
            return false;

        try
        {
            int expected = hidden ? 1 : 0;
            string legacy = "::" + canonical;
            bool legacyPresent = ValueExists(NewStartPanel, legacy)
                                 || ValueExists(ClassicStartMenu, legacy);
            bool already =
                ReadDword(NewStartPanel, canonical) == expected
                && ReadDword(ClassicStartMenu, canonical) == expected
                && !legacyPresent;
            if (already)
                return true;

            WriteDword(NewStartPanel, canonical, expected);
            WriteDword(ClassicStartMenu, canonical, expected);
            DeleteValue(NewStartPanel, legacy);
            DeleteValue(ClassicStartMenu, legacy);
            changed = true;
            return ReadDword(NewStartPanel, canonical) == expected
                   && ReadDword(ClassicStartMenu, canonical) == expected
                   && !ValueExists(NewStartPanel, legacy)
                   && !ValueExists(ClassicStartMenu, legacy);
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyNamespaceHidden(string clsid) => SetNamespaceHidden(clsid, true);

    private static void RestoreNamespaceVisible(string clsid)
    {
        try
        {
            SetNamespaceHidden(clsid, false);
        }
        catch
        {
            /* chave pode não existir */
        }
    }

    private static void WriteDword(string subkey, string name, int value)
    {
        if (!DesktopHide.IsCanonicalNamespaceKey(name))
            throw new ArgumentException("O Registro só aceita CLSID canónico {GUID}.", nameof(name));

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subkey);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static int? ReadDword(string subkey, string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subkey);
        object? value = key?.GetValue(name);
        return value is int dword ? dword : null;
    }

    private static bool ValueExists(string subkey, string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subkey);
        return key?.GetValue(name) is not null;
    }

    private static void DeleteValue(string subkey, string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subkey, writable: true);
        try { key?.DeleteValue(name, throwOnMissingValue: false); }
        catch { /* valor ausente ou chave só de leitura */ }
    }

    private static string? FirstExisting(string? path, string? displayName, string? originalPath)
    {
        foreach (string? candidate in new[] { path, originalPath, displayName })
        {
            string? resolved = DesktopPaths.ResolveExisting(candidate ?? "");
            if (resolved is not null)
                return resolved;
        }

        return path;
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : b;

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record AppliedItem(
        DesktopHideKind Kind,
        string Key,
        string? StoragePath,
        string? OriginalPath,
        string FileName);
}
