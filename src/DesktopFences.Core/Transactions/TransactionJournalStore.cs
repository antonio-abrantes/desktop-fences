using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopFences.Core.Transactions;

public interface ITransactionJournalStore
{
    string DirectoryPath { get; }
    void Save(CustodyTransaction transaction);
    IReadOnlyList<CustodyTransaction> LoadAll();
    void Delete(Guid operationId);
}

public sealed class TransactionJournalStore : ITransactionJournalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string DirectoryPath { get; }

    public TransactionJournalStore(string? directoryPath = null)
    {
        DirectoryPath = directoryPath ?? DefaultPath();
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopFences",
        "Transactions");

    public string PathFor(Guid operationId) =>
        Path.Combine(DirectoryPath, operationId.ToString("D") + ".json");

    public void Save(CustodyTransaction transaction)
    {
        Validate(transaction);
        Directory.CreateDirectory(DirectoryPath);
        transaction.UpdatedUtc = DateTimeOffset.UtcNow;
        string path = PathFor(transaction.OperationId);
        string temp = path + ".tmp";
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(transaction, JsonOptions));
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        CustodyTransaction check = ReadFile(temp);
        Validate(check);
        if (File.Exists(path))
            File.Replace(temp, path, null, ignoreMetadataErrors: true);
        else
            File.Move(temp, path);
    }

    public IReadOnlyList<CustodyTransaction> LoadAll()
    {
        if (!Directory.Exists(DirectoryPath))
            return [];

        var result = new List<CustodyTransaction>();
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json"))
            result.Add(ReadFile(path));
        return result.OrderBy(t => t.CreatedUtc).ToList();
    }

    public void Delete(Guid operationId)
    {
        string path = PathFor(operationId);
        if (File.Exists(path))
            File.Delete(path);
        string temp = path + ".tmp";
        if (File.Exists(temp))
            File.Delete(temp);
    }

    private static CustodyTransaction ReadFile(string path) =>
        JsonSerializer.Deserialize<CustodyTransaction>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Journal inválido: {path}");

    private static void Validate(CustodyTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.OperationId == Guid.Empty)
            throw new InvalidDataException("OperationId vazio.");
        if (transaction.Items.Count == 0)
            throw new InvalidDataException("Transação sem itens.");
        if (transaction.Items.Any(i => i.ItemId == Guid.Empty))
            throw new InvalidDataException("Transação contém ItemId vazio.");
        if (transaction.Items.GroupBy(i => i.ItemId).Any(g => g.Count() > 1))
            throw new InvalidDataException("Transação contém ItemId duplicado.");
    }
}
