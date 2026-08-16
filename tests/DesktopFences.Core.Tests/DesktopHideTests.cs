using DesktopFences.Core.Occupancy;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class DesktopHideTests
{
    private static readonly string[] Folders =
    [
        @"C:\Users\Test\Desktop",
        @"C:\Users\Public\Desktop"
    ];

    [Fact]
    public void For_DesktopShortcut_UsesMoveToStore()
    {
        DesktopHidePlan plan = DesktopHide.For(
            @"C:\Users\Test\Desktop\VS Code.lnk", Folders);

        plan.Kind.Should().Be(DesktopHideKind.MoveToStore);
        plan.Key.Should().Be(@"C:\Users\Test\Desktop\VS Code.lnk");
    }

    [Fact]
    public void For_DocumentAnywhere_UsesMoveToStore()
    {
        DesktopHidePlan plan = DesktopHide.For(
            @"C:\Users\Test\Documents\Contrato.docx", Folders);

        plan.Kind.Should().Be(DesktopHideKind.MoveToStore);
    }

    [Fact]
    public void For_RelativeName_IsNone()
    {
        DesktopHidePlan plan = DesktopHide.For("Chrome.lnk", Folders);
        plan.Kind.Should().Be(DesktopHideKind.None);
    }

    [Fact]
    public void For_FilePath_WinsOverNamespaceParsingName()
    {
        DesktopHidePlan plan = DesktopHide.For(
            @"C:\Users\Test\Desktop\Chrome.lnk",
            Folders,
            parsingName: "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}");

        plan.Kind.Should().Be(DesktopHideKind.MoveToStore);
    }

    [Fact]
    public void For_RecycleBinParsingName_UsesNamespaceIcon()
    {
        DesktopHidePlan plan = DesktopHide.For(
            "Lixeira",
            Folders,
            parsingName: "::{645FF040-5084-101B-9F08-00AA002F954E}");

        plan.Kind.Should().Be(DesktopHideKind.NamespaceIcon);
        plan.Key.Should().Be("{645FF040-5084-101B-9F08-00AA002F954E}");
    }

    [Fact]
    public void For_ShellAlias_MapsToKnownClsid()
    {
        DesktopHide.For(null, Folders, "shell:RecycleBinFolder")
            .Key.Should().Be(DesktopHide.FormatClsid(DesktopHide.RecycleBin));
        DesktopHide.For(null, Folders, "shell:MyComputerFolder")
            .Key.Should().Be(DesktopHide.FormatClsid(DesktopHide.ThisPc));
        DesktopHide.For(null, Folders, "shell:NetworkPlacesFolder")
            .Key.Should().Be(DesktopHide.FormatClsid(DesktopHide.Network));
    }

    [Fact]
    public void WithoutHidden_ClearsOnlyHiddenBit()
    {
        FileAttributes original = FileAttributes.Archive | FileAttributes.Hidden;
        DesktopHide.WithoutHidden(original).Should().Be(FileAttributes.Archive);
    }
}
