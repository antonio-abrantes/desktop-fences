using DesktopFences.Core.Models;
using FluentAssertions;
using Xunit;

namespace DesktopFences.Core.Tests;

public sealed class FenceThemeTests
{
    [Fact]
    public void Default_MatchesMvp1HardcodedGlass()
    {
        FenceTheme theme = FenceTheme.Default().Normalized();
        theme.Fill.Should().Be("#A80C0C12");
        theme.Border.Should().Be("#4DFFFFFF");
        theme.Header.Should().Be("#33000000");
        theme.Text.Should().Be("#F2FFFFFF");
        theme.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Normalized_ClampsHeaderAlphaTo15_85Percent()
    {
        var tooClear = new FenceTheme { Header = "#10000000" }.Normalized();
        ArgbColor.A(tooClear.HeaderArgb).Should().Be(FenceTheme.HeaderAlphaMin);

        var tooSolid = new FenceTheme { Header = "#FF112233" }.Normalized();
        ArgbColor.A(tooSolid.HeaderArgb).Should().Be(FenceTheme.HeaderAlphaMax);
        ArgbColor.Rgb(tooSolid.HeaderArgb).Should().Be(0x112233u);
    }

    [Fact]
    public void Normalized_ClampsFillAlphaTo45_85Percent()
    {
        var tooClear = new FenceTheme { Fill = "#200C0C12" }.Normalized();
        ArgbColor.A(tooClear.FillArgb).Should().Be(FenceTheme.FillAlphaMin);

        var tooSolid = new FenceTheme { Fill = "#FF0C0C12" }.Normalized();
        ArgbColor.A(tooSolid.FillArgb).Should().Be(FenceTheme.FillAlphaMax);
    }

    [Fact]
    public void DropBorder_KeepsRgbAndRaisesAlpha()
    {
        var theme = new FenceTheme { Border = "#4D3366FF" }.Normalized();
        theme.DropBorderArgb.Should().Be(ArgbColor.Pack(0xCC, 0x33, 0x66, 0xFF));
    }

    [Fact]
    public void MutedAndGlyph_FollowTextRgb()
    {
        var theme = new FenceTheme { Text = "#F200AA00" }.Normalized();
        ArgbColor.Rgb(theme.MutedTextArgb).Should().Be(0x00AA00u);
        ArgbColor.Rgb(theme.GripTextArgb).Should().Be(0x00AA00u);
        ArgbColor.Rgb(theme.CollapseGlyphArgb).Should().Be(0x00AA00u);
        ArgbColor.A(theme.MutedTextArgb).Should().Be(0x73);
        ArgbColor.A(theme.GripTextArgb).Should().Be(0xAA);
        ArgbColor.A(theme.CollapseGlyphArgb).Should().Be(0xCC);
    }

    [Fact]
    public void AlphaFromPercent_RoundTripsNearDefaultFill()
    {
        ArgbColor.PercentFromAlpha(0xA8).Should().Be(66);
        ArgbColor.A(ArgbColor.WithAlpha(FenceTheme.DefaultFill, ArgbColor.AlphaFromPercent(66)))
            .Should().Be(ArgbColor.AlphaFromPercent(66));
    }
}
