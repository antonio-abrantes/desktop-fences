using System.Globalization;

namespace DesktopFences.Core.Models;

public static class ArgbColor
{
    public static uint Pack(byte a, byte r, byte g, byte b) =>
        ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

    public static byte A(uint argb) => (byte)(argb >> 24);
    public static byte R(uint argb) => (byte)(argb >> 16);
    public static byte G(uint argb) => (byte)(argb >> 8);
    public static byte B(uint argb) => (byte)argb;

    public static uint Rgb(uint argb) => argb & 0x00FFFFFFu;

    public static uint WithAlpha(uint argb, byte a) => Rgb(argb) | ((uint)a << 24);

    public static uint WithRgb(uint argb, uint rgb) => ((uint)A(argb) << 24) | (rgb & 0x00FFFFFFu);

    public static byte Clamp(byte value, byte min, byte max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    public static byte AlphaFromPercent(int percent) =>
        (byte)Math.Clamp((int)Math.Round(Math.Clamp(percent, 0, 100) * 255 / 100.0), 0, 255);

    public static int PercentFromAlpha(byte alpha) =>
        (int)Math.Round(alpha * 100 / 255.0);

    public static uint Parse(string? hex, uint fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;

        ReadOnlySpan<char> span = hex.Trim().AsSpan();
        if (span.StartsWith("#"))
            span = span[1..];

        if (span.Length == 6)
        {
            if (!uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                return fallback;
            return 0xFF000000u | rgb;
        }

        if (span.Length == 8 && uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            return argb;

        return fallback;
    }

    public static string ToHex(uint argb) => $"#{argb:X8}";
}
