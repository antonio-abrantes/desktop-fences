using System.IO;
using DesktopFences.Core.Models;
using DesktopFences.Core.Persistence;
using DesktopFences.Core.Recovery;
using DesktopFences.Native;
using FluentAssertions;
using Xunit;

namespace DesktopFences.App.Tests;

public sealed class EmergencyRecoveryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "df-emergency-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RestoreAll_CopiesStableAndCatastrophicPayloadsThenClearsActiveCustody()
    {
        string desktop = Path.Combine(_root, "Desktop");
        string publicDesktop = Path.Combine(_root, "PublicDesktop");
        string items = Path.Combine(_root, "Local", "Items");
        string transactions = Path.Combine(_root, "Local", "Transactions");
        string recovery = Path.Combine(_root, "Local", "Recovery");
        string layoutPath = Path.Combine(_root, "Roaming", "layout.json");
        string snapshotPath = Path.Combine(recovery, "desktop-snapshot.json");
        Directory.CreateDirectory(desktop);
        Directory.CreateDirectory(publicDesktop);
        Directory.CreateDirectory(items);
        Directory.CreateDirectory(transactions);
        File.WriteAllText(Path.Combine(transactions, "pending.json"), "preserve");

        Guid envId = Guid.NewGuid();
        string envFolder = Path.Combine(items, envId.ToString("D"));
        Directory.CreateDirectory(envFolder);
        File.WriteAllText(Path.Combine(envFolder, ".env"), "secret");
        Guid catastropheId = Guid.NewGuid();
        string capturedDesktop = Path.Combine(items, catastropheId.ToString("D"), "Desktop");
        Directory.CreateDirectory(Path.Combine(capturedDesktop, "Projeto"));
        File.WriteAllText(Path.Combine(capturedDesktop, "Projeto", "dados.txt"), "important");
        File.WriteAllText(Path.Combine(capturedDesktop, "Atalho.lnk"), "shortcut");

        var layoutStore = new LayoutStore(layoutPath);
        layoutStore.Save(new LayoutDocument
        {
            Revision = 10,
            Fences =
            [
                new FenceState
                {
                    Title = "Projetos",
                    Items =
                    [
                        new FenceItemState
                        {
                            ItemId = envId,
                            Kind = FenceItemKind.Stored,
                            Name = ".env",
                            StorageName = ".env",
                            OriginalPath = Path.Combine(desktop, ".env"),
                            OriginalX = 10,
                            OriginalY = 20
                        }
                    ]
                }
            ]
        });
        new DesktopRecoverySnapshotStore(snapshotPath).Save(new DesktopRecoverySnapshot
        {
            Items =
            [
                new DesktopRecoveryItem
                {
                    ItemId = envId,
                    Name = ".env",
                    OriginalPath = Path.Combine(desktop, ".env"),
                    X = 10,
                    Y = 20
                }
            ]
        });

        var service = new EmergencyRecoveryService(new EmergencyRecoveryOptions(
            layoutPath, items, transactions, snapshotPath,
            [desktop, publicDesktop], recovery));

        EmergencyRecoveryReport report = service.RestoreAll(restorePositions: false);

        report.Success.Should().BeTrue(string.Join(Environment.NewLine, report.Errors));
        File.ReadAllText(Path.Combine(desktop, ".env")).Should().Be("secret");
        File.ReadAllText(Path.Combine(desktop, "Projeto", "dados.txt")).Should().Be("important");
        File.ReadAllText(Path.Combine(desktop, "Atalho.lnk")).Should().Be("shortcut");
        File.Exists(Path.Combine(envFolder, ".env")).Should().BeTrue("the Store is the safety backup");
        File.Exists(Path.Combine(capturedDesktop, "Projeto", "dados.txt")).Should().BeTrue();
        layoutStore.LoadOrEmpty().Fences.Single().Items.Should().BeEmpty();
        Directory.Exists(transactions).Should().BeFalse("the original journal directory was quarantined");
        Directory.EnumerateFiles(report.RecoverySessionPath, "pending.json", SearchOption.AllDirectories)
            .Should().ContainSingle();
    }

    [Fact]
    public void RestoreAll_NeverOverwritesDifferentDestination()
    {
        string desktop = Path.Combine(_root, "Desktop");
        string items = Path.Combine(_root, "Items");
        string transactions = Path.Combine(_root, "Transactions");
        string recovery = Path.Combine(_root, "Recovery");
        string layoutPath = Path.Combine(_root, "layout.json");
        Directory.CreateDirectory(desktop);
        Directory.CreateDirectory(transactions);
        Guid id = Guid.NewGuid();
        string itemFolder = Path.Combine(items, id.ToString("D"));
        Directory.CreateDirectory(itemFolder);
        File.WriteAllText(Path.Combine(itemFolder, "dados.txt"), "store-version");
        File.WriteAllText(Path.Combine(desktop, "dados.txt"), "user-version");
        new LayoutStore(layoutPath).Save(new LayoutDocument
        {
            Fences =
            [
                new FenceState
                {
                    Items =
                    [
                        new FenceItemState
                        {
                            ItemId = id,
                            Name = "dados.txt",
                            StorageName = "dados.txt",
                            OriginalPath = Path.Combine(desktop, "dados.txt")
                        }
                    ]
                }
            ]
        });

        EmergencyRecoveryReport report = new EmergencyRecoveryService(new EmergencyRecoveryOptions(
            layoutPath, items, transactions, Path.Combine(recovery, "snapshot.json"),
            [desktop], recovery)).RestoreAll(restorePositions: false);

        report.Success.Should().BeTrue();
        File.ReadAllText(Path.Combine(desktop, "dados.txt")).Should().Be("user-version");
        report.ConflictsPreserved.Should().Be(1);
        Directory.EnumerateFiles(desktop, "dados (recuperado DesktopFences *).txt")
            .Should().ContainSingle()
            .Which.Should().Match<string>(path => File.ReadAllText(path) == "store-version");
    }

    [Fact]
    public void RestoreAll_DoesNotTrustDestinationOutsideConfiguredDesktop()
    {
        string desktop = Path.Combine(_root, "Desktop");
        string outside = Path.Combine(_root, "Outside");
        string items = Path.Combine(_root, "Items");
        string transactions = Path.Combine(_root, "Transactions");
        string recovery = Path.Combine(_root, "Recovery");
        string layoutPath = Path.Combine(_root, "layout.json");
        Directory.CreateDirectory(desktop);
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(transactions);
        Guid id = Guid.NewGuid();
        string itemFolder = Path.Combine(items, id.ToString("D"));
        Directory.CreateDirectory(itemFolder);
        File.WriteAllText(Path.Combine(itemFolder, "dados.txt"), "safe");
        new LayoutStore(layoutPath).Save(new LayoutDocument
        {
            Fences =
            [
                new FenceState
                {
                    Items =
                    [
                        new FenceItemState
                        {
                            ItemId = id,
                            Name = "dados.txt",
                            StorageName = "dados.txt",
                            OriginalPath = Path.Combine(outside, "dados.txt")
                        }
                    ]
                }
            ]
        });

        EmergencyRecoveryReport report = new EmergencyRecoveryService(new EmergencyRecoveryOptions(
            layoutPath, items, transactions, Path.Combine(recovery, "snapshot.json"),
            [desktop], recovery)).RestoreAll(restorePositions: false);

        report.Success.Should().BeTrue();
        File.Exists(Path.Combine(outside, "dados.txt")).Should().BeFalse();
        File.ReadAllText(Path.Combine(desktop, "dados.txt")).Should().Be("safe");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
