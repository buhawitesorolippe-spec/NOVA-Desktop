namespace Nova.Desktop;

internal static class EnvironmentConfiguration
{
    public static void Load()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(AppContext.BaseDirectory, ".env")
        };
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            paths.Add(Path.Combine(directory.FullName, ".env"));

        foreach (var envPath in paths)
        {
            if (!File.Exists(envPath))
                continue;
            foreach (var line in File.ReadLines(envPath))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0 || line.TrimStart().StartsWith('#'))
                    continue;

                var name = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'')
                    value = value[1..^1];
                if (!string.IsNullOrWhiteSpace(name) && Environment.GetEnvironmentVariable(name) is null)
                    Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
