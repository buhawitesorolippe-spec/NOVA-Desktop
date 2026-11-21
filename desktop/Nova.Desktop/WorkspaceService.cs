namespace Nova.Desktop;

internal static class WorkspaceService
{
    public static string RootPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NOVA-Workspace");

    public static string WriteFile(string relativePath, string content)
    {
        var target = ValidateFilePath(relativePath);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".nova-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(
                       temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false)))
                writer.Write(content);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
        return target;
    }

    public static string ReadTextFile(string relativePath, int maximumBytes)
    {
        var path = ValidateFilePath(relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Die Zieldatei wurde im NOVA-Arbeitsordner nicht gefunden.", path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > maximumBytes)
            throw new InvalidDataException($"Die Zieldatei überschreitet das Limit von {maximumBytes / (1024 * 1024)} MB.");
        using var content = new MemoryStream((int)input.Length);
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (content.Length + read > maximumBytes)
                throw new InvalidDataException($"Die Zieldatei überschreitet das Limit von {maximumBytes / (1024 * 1024)} MB.");
            content.Write(buffer, 0, read);
        }
        return new System.Text.UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(content.GetBuffer(), 0, (int)content.Length);
    }

    public static string ValidateFilePath(string relativePath) => ResolvePath(relativePath);

    public static string ResolveProject(string relativePath)
    {
        var project = ResolvePath(relativePath);
        if (!project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Zum Kompilieren muss der relative Pfad auf eine .csproj-Datei zeigen.");
        if (!File.Exists(project))
            throw new FileNotFoundException("Die Projektdatei wurde im NOVA-Arbeitsordner nicht gefunden.", project);
        return project;
    }

    public static IReadOnlyList<string> ListFiles(string relativeDirectory)
    {
        Directory.CreateDirectory(RootPath);
        var directory = ResolveDirectory(relativeDirectory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Der Ordner wurde nicht gefunden: {relativeDirectory}");

        var root = Path.GetFullPath(RootPath) + Path.DirectorySeparatorChar;
        var pending = new Stack<string>();
        var results = new List<string>();
        pending.Push(directory);
        while (pending.Count > 0 && results.Count < 500)
        {
            var current = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
            {
                results.Add(Path.GetRelativePath(RootPath, file));
                if (results.Count == 500)
                    break;
            }
            foreach (var child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
            {
                var info = new DirectoryInfo(child);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                var fullPath = Path.GetFullPath(child);
                if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    pending.Push(fullPath);
            }
        }
        return results.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new InvalidDataException("Gib einen gültigen relativen Pfad innerhalb des NOVA-Arbeitsordners an.");

        var rootPath = Path.GetFullPath(RootPath);
        var root = rootPath + Path.DirectorySeparatorChar;
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment =>
                segment is "." or ".." || segment.Contains(':')
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException("Der relative Pfad enthält ungültige Ordner- oder Dateinamen.");

        var target = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!IsWithinRoot(root, target))
            throw new InvalidDataException("Der Pfad muss innerhalb des NOVA-Arbeitsordners bleiben.");
        var current = rootPath;
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
                throw new InvalidDataException("Der Pfad führt über einen Link aus dem NOVA-Arbeitsordner heraus.");
        }
        if (Directory.Exists(target))
            throw new InvalidDataException("Der Pfad bezeichnet einen Ordner, keine Datei.");
        return target;
    }

    private static string ResolveDirectory(string relativeDirectory)
    {
        var root = Path.GetFullPath(RootPath);
        if (string.IsNullOrWhiteSpace(relativeDirectory))
            return root;
        var segments = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(relativeDirectory)
            || segments.Length == 0
            || segments.Any(segment => segment is "." or ".." || segment.Contains(':')
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException("Gib einen gültigen relativen Ordner im NOVA-Arbeitsordner an.");

        var target = Path.GetFullPath(Path.Combine(root, relativeDirectory));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Der Ordner muss innerhalb des NOVA-Arbeitsordners liegen.");

        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
                continue;
            var entry = new DirectoryInfo(current);
            if (entry.LinkTarget is null)
                continue;
            var resolved = entry.ResolveLinkTarget(returnFinalTarget: true);
            if (resolved is null || !Path.GetFullPath(resolved.FullName)
                    .StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Der Ordner führt über einen Link aus dem NOVA-Arbeitsordner heraus.");
        }
        return target;
    }

    private static bool IsWithinRoot(string rootWithSeparator, string target) =>
        target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
}
