using System.IO;
using System.Text;
using System.Windows;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using IDataObject = System.Windows.IDataObject;
using DataFormats = System.Windows.DataFormats;

namespace DesktopFences.App.Services;

/// <summary>
/// O Explorer (e o desktop) quase nunca manda só CF_HDROP + Move.
/// Lê FileDrop, FileNameW e texto-caminho; aceita Copy/Move/Link.
/// </summary>
internal static class ShellFileDrop
{
    /// <summary>
    /// No DragOver o Explorer atrasa o CF_HDROP: GetDataPresent costuma ser false
    /// e virar cursor proibido. Aceita Copy/Move/Link sem ler o payload.
    /// </summary>
    public static DragDropEffects AcceptWhileDragging(DragEventArgs e)
    {
        const DragDropEffects wanted = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;
        DragDropEffects offered = e.AllowedEffects;
        if (offered == DragDropEffects.None)
            return DragDropEffects.Copy;

        DragDropEffects chosen = offered & wanted;
        return chosen == DragDropEffects.None ? DragDropEffects.Copy : chosen;
    }

    public static bool CanDrop(IDataObject data)
    {
        try
        {
            return ExtractPaths(data).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public static DragDropEffects AllowedEffect(DragEventArgs e)
    {
        const DragDropEffects wanted = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;
        DragDropEffects offered = e.AllowedEffects == DragDropEffects.None
            ? wanted
            : e.AllowedEffects;
        DragDropEffects chosen = offered & wanted;
        return chosen == DragDropEffects.None ? DragDropEffects.None : chosen;
    }

    public static IReadOnlyList<string> ExtractPaths(IDataObject data)
    {
        var paths = new List<string>();

        TryAdd(data, DataFormats.FileDrop, paths);
        TryAdd(data, "FileDrop", paths);
        TryAdd(data, "FileNameW", paths);
        TryAdd(data, "FileName", paths);
        TryAdd(data, DataFormats.UnicodeText, paths);
        TryAdd(data, DataFormats.Text, paths);

        return paths
            .Select(Normalize)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void TryAdd(IDataObject data, string format, List<string> paths)
    {
        try
        {
            if (!data.GetDataPresent(format, true))
                return;

            object? raw = data.GetData(format, true);
            switch (raw)
            {
                case string[] files:
                    paths.AddRange(files);
                    break;
                case string text:
                    AddLines(text, paths);
                    break;
                case MemoryStream stream:
                    AddLines(ReadStream(stream), paths);
                    break;
            }
        }
        catch
        {
            // formato presente mas incompatível — ignorar
        }
    }

    private static void AddLines(string text, List<string> paths)
    {
        foreach (string line in text.Split(['\r', '\n', '\0'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim().Trim('"');
            if (trimmed.Length > 0)
                paths.Add(trimmed);
        }
    }

    private static string ReadStream(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string Normalize(string path)
    {
        string trimmed = path.Trim().Trim('"');
        if (trimmed.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            try { return new Uri(trimmed).LocalPath; }
            catch { return trimmed; }
        }

        return trimmed;
    }
}
