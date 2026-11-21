namespace Nova.Desktop;

internal sealed record ModelFileInfo(
    string FileName,
    string FullPath,
    string DirectoryPath,
    bool IsPrimaryDirectory)
{
    public string DisplayName =>
        IsPrimaryDirectory ? FileName : $"{FileName} — {DirectoryPath}";
}

internal sealed record ModelDiscoveryResult(
    IReadOnlyList<ModelFileInfo> Models,
    IReadOnlyList<string> Errors);

internal static class ModelDirectory
{
    public static string PathName { get; } = ResolvePath();

    public static ModelDiscoveryResult Discover(IEnumerable<string>? additionalDirectories = null)
    {
        var models = new List<ModelFileInfo>();
        var errors = new List<string>();
        var directories = GetSearchDirectories(additionalDirectories, errors);

        foreach (var (directory, isPrimary) in directories)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    if (!string.Equals(Path.GetExtension(file), ".gguf", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var fullPath = Path.GetFullPath(file);
                    models.Add(new ModelFileInfo(
                        Path.GetFileName(fullPath),
                        fullPath,
                        directory,
                        isPrimary));
                }
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or ArgumentException
                    or NotSupportedException or System.Security.SecurityException)
            {
                errors.Add($"{directory}: {error.Message}");
            }
        }

        var distinctModels = models
            .DistinctBy(model => model.FullPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(model => model.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ModelDiscoveryResult(distinctModels, errors);
    }

    public static string? GetModelPath(string modelFile, IEnumerable<string>? additionalDirectories = null)
    {
        if (string.IsNullOrWhiteSpace(modelFile))
            return null;

        if (Path.IsPathRooted(modelFile))
        {
            try
            {
                var fullPath = Path.GetFullPath(modelFile);
                if (!File.Exists(fullPath)
                    || !string.Equals(Path.GetExtension(fullPath), ".gguf", StringComparison.OrdinalIgnoreCase))
                    return null;

                foreach (var (directory, _) in GetSearchDirectories(additionalDirectories))
                {
                    var root = Path.GetFullPath(directory)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
                    if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        return fullPath;
                }
                return null;
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or ArgumentException
                    or NotSupportedException)
            {
                return null;
            }
        }

        if (Path.GetFileName(modelFile) != modelFile
            || !string.Equals(Path.GetExtension(modelFile), ".gguf", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var (directory, _) in GetSearchDirectories(additionalDirectories))
        {
            try
            {
                var candidate = Path.GetFullPath(Path.Combine(directory, modelFile));
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or ArgumentException
                    or NotSupportedException)
            {
            }
        }

        return null;
    }

    public static IReadOnlyList<string> NormalizeAdditionalDirectories(IEnumerable<string>? directories)
    {
        if (directories is null)
            return [];

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(PathName)
        };
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            var fullPath = Path.GetFullPath(directory.Trim());
            if (seen.Add(fullPath))
                normalized.Add(fullPath);
        }
        return normalized;
    }

    private static IReadOnlyList<(string Path, bool IsPrimary)> GetSearchDirectories(
        IEnumerable<string>? additionalDirectories,
        List<string>? errors = null)
    {
        var directories = new List<(string Path, bool IsPrimary)> { (PathName, true) };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(PathName)
        };
        if (additionalDirectories is null)
            return directories;

        foreach (var directory in additionalDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            try
            {
                var fullPath = Path.GetFullPath(directory.Trim());
                if (seen.Add(fullPath))
                    directories.Add((fullPath, false));
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or ArgumentException
                    or NotSupportedException)
            {
                errors?.Add($"{directory}: {error.Message}");
            }
        }

        return directories;
    }

    private static string ResolvePath()
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "models");
        var currentPath = Path.Combine(Directory.GetCurrentDirectory(), "models");
        if (PackagedAssetPaths.RunningFromBuildOutput && ContainsModel(currentPath))
            return currentPath;
        if (ContainsModel(executablePath))
            return executablePath;
        var developmentPackageModels = PackagedAssetPaths.DevelopmentPackageRoot is { } packageRoot
            ? Path.Combine(packageRoot, "models")
            : null;
        if (developmentPackageModels is not null && ContainsModel(developmentPackageModels))
            return developmentPackageModels;
        if (Directory.Exists(currentPath))
            return currentPath;
        if (Directory.Exists(executablePath))
            return executablePath;
        Directory.CreateDirectory(executablePath);
        return executablePath;
    }

    private static bool ContainsModel(string path) =>
        Directory.Exists(path) && Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
            .Any(file => string.Equals(Path.GetExtension(file), ".gguf", StringComparison.OrdinalIgnoreCase));
}
