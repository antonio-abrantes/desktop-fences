using DesktopFences.Core;
using DesktopFences.Core.Models;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class DesktopIconMatcherTests
{
    private static readonly DesktopIcon[] Icons =
    [
        new(0, "Stremio.lnk", 10, 10),
        new(1, "Relatorio", 20, 20)
    ];

    [Theory]
    [InlineData(@"C:\Users\x\Desktop\Stremio.lnk", "Stremio.lnk")]
    [InlineData("Stremio.lnk", "Stremio.lnk")]
    [InlineData("Relatorio.docx", "Relatorio")]
    public void Find_MatchesFileNameOrStem(string input, string expectedName)
    {
        DesktopIconMatcher.Find(Icons, input)!.Name.Should().Be(expectedName);
    }

    [Fact]
    public void Find_ReturnsNull_WhenUnknown()
    {
        DesktopIconMatcher.Find(Icons, "zzz.exe").Should().BeNull();
    }
}
