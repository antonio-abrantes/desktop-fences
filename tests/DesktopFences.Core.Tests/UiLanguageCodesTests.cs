using DesktopFences.Core.Fences;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class UiLanguageCodesTests
{
    [Theory]
    [InlineData(null, "system")]
    [InlineData("", "system")]
    [InlineData("  ", "system")]
    [InlineData("pt", "pt")]
    [InlineData("PT", "pt")]
    [InlineData("en", "en")]
    [InlineData("system", "system")]
    [InlineData("fr", "system")]
    public void Normalize_MapsKnownCodes_AndDefaultsUnknown(string? value, string expected)
    {
        UiLanguageCodes.Normalize(value).Should().Be(expected);
    }
}
