using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopFences.Core.Recovery;

public sealed class DesktopRecoverySnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public DesktopRecoverySnapshotStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
    }

    public string FilePath { get; }
    public string BackupPath => FilePath + ".bak";
    public string TempPath => FilePath + ".tmp";

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopFences", "Recovery", "desktop-snapshot.json");

    public DesktopRecoverySnapshot? Load()
    {
        if (TryLoad(FilePath, out DesktopRecoverySnapshot? primary))
            return primary;
        return TryLoad(BackupPath, out DesktopRecoverySnapshot? backup) ? backup : null;
    }

    public void Save(DesktopRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (!TryLoad(TempPath, out DesktopRecoverySnapshot? validated))
            throw new InvalidDataException("O snapshot temporário de recuperação é inválido.");
        Validate(validated!);
        try
        {
            if (File.Exists(FilePath))
                File.Replace(TempPath, FilePath, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(TempPath, FilePath);
        }
        catch
        {
            try { if (File.Exists(TempPath)) File.Delete(TempPath); } catch { }
            throw;
        }
    }

    private static bool TryLoad(string path, out DesktopRecoverySnapshot? snapshot)
    {
        snapshot = null;
        if (!File.Exists(path))
            return false;
        try
        {
            snapshot = JsonSerializer.Deserialize<DesktopRecoverySnapshot>(File.ReadAllText(path), JsonOptions);
            if (snapshot is null)
                return false;
            Validate(snapshot);
            return true;
        }
        catch
        {
            snapshot = null;
            return false;
        }
    }

    private static void Validate(DesktopRecoverySnapshot snapshot)
    {
        if (snapshot.Version != DesktopRecoverySnapshot.CurrentVersion)
            throw new InvalidDataException("Versão de snapshot de recuperação não suportada.");
        if (snapshot.Items.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw new InvalidDataException("Snapshot de recuperação contém item sem nome.");
    }
}
