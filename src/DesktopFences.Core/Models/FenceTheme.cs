namespace DesktopFences.Core.Models;

/// <summary>
/// Cores visíveis da fence. Os hex default são o visual do MVP 1 (hardcoded).
/// Não inclui fundos de hit-test (#01…) nem o Background da Window.
/// </summary>
public sealed class FenceTheme
{
    public const uint DefaultFill = 0xA80C0C12;
    public const uint DefaultBorder = 0x4DFFFFFF;
    public const uint DefaultHeader = 0x33000000;
    public const uint DefaultText = 0xF2FFFFFF;

    /// <summary>45% — abaixo o painel some no wallpaper.</summary>
    public const byte FillAlphaMin = 115;

    /// <summary>85% — acima vira caixa opaca.</summary>
    public const byte FillAlphaMax = 217;

    public const byte BorderAlphaMin = 38;
    public const byte BorderAlphaMax = 255;

    /// <summary>15% — abaixo o header some no vidro.</summary>
    public const byte HeaderAlphaMin = 38;

    /// <summary>85% — acima vira faixa opaca.</summary>
    public const byte HeaderAlphaMax = 217;

    public const byte DefaultDropBoost = 0x7F;

    public string Fill { get; set; } = ArgbColor.ToHex(DefaultFill);
    public string Border { get; set; } = ArgbColor.ToHex(DefaultBorder);
    public string Header { get; set; } = ArgbColor.ToHex(DefaultHeader);
    public string Text { get; set; } = ArgbColor.ToHex(DefaultText);

    public static FenceTheme Default() => new();

    public FenceTheme Normalized()
    {
        uint fill = ArgbColor.Parse(Fill, DefaultFill);
        fill = ArgbColor.WithAlpha(fill, ArgbColor.Clamp(ArgbColor.A(fill), FillAlphaMin, FillAlphaMax));

        uint border = ArgbColor.Parse(Border, DefaultBorder);
        border = ArgbColor.WithAlpha(border, ArgbColor.Clamp(ArgbColor.A(border), BorderAlphaMin, BorderAlphaMax));

        uint header = ArgbColor.Parse(Header, DefaultHeader);
        header = ArgbColor.WithAlpha(header, ArgbColor.Clamp(ArgbColor.A(header), HeaderAlphaMin, HeaderAlphaMax));
        uint text = ArgbColor.Parse(Text, DefaultText);

        return new FenceTheme
        {
            Fill = ArgbColor.ToHex(fill),
            Border = ArgbColor.ToHex(border),
            Header = ArgbColor.ToHex(header),
            Text = ArgbColor.ToHex(text)
        };
    }

    public uint FillArgb => ArgbColor.Parse(Fill, DefaultFill);
    public uint BorderArgb => ArgbColor.Parse(Border, DefaultBorder);
    public uint HeaderArgb => ArgbColor.Parse(Header, DefaultHeader);
    public uint TextArgb => ArgbColor.Parse(Text, DefaultText);

    public uint DropBorderArgb
    {
        get
        {
            uint border = BorderArgb;
            byte a = (byte)Math.Min(255, ArgbColor.A(border) + DefaultDropBoost);
            return ArgbColor.WithAlpha(border, a);
        }
    }

    public uint MutedTextArgb => ScaleTextAlpha(0x73);
    public uint GripTextArgb => ScaleTextAlpha(0xAA);
    public uint CollapseGlyphArgb => ScaleTextAlpha(0xCC);

    public bool IsDefault
    {
        get
        {
            FenceTheme n = Normalized();
            return n.Fill.Equals(ArgbColor.ToHex(DefaultFill), StringComparison.OrdinalIgnoreCase)
                   && n.Border.Equals(ArgbColor.ToHex(DefaultBorder), StringComparison.OrdinalIgnoreCase)
                   && n.Header.Equals(ArgbColor.ToHex(DefaultHeader), StringComparison.OrdinalIgnoreCase)
                   && n.Text.Equals(ArgbColor.ToHex(DefaultText), StringComparison.OrdinalIgnoreCase);
        }
    }

    private uint ScaleTextAlpha(byte mvpAlpha)
    {
        uint text = TextArgb;
        byte a = (byte)Math.Clamp((int)Math.Round(ArgbColor.A(text) * mvpAlpha / 242.0), 0, 255);
        return ArgbColor.WithAlpha(text, a);
    }
}
