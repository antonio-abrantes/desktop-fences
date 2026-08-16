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
            parsingName: "::{645FF040-5081-101B-9F08-00AA002F954E}");

        plan.Kind.Should().Be(DesktopHideKind.NamespaceIcon);
        plan.Key.Should().Be("{645FF040-5081-101B-9F08-00AA002F954E}");
    }

    [Theory]
    [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("::{f02c1a0d-be21-4350-88b0-7367fc96ef3c}", "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}")]
    [InlineData("shell:MyComputerFolder", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("shell:RecycleBinFolder", "{645FF040-5081-101B-9F08-00AA002F954E}")]
    [InlineData("shell:NetworkPlacesFolder", "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}")]
    public void TryNamespaceKey_NormalizesToCanonicalClsid(string raw, string expected)
    {
        DesktopHide.TryNamespaceKey(raw, out string key).Should().BeTrue();
        key.Should().Be(expected);
        DesktopHide.IsCanonicalNamespaceKey(key).Should().BeTrue();
        DesktopHide.IsCanonicalNamespaceKey(raw).Should().Be(raw == expected);
        DesktopHide.ToShellParsingName(raw).Should().Be("::" + expected);
    }

    [Fact]
    public void TryNamespaceKey_RejectsUnnormalizableValue()
    {
        DesktopHide.TryNamespaceKey("not-a-clsid", out _).Should().BeFalse();
        DesktopHide.TryNamespaceKey("::{not-a-guid}", out _).Should().BeFalse();
        FluentActions.Invoking(() => DesktopHide.RequireNamespaceKey("::{bad}", "X"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*X*");
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
        DesktopHide.RecycleBin.Should().Be(new Guid("645FF040-5081-101B-9F08-00AA002F954E"));
    }

    [Fact]
    public void WithoutHidden_ClearsOnlyHiddenBit()
    {
        FileAttributes original = FileAttributes.Archive | FileAttributes.Hidden;
        DesktopHide.WithoutHidden(original).Should().Be(FileAttributes.Archive);
    }
}
