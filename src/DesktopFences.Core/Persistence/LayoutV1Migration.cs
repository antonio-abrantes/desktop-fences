using DesktopFences.Core.Models;
using DesktopFences.Core.Transactions;

namespace DesktopFences.Core.Persistence;

public sealed record LayoutMigrationPlan(
    LayoutDocument Document,
    CustodyTransaction? Transaction,
    IReadOnlyList<string> LegacyFolders);

public static class LayoutV1Migration
{
    public static LayoutMigrationPlan Plan(
        LayoutDocument source,
        string itemsRoot,
        Func<string, bool>? exists = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemsRoot);
        if (source.Version != 1)
            throw new InvalidOperationException("A migração aceita somente layout v1.");

        exists ??= path => File.Exists(path) || Directory.Exists(path);
        LayoutDocument migrated = LayoutStore.Clone(source);
        migrated.Version = LayoutDocument.CurrentVersion;
        var transaction = new CustodyTransaction { Operation = CustodyOperationKind.Migration };
        var legacyFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (FenceState fence in migrated.Fences)
        {
            string legacyFolder = Path.Combine(itemsRoot, fence.Id.ToString("D"));
            foreach (FenceItemState item in fence.Items)
            {
                item.ItemId = Guid.NewGuid();
                string? legacyPath = item.Path;
                bool underStore = IsUnder(itemsRoot, legacyPath);
                bool underLegacy = IsUnder(legacyFolder, legacyPath);

                if (underStore && !underLegacy)
                    throw new InvalidDataException($"Item v1 ambíguo fora da pasta da fence: {item.Name}");

                if (underLegacy && !string.IsNullOrWhiteSpace(legacyPath) && exists(legacyPath))
                {
                    string storageName = Path.GetFileName(legacyPath);
                    if (string.IsNullOrWhiteSpace(storageName))
                        throw new InvalidDataException($"Payload v1 sem nome: {item.Name}");
                    if (!string.IsNullOrWhiteSpace(item.OriginalPath))
                    {
                        string expectedName = Path.GetFileName(item.OriginalPath);
                        if (!string.IsNullOrWhiteSpace(expectedName)
                            && !string.Equals(storageName, expectedName, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                $"Payload v1 incompatível com o item '{item.Name}': " +
                                $"esperado '{expectedName}', encontrado '{storageName}'.");
                        }
                    }
                    item.Kind = FenceItemKind.Stored;
                    item.StorageName = storageName;
                    string destination = FenceItemStore.PayloadPath(itemsRoot, item.ItemId, storageName);
                    transaction.Items.Add(new CustodyTransactionItem
                    {
                        ItemId = item.ItemId,
                        SourceFenceId = fence.Id,
                        TargetFenceId = fence.Id,
                        Name = item.Name,
                        SourcePath = legacyPath,
                        DestinationPath = destination
                    });
                    legacyFolders.Add(legacyFolder);
                }
                else if (underLegacy
                         && !string.IsNullOrWhiteSpace(item.OriginalPath)
                         && exists(item.OriginalPath))
                {
                    // O v1 mantinha Path apontando para o store mesmo depois de devolver o
                    // payload ao Desktop. Um encerramento parcial pode deixar este estado
                    // misturado com outros itens que ainda permanecem no store antigo.
                    item.Kind = FenceItemKind.Stored;
                    item.StorageName = Path.GetFileName(item.OriginalPath);
                }
                else if (underLegacy)
                {
                    throw new InvalidDataException($"Payload v1 ausente: {item.Name}");
                }
                else if (!string.IsNullOrWhiteSpace(legacyPath) && exists(legacyPath))
                {
                    // Em encerramento limpo do v1 o payload pode já ter regressado ao Desktop.
                    item.Kind = FenceItemKind.Stored;
                    item.StorageName = Path.GetFileName(legacyPath);
                    item.OriginalPath ??= legacyPath;
                }
                else
                {
                    item.Kind = FenceItemKind.Namespace;
                    item.StorageName = null;
                    item.OriginalPath ??= legacyPath;
                }

                item.Path = null;
            }
        }

        LayoutStore.ValidateForCommit(migrated);
        return new LayoutMigrationPlan(
            migrated,
            transaction.Items.Count == 0 ? null : transaction,
            legacyFolders.ToList());
    }

    private static bool IsUnder(string root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(path);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
