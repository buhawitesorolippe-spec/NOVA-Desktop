using System.IO.Compression;

namespace Nova.Desktop;

internal sealed record LlamaRuntime(string Kind, string Name, string ExecutablePath)
{
    public string DisplayName => $"{Kind} · {Name}";
}

internal static class LlamaRuntimeStore
{
    private const string PackagedVulkanRuntimeName = "llama-b11601-bin-win-vulkan-x64";

    public static string RootPath => Path.Combine(AppContext.BaseDirectory, "llama-runtimes");

    public static void EnsureDirectories()
    {
        foreach (var kind in new[] { "CPU", "CUDA", "Vulkan" })
            Directory.CreateDirectory(Path.Combine(RootPath, kind));
    }

    public static IReadOnlyList<LlamaRuntime> ImportArchives()
    {
        EnsureDirectories();
        var imported = new List<LlamaRuntime>();
        foreach (var kind in new[] { "CPU", "CUDA", "Vulkan" })
        {
            var directory = Path.Combine(RootPath, kind);
            foreach (var archivePath in Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly))
            {
                var destination = Path.Combine(directory, Path.GetFileNameWithoutExtension(archivePath));
                if (Directory.Exists(destination))
                    continue;

                ExtractArchive(archivePath, destination);
                imported.Add(new LlamaRuntime(kind, Path.GetFileNameWithoutExtension(archivePath), ""));
            }
        }
        return imported;
    }

    public static IReadOnlyList<LlamaRuntime> GetRuntimes()
    {
        EnsureDirectories();
        var runtimes = new List<LlamaRuntime>();
        var roots = new List<string> { RootPath };
        if (PackagedAssetPaths.DevelopmentPackageRoot is { } packageRoot)
            roots.Add(Path.Combine(packageRoot, "llama-runtimes"));
        foreach (var root in roots)
        {
            foreach (var kind in new[] { "CPU", "CUDA", "Vulkan" })
            {
                var kindDirectory = Path.Combine(root, kind);
                if (!Directory.Exists(kindDirectory))
                    continue;
                foreach (var executable in Directory.EnumerateFiles(
                             kindDirectory,
                             "llama-server.exe",
                             new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                {
                    var versionName = Path.GetRelativePath(kindDirectory, Path.GetDirectoryName(executable)!);
                    if (versionName == ".")
                        versionName = Path.GetFileName(executable);
                    runtimes.Add(new LlamaRuntime(kind, versionName, Path.GetFullPath(executable)));
                }
            }
        }
        return runtimes.DistinctBy(runtime => runtime.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(runtime => runtime.Kind).ThenBy(runtime => runtime.Name).ToArray();
    }

    public static LlamaRuntime? GetPackagedVulkanRuntimeBeside(LlamaRuntime cudaRuntime)
    {
        if (!string.Equals(cudaRuntime.Kind, "CUDA", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var executable = Path.GetFullPath(cudaRuntime.ExecutablePath);
            var versionDirectory = Path.GetDirectoryName(executable);
            var cudaDirectory = versionDirectory is null
                ? null
                : Directory.GetParent(versionDirectory)?.FullName;
            if (cudaDirectory is null
                || !string.Equals(Path.GetFileName(cudaDirectory), "CUDA", StringComparison.OrdinalIgnoreCase))
                return null;

            var runtimeRoot = Directory.GetParent(cudaDirectory)?.FullName;
            if (runtimeRoot is null)
                return null;

            var packagedVulkanPath = Path.GetFullPath(Path.Combine(
                runtimeRoot, "Vulkan", PackagedVulkanRuntimeName, "llama-server.exe"));
            return GetRuntimes().FirstOrDefault(runtime =>
                string.Equals(runtime.Name, PackagedVulkanRuntimeName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(runtime.Kind, "Vulkan", StringComparison.OrdinalIgnoreCase)
                && string.Equals(runtime.ExecutablePath, packagedVulkanPath, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static void ExtractArchive(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);
        try
        {
            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > 100_000)
                throw new InvalidDataException("Das ZIP-Archiv enthält zu viele Einträge.");

            long expandedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > 12L * 1024 * 1024 * 1024)
                    throw new InvalidDataException("Das ZIP-Archiv überschreitet das NOVA-Limit von 12 GB entpackten Dateien.");

                var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Das ZIP-Archiv enthält einen ungültigen Pfad.");
                if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                    throw new InvalidDataException("Symbolische Links in Runtime-Archiven werden nicht unterstützt.");

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var source = entry.Open();
                using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(output);
            }
        }
        catch (Exception extractionError) when (
            extractionError is IOException or UnauthorizedAccessException or OverflowException or ArgumentException)
        {
            try
            {
                Directory.Delete(destination, recursive: true);
            }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"ZIP-Import fehlgeschlagen ({extractionError.Message}); unvollständiger Zielordner konnte nicht entfernt werden ({cleanupError.Message}).",
                    new AggregateException(extractionError, cleanupError));
            }
            throw;
        }
    }
}
