namespace DesktopFences.Core.Process;

public static class CommandLinePath
{
    public static string Quote(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            return path;
        return $"\"{path}\"";
    }
}
