namespace Nova.Desktop;

internal static class PackagedAssetPaths
{
    public static bool RunningFromBuildOutput
    {
        get
        {
            var path = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string? DevelopmentPackageRoot
    {
        get
        {
            if (!RunningFromBuildOutput)
                return null;
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var current = new DirectoryInfo(start);
                while (current is not null)
                {
                    var package = Path.Combine(current.FullName, "dist");
                    if (Directory.Exists(Path.Combine(package, "models"))
                        || Directory.Exists(Path.Combine(package, "llama-runtimes")))
                        return package;
                    current = current.Parent;
                }
            }
            return null;
        }
    }
}
