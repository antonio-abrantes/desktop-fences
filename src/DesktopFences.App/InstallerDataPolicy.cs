using System.IO;
using DesktopFences.Core.Fences;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;

namespace DesktopFences.App;

internal sealed record InstallerDataPaths(
    string LayoutPath,
    string RoamingRoot,
    string LocalRoot,
    string BackupRoot)
{
    public static InstallerDataPaths Default()
    {
        string roaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopFences");
        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopFences");
        return new InstallerDataPaths(
            Path.Combine(roaming, "layout.json"),
            roaming,
            local,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopFences.Backups"));
    }
}

internal sealed class InstallerDataPolicy
{
    private readonly InstallerDataPaths _paths;

    public InstallerDataPolicy(InstallerDataPaths? paths = null)
    {
        _paths = paths ?? InstallerDataPaths.Default();
    }

    public string MaintenanceLogPath => Path.Combine(_paths.LocalRoot, "maintenance-last.log");

    public void SetLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return;
        string normalized = UiLanguageCodes.Normalize(language);
        if (normalized == UiLanguageCodes.System)
            throw new InvalidDataException("O instalador aceita somente Português ou Inglês.");

        var store = new LayoutStore(_paths.LayoutPath);
        LayoutDocument document = store.LoadOrEmpty();
        if (document.Version != LayoutDocument.CurrentVersion)
            throw new InvalidDataException("A configuração precisa ser migrada antes de definir o idioma.");
        document.UiLanguage = normalized;
        document.Revision = Math.Max(1, document.Revision + 1);
        store.Save(document);
    }

    public void SetLanguageIfCurrentSchema(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return;
        var store = new LayoutStore(_paths.LayoutPath);
        LayoutDocument document = store.LoadOrEmpty();
        if (document.Version != LayoutDocument.CurrentVersion)
            return;
        SetLanguage(language);
    }

    public string ResetAfterRelease(string? language)
    {
        string archive = ArchiveCurrentState("Reset");
        DeleteDataRoots();
        SetLanguage(language ?? UiLanguageCodes.Portuguese);
        return archive;
    }

    public void RemoveAfterRelease() => DeleteDataRoots();

    public string? TryArchiveWithoutDelete(string prefix = "MaintenanceFail")
    {
        try
        {
            return ArchiveCurrentState(prefix);
        }
        catch
        {
            return null;
        }
    }

    public string ArchiveCurrentState(string prefix)
    {
        string archive = Path.Combine(
            _paths.BackupRoot,
            $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(archive);
        CopyTree(_paths.RoamingRoot, Path.Combine(archive, "Roaming"));
        CopyTree(_paths.LocalRoot, Path.Combine(archive, "Local"));
        return archive;
    }

    private void DeleteDataRoots()
    {
        DeleteTree(_paths.RoamingRoot);
        DeleteTree(_paths.LocalRoot);
    }

    private static void CopyTree(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            FileAttributes attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                continue;
            CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void DeleteTree(string root)
    {
        if (!Directory.Exists(root))
            return;
        string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(full, Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A raiz de dados não pode ser a raiz do volume.");
        Directory.Delete(full, recursive: true);
    }
}
