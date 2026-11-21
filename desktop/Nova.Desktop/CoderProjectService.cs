using System.Security.Cryptography;
using System.Text;

namespace Nova.Desktop;

internal sealed record ProjectFileSnapshot(bool Exists, string? Content, string? Sha256);

internal static class CoderProjectService
{
    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".yaml", ".yml", ".html", ".css",
        ".js", ".ts", ".tsx", ".jsx", ".py", ".cs", ".csproj", ".sln", ".slnx",
        ".java", ".kt", ".go", ".rs", ".cpp", ".c", ".h", ".hpp", ".sql", ".log",
        ".ini", ".toml", ".ps1", ".sh", ".bat", ".properties", ".cfg", ".conf",
        ".xaml", ".gradle", ".kt", ".swift", ".dart", ".vue", ".svelte", ".php"
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", ".vscode", "bin", "obj", "node_modules", "packages",
        ".venv", "venv", "__pycache__", "dist", "build", "target"
    };

    public static string ValidateRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathRooted(rootPath))
            throw new InvalidDataException("Wähle einen absoluten Projektordner aus.");

        var fullPath = Path.GetFullPath(rootPath.Trim());
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Der Projektordner wurde nicht gefunden: {fullPath}");
        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    public static IReadOnlyList<string> ListTextFiles(string rootPath, int maximumFiles)
    {
        var root = ValidateRoot(rootPath);
        var pending = new Stack<string>();
        var results = new List<string>(Math.Min(maximumFiles, 100));
        pending.Push(root);

        while (pending.Count > 0 && results.Count < maximumFiles)
        {
            var current = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0
                    || !SourceExtensions.Contains(info.Extension))
                    continue;
                results.Add(Path.GetRelativePath(root, file));
                if (results.Count == maximumFiles)
                    break;
            }

            foreach (var directory in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
            {
                var info = new DirectoryInfo(directory);
                if (IgnoredDirectories.Contains(info.Name)
                    || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                pending.Push(directory);
            }
        }

        return results.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ProjectFileSnapshot ReadSnapshot(string rootPath, string relativePath, int maximumBytes)
    {
        var path = ResolvePath(rootPath, relativePath);
        if (!File.Exists(path))
            return new ProjectFileSnapshot(false, null, null);

        var info = new FileInfo(path);
        if (info.Length > maximumBytes)
            throw new InvalidDataException(
                $"„{relativePath}“ überschreitet das NOVA-Limit von {maximumBytes / (1024 * 1024)} MB.");

        var bytes = File.ReadAllBytes(path);
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes);
        return new ProjectFileSnapshot(true, content, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public static string WriteFile(string rootPath, string relativePath, string content)
    {
        var target = ResolvePath(rootPath, relativePath);
        var directory = Path.GetDirectoryName(target)
            ?? throw new InvalidDataException("Der Zieldateipfad enthält keinen Ordner.");
        Directory.CreateDirectory(directory);
        target = ResolvePath(rootPath, relativePath);
        var temporary = Path.Combine(directory, $".nova-coder-{Guid.NewGuid():N}.tmp");
        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            try
            {
                using var output = new FileStream(
                    temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }

            File.Move(temporary, target, overwrite: true);
            return target;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static bool IsSupportedSourceFile(string relativePath) =>
        SourceExtensions.Contains(Path.GetExtension(relativePath));

    private static string ResolvePath(string rootPath, string relativePath)
    {
        var root = ValidateRoot(rootPath);
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new InvalidDataException("Gib einen relativen Dateipfad innerhalb des Projektordners an.");

        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment =>
                segment is "." or ".." || segment.Contains(':')
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException("Der Dateipfad enthält ungültige Ordner- oder Dateinamen.");

        var target = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!IsWithinRoot(root, target))
            throw new InvalidDataException("Der Pfad muss innerhalb des ausgewählten Projektordners bleiben.");

        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            FileSystemInfo? entry = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : File.Exists(current) ? new FileInfo(current) : null;
            if (entry?.LinkTarget is null)
                continue;
            var resolved = entry.ResolveLinkTarget(returnFinalTarget: true);
            if (resolved is null || !IsWithinRoot(root, Path.GetFullPath(resolved.FullName)))
                throw new InvalidDataException("Der Pfad führt über einen Link aus dem Projektordner heraus.");
        }

        if (Directory.Exists(target))
            throw new InvalidDataException("Der Pfad bezeichnet einen Ordner, keine Datei.");
        return target;
    }

    private static bool IsWithinRoot(string rootPath, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
