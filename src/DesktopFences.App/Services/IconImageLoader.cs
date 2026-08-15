using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopFences.Core;
using DesktopFences.Native;

namespace DesktopFences.App.Services;

internal static class IconImageLoader
{
    public static ImageSource? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            string? resolved = DesktopPaths.ResolveExisting(path) ?? path;
            byte[]? png = ShellFileIcon.ExtractPng(resolved);
            if (png is null || png.Length == 0)
                return null;

            var image = new BitmapImage();
            using var stream = new MemoryStream(png);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
