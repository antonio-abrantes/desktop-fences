using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopFences.Core.Models;

namespace DesktopFences.Core.Persistence;

public sealed class LayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string FilePath { get; }

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
        if (!File.Exists(FilePath))
            return new LayoutDocument();

        string json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<LayoutDocument>(json, JsonOptions) ?? new LayoutDocument();
    }

    public void Save(LayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string? dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
