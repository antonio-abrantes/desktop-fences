using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopFences.Core.Models;

namespace DesktopFences.Core.Persistence;

public interface ILayoutStore
{
    void Save(LayoutDocument document);
}

public sealed class LayoutStore : ILayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string FilePath { get; }
    public string BackupPath => FilePath + ".bak";
    public string TempPath => FilePath + ".tmp";

    public LayoutStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
    }

    public static string DefaultPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "DesktopFences", "layout.json");
    }

    public LayoutDocument LoadOrEmpty()
    {
        if (TryLoad(FilePath, out LayoutDocument? primary))
            return primary!;
        if (TryLoad(BackupPath, out LayoutDocument? backup))
            return backup!;
        if (!File.Exists(FilePath) && !File.Exists(BackupPath))
            return new LayoutDocument();

        throw new InvalidDataException(
            "layout.json e layout.json.bak existem, mas nenhum contém um layout válido.");
    }

    public void Save(LayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateForCommit(document);
        string? dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(document, JsonOptions);
        WriteDurable(TempPath, json);

        if (!TryLoad(TempPath, out LayoutDocument? validated))
            throw new InvalidDataException("O layout temporário não passou na validação após escrita.");
        ValidateForCommit(validated!);

        try
        {
            if (File.Exists(FilePath))
                File.Replace(TempPath, FilePath, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(TempPath, FilePath);
        }
        catch
        {
            TryDelete(TempPath);
            throw;
        }
    }

    public static LayoutDocument Clone(LayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string json = JsonSerializer.Serialize(document, JsonOptions);
        return JsonSerializer.Deserialize<LayoutDocument>(json, JsonOptions)
               ?? throw new InvalidDataException("Falha ao clonar o layout.");
    }

    public static void ValidateForCommit(LayoutDocument document)
    {
        if (document.Version != LayoutDocument.CurrentVersion)
            throw new InvalidDataException($"Somente layout v{LayoutDocument.CurrentVersion} pode ser gravado.");

        var fenceIds = new HashSet<Guid>();
        var itemIds = new HashSet<Guid>();
        var originalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FenceState fence in document.Fences)
        {
            if (fence.Id == Guid.Empty || !fenceIds.Add(fence.Id))
                throw new InvalidDataException("FenceId vazio ou duplicado.");
            foreach (FenceItemState item in fence.Items)
            {
                if (item.ItemId == Guid.Empty || !itemIds.Add(item.ItemId))
                    throw new InvalidDataException("ItemId vazio ou duplicado.");
                if (string.IsNullOrWhiteSpace(item.Name))
                    throw new InvalidDataException("Item sem nome.");
                if (item.Kind == FenceItemKind.Stored)
                {
                    if (string.IsNullOrWhiteSpace(item.StorageName)
                        || !string.Equals(Path.GetFileName(item.StorageName), item.StorageName, StringComparison.Ordinal))
                        throw new InvalidDataException("Item armazenado sem StorageName relativo válido.");
                    if (!string.IsNullOrWhiteSpace(item.OriginalPath)
                        && !originalPaths.Add(NormalizePath(item.OriginalPath)))
                        throw new InvalidDataException("Duas referências apontam para o mesmo payload original.");
                }
                else if (!string.IsNullOrEmpty(item.StorageName))
                    throw new InvalidDataException("Item de namespace não pode ter StorageName.");
                if (item.Path is not null)
                    throw new InvalidDataException("Layout v2 não pode persistir Path absoluto do store.");
            }
        }
    }

    private static bool TryLoad(string path, out LayoutDocument? document)
    {
        document = null;
        if (!File.Exists(path))
            return false;
        try
        {
            document = JsonSerializer.Deserialize<LayoutDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null || document.Version is < 1 or > LayoutDocument.CurrentVersion)
                return false;
            if (document.Version == LayoutDocument.CurrentVersion)
                ValidateForCommit(document);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteDurable(string path, string contents)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(contents);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar); }
        catch { return path.Trim(); }
    }
}
