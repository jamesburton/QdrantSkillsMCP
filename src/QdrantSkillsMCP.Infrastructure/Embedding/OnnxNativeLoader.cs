using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Ensures the ONNX native runtime is available before ONNX managed code runs.
/// On first use, installs a DllImportResolver that downloads the platform-specific
/// native from NuGet if not already present, then caches it in
/// %LOCALAPPDATA%/QdrantSkillsMCP/onnx-runtime/{version}/.
/// </summary>
internal static class OnnxNativeLoader
{
    private static bool _installed;
    private static readonly object _lock = new();
    private static string? _resolvedPath;

    /// <summary>
    /// Installs the DllImportResolver on the OnnxRuntime managed assembly.
    /// Must be called before any OnnxRuntime managed types are instantiated.
    /// Safe to call multiple times — installs only once.
    /// </summary>
    public static void EnsureLoaded(ILogger logger)
    {
        lock (_lock)
        {
            if (_installed) return;
            _installed = true;
        }

        NativeLibrary.SetDllImportResolver(
            typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly,
            (libraryName, _, _) =>
            {
                // OnnxRuntime uses "onnxruntime" as the DllImport name on all platforms.
                if (!libraryName.Equals("onnxruntime", StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                // Already resolved in this process — return the same handle.
                if (_resolvedPath is not null && NativeLibrary.TryLoad(_resolvedPath, out var cached))
                    return cached;

                // System-level install (e.g. user has Microsoft.ML.OnnxRuntime installed globally).
                if (NativeLibrary.TryLoad(libraryName, out var system))
                    return system;

                // Our local download cache.
                var cachePath = GetCachePath();
                if (File.Exists(cachePath) && NativeLibrary.TryLoad(cachePath, out var fromCache))
                {
                    _resolvedPath = cachePath;
                    return fromCache;
                }

                // Download from NuGet, then load.
                try
                {
                    Task.Run(() => DownloadAsync(logger, cachePath)).GetAwaiter().GetResult();

                    if (File.Exists(cachePath) && NativeLibrary.TryLoad(cachePath, out var downloaded))
                    {
                        _resolvedPath = cachePath;
                        return downloaded;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to download ONNX native runtime. " +
                        "Set EmbeddingProvider=OpenAI or EmbeddingProvider=Ollama to avoid ONNX: " +
                        "qdrant-skills-mcp --config set EmbeddingProvider=OpenAI");
                }

                return IntPtr.Zero;
            });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetCachePath()
    {
        var version = GetManagedVersion();
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QdrantSkillsMCP", "onnx-runtime", version);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, GetNativeFileName());
    }

    private static string GetManagedVersion()
    {
        var v = typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly.GetName().Version!;
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private static string GetNativeFileName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "onnxruntime.dll"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "libonnxruntime.so"
        : "libonnxruntime.dylib";

    private static string GetRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64   => "x64",
            Architecture.X86   => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm   => "arm",
            _                  => "x64"
        };
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"win-{arch}"
             : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)   ? $"linux-{arch}"
             : $"osx-{arch}";
    }

    private static async Task DownloadAsync(ILogger logger, string destPath)
    {
        var version  = GetManagedVersion();
        var rid      = GetRid();
        var fileName = GetNativeFileName();
        const string packageId = "microsoft.ml.onnxruntime";
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId}/{version}/{packageId}.{version}.nupkg";

        logger.LogInformation(
            "Downloading ONNX native runtime ({Version}, {Rid}) from NuGet — one-time download, cached locally...",
            version, rid);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var entryPath = $"runtimes/{rid}/native/{fileName}";
        var entry = zip.GetEntry(entryPath)
            ?? throw new InvalidOperationException(
                $"'{entryPath}' not found in NuGet package. " +
                $"RID '{rid}' may be unsupported by OnnxRuntime {version}.");

        var tmp = destPath + ".tmp";
        await using (var src  = entry.Open())
        await using (var dest = File.Create(tmp))
            await src.CopyToAsync(dest);

        File.Move(tmp, destPath, overwrite: true);

        logger.LogInformation("ONNX native runtime cached at {Path}", destPath);
    }
}
