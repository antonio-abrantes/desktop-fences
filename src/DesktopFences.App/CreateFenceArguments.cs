namespace DesktopFences.App;

internal sealed record CreateFenceArguments(string? StubPath)
{
    public static bool TryParse(IReadOnlyList<string> args, out CreateFenceArguments? result)
    {
        result = null;
        if (args is null)
            return false;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg.StartsWith("--create-fence=", StringComparison.OrdinalIgnoreCase))
            {
                string path = arg[(arg.IndexOf('=') + 1)..].Trim().Trim('"');
                result = new CreateFenceArguments(string.IsNullOrWhiteSpace(path) ? null : path);
                return true;
            }

            if (!arg.Equals("--create-fence", StringComparison.OrdinalIgnoreCase))
                continue;

            string? next = i + 1 < args.Count ? args[i + 1] : null;
            if (next is not null && !next.StartsWith("--", StringComparison.Ordinal))
            {
                string path = next.Trim().Trim('"');
                result = new CreateFenceArguments(string.IsNullOrWhiteSpace(path) ? null : path);
            }
            else
            {
                result = new CreateFenceArguments(StubPath: null);
            }

            return true;
        }

        return false;
    }
}
