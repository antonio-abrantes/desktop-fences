using System.Security.Cryptography;
using DesktopFences.Core;
using DesktopFences.Core.Models;
using DesktopFences.Core.Occupancy;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Recovery;
using DesktopFences.Core.Transactions;

namespace DesktopFences.Native;

public sealed record EmergencyRecoveryOptions(
    string LayoutPath,
    string ItemsRoot,
    string TransactionsRoot,
    string SnapshotPath,
    IReadOnlyList<string> DesktopFolders,
    string RecoveryRoot)
{
    public static EmergencyRecoveryOptions Default() => new(
        LayoutStore.DefaultPath(),
        FenceItemStore.Root(),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopFences", "Transactions"),
        DesktopRecoverySnapshotStore.DefaultPath(),
        DesktopPaths.FolderList(),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopFences", "Recovery"));
}

public sealed record EmergencyRecoveryReport(
    bool Success,
    int CopiedFiles,
    int CopiedDirectories,
    int IdenticalItems,
    int ConflictsPreserved,
    int PositionsRestored,
    string RecoverySessionPath,
    IReadOnlyList<string> Errors,
    int NamespaceRestored,
    int NamespacePending);

/// <summary>
/// Restauração independente e não destrutiva: o Store nunca é removido e
/// conflitos recebem outro nome. Só limpa as referências ativas após copiar tudo.
/// </summary>
public sealed class EmergencyRecoveryService
{
    private readonly EmergencyRecoveryOptions _options;
    private readonly bool _usesLiveDesktop;

    public EmergencyRecoveryService(EmergencyRecoveryOptions? options = null)
    {
        _options = options ?? EmergencyRecoveryOptions.Default();
        _usesLiveDesktop = options is null;
    }

    public IReadOnlyList<string> PendingLegacyNamespace()
    {
        var items = new List<FenceItemState>();
        var snapshots = new List<DesktopRecoveryItem>();
        var journal = new List<CustodyTransactionItem>();
        var pending = new List<string>();
        ReadPreviousNamespaceEvidence("", items, snapshots, journal, pending);
        foreach (CustodyTransactionItem entry in journal.Where(entry => entry.NamespaceItem))
            if (!items.Any(item => item.ItemId == entry.ItemId))
                items.Add(new FenceItemState { ItemId = entry.ItemId, Kind = FenceItemKind.Namespace, Name = entry.Name });
        foreach (FenceItemState item in items)
        {
            string? key = DesktopCustodyBatch.ResolveNamespaceKey(item, journal, snapshots);
            if (key is null || !DesktopVisibility.IsNamespaceStateSet(key, hidden: false))
                pending.Add(item.Name);
        }
        return pending;
    }

    public EmergencyRecoveryReport RestoreAll(bool restorePositions = false)
    {
        var icons = _usesLiveDesktop ? new DesktopIconService() : null;
        DesktopLayoutProtection? protection = icons?.CaptureLayoutProtection([]);
        try { return RestoreAllCore(restorePositions, protection is not null ? icons : null); }
        finally
        {
            try { icons?.PreserveDesktopLayout(protection); }
            catch { /* A recuperação dos arquivos pode terminar mesmo sem acesso à vista do Explorer. */ }
        }
    }

    private EmergencyRecoveryReport RestoreAllCore(bool restorePositions, DesktopIconService? icons)
    {
        string session = Path.Combine(
            _options.RecoveryRoot,
            $"Emergency-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(session);
        var errors = new List<string>();
        var stats = new CopyStats();

        LayoutStore layoutStore = new(_options.LayoutPath);
        LayoutDocument layout;
        try { layout = layoutStore.LoadOrEmpty(); }
        catch (Exception ex)
        {
            errors.Add("Layout inválido: " + ex.Message);
            layout = new LayoutDocument();
        }
        var snapshotStore = new DesktopRecoverySnapshotStore(_options.SnapshotPath);
        DesktopRecoverySnapshot? snapshot = snapshotStore.Load();
        if (snapshot is null && restorePositions)
        {
            DesktopSnapshot visible = new DesktopIconService().Capture();
            if (visible.Connected)
            {
                snapshot = DesktopRecoverySnapshotBuilder.Build(
                    visible.Icons, layout, DesktopPaths.ResolveExisting);
                try { snapshotStore.Save(snapshot); }
                catch (Exception ex) { errors.Add("Falha ao criar snapshot de emergência: " + ex.Message); }
            }
        }
        ArchiveFile(_options.LayoutPath, session, errors);
        ArchiveFile(_options.LayoutPath + ".bak", session, errors);
        ArchiveFile(_options.SnapshotPath, session, errors);
        ArchiveFile(_options.SnapshotPath + ".bak", session, errors);

        List<FenceItemState> namespaceItems = layout.Fences.SelectMany(fence => fence.Items)
            .Where(item => item.Kind == FenceItemKind.Namespace).ToList();
        var evidence = new List<CustodyTransactionItem>();
        var snapshotEvidence = new List<DesktopRecoveryItem>(snapshot?.Items ?? []);
        ReadNamespaceEvidence(_options.TransactionsRoot, evidence);
        if (namespaceItems.Count == 0)
            ReadPreviousNamespaceEvidence(session, namespaceItems, snapshotEvidence, evidence, errors);
        foreach (CustodyTransactionItem item in evidence.Where(item => item.NamespaceItem))
        {
            if (namespaceItems.Any(existing => existing.ItemId == item.ItemId))
                continue;
            namespaceItems.Add(new FenceItemState
            {
                ItemId = item.ItemId, Kind = FenceItemKind.Namespace, Name = item.Name
            });
        }

        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FenceItemState item in layout.Fences.SelectMany(fence => fence.Items))
        {
            if (item.Kind == FenceItemKind.Namespace)
                continue;

            string? source = SourceFor(item);
            if (string.IsNullOrWhiteSpace(source) || !Exists(source))
                continue;
            string destination = DestinationFor(item, snapshot);
            if (Directory.Exists(source)
                && !string.Equals(
                    Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)),
                    Path.GetFileName(destination.TrimEnd(Path.DirectorySeparatorChar)),
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(Path.GetFileName(source), Path.GetFileName(UserDesktop()), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string child in Directory.EnumerateFileSystemEntries(source))
                        RestorePayload(child, Path.Combine(UserDesktop(), Path.GetFileName(child)), handled, stats, errors);
                    handled.Add(Normalize(source));
                }
                else
                {
                    errors.Add($"Payload incompatível preservado sem alteração: {source} (destino esperado: {destination})");
                }
                continue;
            }
            RestorePayload(source, destination, handled, stats, errors);
        }

        RestoreUnreferencedStore(snapshot, handled, stats, errors);
        int namespaceRestored = 0;
        int namespacePending = 0;
        bool namespaceChanged = false;
        var restoredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FenceItemState item in namespaceItems)
        {
            string? key = DesktopCustodyBatch.ResolveNamespaceKey(item, evidence, snapshotEvidence);
            if (key is not null && restoredKeys.Contains(key))
                continue;
            if (key is not null && DesktopVisibility.SetNamespaceHidden(key, hidden: false, out bool changed))
            {
                namespaceChanged |= changed;
                restoredKeys.Add(key);
                namespaceRestored++;
                continue;
            }
            namespacePending++;
            errors.Add(key is null
                ? $"Não foi possível identificar o ícone de sistema: {item.Name}"
                : $"Não foi possível restaurar a visibilidade de {item.Name} ({key}).");
        }
        DesktopCustodyBatch.FlushShell(new ShellNotifyRequest(false, namespaceChanged), _options.DesktopFolders);

        int positioned = 0;
        if (errors.Count == 0)
        {
            try
            {
                QuarantineTransactionsAndResetLayout(layoutStore, layout, session);
                if (restorePositions && icons is not null)
                    positioned = RestorePositions(snapshot, layout, stats, icons);
            }
            catch (Exception ex)
            {
                errors.Add("Falha ao finalizar o estado seguro: " + ex.Message);
            }
        }

        try { WriteReceipt(session, stats, positioned, namespaceRestored, namespacePending, errors); }
        catch (Exception ex) { errors.Add("Falha ao gravar o recibo da recuperação: " + ex.Message); }
        return new EmergencyRecoveryReport(
            errors.Count == 0,
            stats.Files,
            stats.Directories,
            stats.Identical,
            stats.Conflicts,
            positioned,
            session,
            errors,
            namespaceRestored,
            namespacePending);
    }

    private string? SourceFor(FenceItemState item)
    {
        if (item.ItemId != Guid.Empty && !string.IsNullOrWhiteSpace(item.StorageName))
        {
            string stable = FenceItemStore.PayloadPath(_options.ItemsRoot, item.ItemId, item.StorageName);
            if (Exists(stable))
                return stable;
        }
        return !string.IsNullOrWhiteSpace(item.Path) && IsUnder(_options.ItemsRoot, item.Path)
            ? item.Path
            : null;
    }

    private string DestinationFor(FenceItemState item, DesktopRecoverySnapshot? snapshot)
    {
        string? originalPath = item.OriginalPath;
        if (IsDesktopDestination(originalPath))
            return originalPath!;
        DesktopRecoveryItem? known = snapshot?.Items.FirstOrDefault(candidate =>
            item.ItemId != Guid.Empty && candidate.ItemId == item.ItemId);
        string? knownPath = known?.OriginalPath;
        if (IsDesktopDestination(knownPath))
            return knownPath!;
        string leaf = Path.GetFileName(item.StorageName ?? item.Name);
        if (string.IsNullOrWhiteSpace(leaf))
            leaf = item.ItemId == Guid.Empty ? "Item-recuperado" : $"Item-{item.ItemId:D}";
        return Path.Combine(UserDesktop(), leaf);
    }

    private void RestoreUnreferencedStore(
        DesktopRecoverySnapshot? snapshot,
        HashSet<string> handled,
        CopyStats stats,
        List<string> errors)
    {
        if (!Directory.Exists(_options.ItemsRoot))
            return;

        foreach (string itemFolder in Directory.EnumerateDirectories(_options.ItemsRoot))
        {
            Guid? itemId = Guid.TryParse(Path.GetFileName(itemFolder), out Guid parsed) ? parsed : null;
            foreach (string payload in Directory.EnumerateFileSystemEntries(itemFolder))
            {
                if (handled.Contains(Normalize(payload)))
                    continue;

                if (Directory.Exists(payload)
                    && string.Equals(Path.GetFileName(payload), Path.GetFileName(UserDesktop()), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string child in Directory.EnumerateFileSystemEntries(payload))
                        RestorePayload(child, Path.Combine(UserDesktop(), Path.GetFileName(child)), handled, stats, errors);
                    handled.Add(Normalize(payload));
                    continue;
                }

                DesktopRecoveryItem? known = itemId is Guid id
                    ? snapshot?.Items.FirstOrDefault(item => item.ItemId == id)
                    : null;
                string? knownPath = known?.OriginalPath;
                string destination = IsDesktopDestination(knownPath)
                    ? knownPath!
                    : Path.Combine(UserDesktop(), Path.GetFileName(payload));
                RestorePayload(payload, destination, handled, stats, errors);
            }
        }
    }

    private static void RestorePayload(
        string source,
        string destination,
        HashSet<string> handled,
        CopyStats stats,
        List<string> errors)
    {
        string normalized = Normalize(source);
        if (!handled.Add(normalized))
            return;
        try
        {
            string? created = Directory.Exists(source) ? CopyDirectory(source, destination, stats)
                : File.Exists(source) ? CopyFile(source, destination, stats) : null;
            if (created is not null && DesktopFileIdentity.Read(created) is { } identity)
                stats.Created[Normalize(destination)] = (created, identity);
        }
        catch (Exception ex)
        {
            errors.Add($"{source} → {destination}: {ex.Message}");
        }
    }

    private static string? CopyDirectory(string source, string destination, CopyStats stats)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Ponto de nova análise não é copiado automaticamente.");
        if (File.Exists(destination))
        {
            destination = UniqueRecoveryName(destination);
            stats.Conflicts++;
        }
        bool newDirectory = !Directory.Exists(destination);
        Directory.CreateDirectory(destination);
        stats.Directories++;
        foreach (string directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), stats);
        foreach (string file in Directory.EnumerateFiles(source))
            CopyFile(file, Path.Combine(destination, Path.GetFileName(file)), stats);
        try
        {
            Directory.SetLastWriteTimeUtc(destination, Directory.GetLastWriteTimeUtc(source));
            File.SetAttributes(destination, File.GetAttributes(source) & ~FileAttributes.ReparsePoint);
        }
        catch { }
        return newDirectory ? destination : null;
    }

    private static string? CopyFile(string source, string destination, CopyStats stats)
    {
        string final = destination;
        if (File.Exists(destination))
        {
            if (FilesEqual(source, destination))
            {
                stats.Identical++;
                return null;
            }
            final = UniqueRecoveryName(destination);
            stats.Conflicts++;
        }
        else if (Directory.Exists(destination))
        {
            final = UniqueRecoveryName(destination);
            stats.Conflicts++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(final)!);
        File.Copy(source, final, overwrite: false);
        try
        {
            File.SetLastWriteTimeUtc(final, File.GetLastWriteTimeUtc(source));
            File.SetAttributes(final, File.GetAttributes(source) & ~FileAttributes.ReparsePoint);
        }
        catch { }
        stats.Files++;
        return final;
    }

    private void QuarantineTransactionsAndResetLayout(LayoutStore store, LayoutDocument layout, string session)
    {
        string? movedTransactions = null;
        try
        {
            if (Directory.Exists(_options.TransactionsRoot))
            {
                movedTransactions = Path.Combine(session, "Transactions");
                Directory.Move(_options.TransactionsRoot, movedTransactions);
            }

            LayoutDocument safe = layout.Version == LayoutDocument.CurrentVersion
                ? LayoutStore.Clone(layout)
                : new LayoutDocument
                {
                    UiLanguage = layout.UiLanguage,
                    Fences = layout.Fences.Select(fence => new FenceState
                    {
                        Id = fence.Id == Guid.Empty ? Guid.NewGuid() : fence.Id,
                        Title = fence.Title,
                        TitleAlignment = fence.TitleAlignment,
                        X = fence.X,
                        Y = fence.Y,
                        Width = fence.Width,
                        Height = fence.Height,
                        MonitorDeviceName = fence.MonitorDeviceName,
                        Collapsed = fence.Collapsed,
                        Theme = fence.Theme,
                        Items = []
                    }).ToList()
                };
            safe.Version = LayoutDocument.CurrentVersion;
            safe.Revision = Math.Max(1, safe.Revision + 1);
            foreach (FenceState fence in safe.Fences)
                fence.Items.Clear();
            store.Save(safe);
        }
        catch
        {
            if (movedTransactions is not null
                && Directory.Exists(movedTransactions)
                && !Directory.Exists(_options.TransactionsRoot))
            {
                try { Directory.Move(movedTransactions, _options.TransactionsRoot); } catch { }
            }
            throw;
        }
    }

    private static int RestorePositions(DesktopRecoverySnapshot? snapshot, LayoutDocument layout,
        CopyStats stats, DesktopIconService icons)
    {
        IEnumerable<DesktopRecoveryItem> items = snapshot?.Items
            ?? layout.Fences.SelectMany(fence => fence.Items)
                .Where(item => item.OriginalX.HasValue && item.OriginalY.HasValue)
                .Select(item => new DesktopRecoveryItem
                {
                    Name = item.Name,
                    OriginalPath = item.OriginalPath,
                    X = item.OriginalX!.Value,
                    Y = item.OriginalY!.Value
                });
        var placements = new List<DesktopPlacement>();
        foreach (DesktopRecoveryItem item in items)
        {
            if (item.Kind != FenceItemKind.Stored || item.OriginalPath is not { } original ||
                !Path.IsPathFullyQualified(original) || !stats.Created.TryGetValue(Normalize(original), out var copy))
                continue;
            placements.Add(new DesktopPlacement(copy.Path, item.X, item.Y, null, null, copy.Identity,
                VerifyFileIdentity: true));
        }
        if (placements.Count == 0)
            return 0;

        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int best = 0;
        for (int attempt = 0; attempt < 8 && best < placements.Count; attempt++)
        {
            if (attempt > 0)
                Thread.Sleep(250);
            best = Math.Max(best, icons.PlaceRevealedItems(placements, handled));
        }
        return best;
    }

    private static void ReadNamespaceEvidence(string folder, List<CustodyTransactionItem> evidence) =>
        evidence.AddRange(new TransactionJournalStore(folder).LoadReadable().SelectMany(journal => journal.Items));

    private void ReadPreviousNamespaceEvidence(
        string currentSession, List<FenceItemState> items, List<DesktopRecoveryItem> snapshots,
        List<CustodyTransactionItem> evidence, List<string> errors)
    {
        try
        {
            if (!Directory.Exists(_options.RecoveryRoot))
                return;
            foreach (string previous in Directory.EnumerateDirectories(_options.RecoveryRoot, "Emergency-*")
                .Where(folder => folder != currentSession).OrderByDescending(folder => folder, StringComparer.Ordinal))
            {
                string receipt = Path.Combine(previous, "recovery-result.txt");
                if (!File.Exists(receipt))
                    continue;
                string[] lines = File.ReadAllLines(receipt);
                if (!lines.Contains("errors=0"))
                    continue;
                if (lines.Any(line => line.StartsWith("namespaceRestored=", StringComparison.Ordinal)))
                    return;

                var archived = new LayoutStore(Path.Combine(previous, Path.GetFileName(_options.LayoutPath)));
                items.AddRange(archived.LoadOrEmpty().Fences.SelectMany(fence => fence.Items)
                    .Where(item => item.Kind == FenceItemKind.Namespace));
                var previousSnapshot = new DesktopRecoverySnapshotStore(
                    Path.Combine(previous, Path.GetFileName(_options.SnapshotPath))).Load();
                snapshots.AddRange(previousSnapshot?.Items ?? []);
                ReadNamespaceEvidence(Path.Combine(previous, "Transactions"), evidence);
                return;
            }
        }
        catch (Exception ex)
        {
            errors.Add("Falha ao verificar a recuperação anterior: " + ex.Message);
        }
    }

    private string UserDesktop() => _options.DesktopFolders.FirstOrDefault(Directory.Exists)
                                    ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private bool IsDesktopDestination(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && _options.DesktopFolders.Any(folder => IsUnder(folder, path));

    private static bool FilesEqual(string first, string second)
    {
        var a = new FileInfo(first);
        var b = new FileInfo(second);
        if (a.Length != b.Length)
            return false;
        using SHA256 sha = SHA256.Create();
        using FileStream firstStream = File.OpenRead(first);
        byte[] firstHash = sha.ComputeHash(firstStream);
        using FileStream secondStream = File.OpenRead(second);
        byte[] secondHash = sha.ComputeHash(secondStream);
        return firstHash.SequenceEqual(secondHash);
    }

    private static string UniqueRecoveryName(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        for (int index = 0; index < 10_000; index++)
        {
            string suffix = index == 0 ? stamp : $"{stamp}-{index}";
            string candidate = Path.Combine(directory, $"{stem} (recuperado DesktopFences {suffix}){extension}");
            if (!Exists(candidate))
                return candidate;
        }
        return Path.Combine(directory, $"{stem} (recuperado DesktopFences {Guid.NewGuid():N}){extension}");
    }

    private static void ArchiveFile(string path, string session, List<string> errors)
    {
        if (!File.Exists(path))
            return;
        try { File.Copy(path, Path.Combine(session, Path.GetFileName(path)), overwrite: false); }
        catch (Exception ex) { errors.Add("Falha ao arquivar " + path + ": " + ex.Message); }
    }

    private static void WriteReceipt(string session, CopyStats stats, int positioned,
        int namespaceRestored, int namespacePending, IReadOnlyList<string> errors)
    {
        string[] lines =
        [
            $"utc={DateTimeOffset.UtcNow:O}",
            $"files={stats.Files}",
            $"directories={stats.Directories}",
            $"identical={stats.Identical}",
            $"conflicts={stats.Conflicts}",
            $"positions={positioned}",
            $"namespaceRestored={namespaceRestored}",
            $"namespacePending={namespacePending}",
            $"errors={errors.Count}",
            .. errors.Select(error => "error=" + error)
        ];
        File.WriteAllLines(Path.Combine(session, "recovery-result.txt"), lines);
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static bool IsUnder(string root, string path)
    {
        try
        {
            string normalizedRoot = Normalize(root) + Path.DirectorySeparatorChar;
            return Normalize(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed class CopyStats
    {
        public Dictionary<string, (string Path, string Identity)> Created { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Files;
        public int Directories;
        public int Identical;
        public int Conflicts;
    }
}
