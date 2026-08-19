using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class DesktopFenceStubRulesTests
{
    [Theory]
    [InlineData(@"C:\Users\me\Desktop\Fence.desktopfence", true)]
    [InlineData(@"C:\Users\me\Desktop\Nova fence.desktopfence", true)]
    [InlineData(@"C:\Users\me\Desktop\Fence (2).DESKTOPFENCE", true)]
    [InlineData(@"""C:\Users\me\Desktop\Fence.desktopfence""", true)]
    [InlineData(@"C:\Users\me\Desktop\notes.txt", false)]
    [InlineData(@"C:\Users\me\Desktop\Fence.desktopfence.bak", false)]
    [InlineData(@"C:\Windows\Fence.desktopfence", false)]
    [InlineData(@"C:\Users\me\Documents\Fence.desktopfence", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsStubPath_AcceptsDesktopFenceFilesOnly(string? path, bool expected)
    {
        string[] roots =
        [
            @"C:\Users\me\Desktop",
            @"C:\Users\Public\Desktop"
        ];

        DesktopFenceStubRules.IsStubPath(path, roots).Should().Be(expected);
    }

    [Fact]
    public void IsStubPath_AcceptsPublicDesktop()
    {
        DesktopFenceStubRules.IsStubPath(
                @"C:\Users\Public\Desktop\Fence.desktopfence",
                [@"C:\Users\me\Desktop", @"C:\Users\Public\Desktop"])
            .Should().BeTrue();
    }

    [Fact]
    public void IsStubPath_RejectsTraversalOutsideDesktop()
    {
        string[] roots = [@"C:\Users\me\Desktop"];

        DesktopFenceStubRules.IsStubPath(
                @"C:\Users\me\Desktop\..\..\Windows\Fence.desktopfence",
                roots)
            .Should().BeFalse();
    }

    [Fact]
    public void IsStubPath_RejectsExistingDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "df-stub-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Fence.desktopfence");
        Directory.CreateDirectory(folder);
        try
        {
            DesktopFenceStubRules.IsStubPath(folder, [root]).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(folder); } catch { }
            try { Directory.Delete(root); } catch { }
        }
    }

    [Theory]
    [InlineData("Fence.desktopfence", null, null, true)]
    [InlineData("notes.txt", @"C:\Users\me\Desktop\Fence.desktopfence", null, true)]
    [InlineData("notes.txt", null, @"C:\Users\me\Desktop\Fence.desktopfence", true)]
    [InlineData("Relatorio.docx", @"C:\Users\me\Desktop\Relatorio.docx", null, false)]
    public void ForbidsCustody_WhenAnyPathIsTheStub(
        string? name, string? path, string? originalPath, bool expected)
    {
        DesktopFenceStubRules.ForbidsCustody(name, path, originalPath).Should().Be(expected);
    }
}
