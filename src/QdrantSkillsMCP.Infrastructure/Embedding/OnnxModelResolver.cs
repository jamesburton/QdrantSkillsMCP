using Microsoft.Extensions.Logging;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Resolves ONNX model and vocabulary file paths using a four-tier strategy:
/// 1. Explicit config path (OnnxModelPath)
/// 2a. AppContext.BaseDirectory onnx-models/ subdirectory
/// 2b. NuGet global packages cache (companion QdrantSkillsMCP.Models.* package)
/// 3. Auto-download from HuggingFace (cached to LocalApplicationData)
/// </summary>
public static class OnnxModelResolver
{
    private const string DefaultModelName = "all-MiniLM-L6-v2";

    /// <summary>
    /// Maps model names to their HuggingFace download URLs and companion NuGet package ID.
    /// </summary>
    private static readonly Dictionary<string, ModelInfo> KnownModels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["all-MiniLM-L6-v2"] = new(
            ModelUrl: "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx",
            TokenizerUrl: "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/tokenizer.json",
            VocabUrl: "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt",
            Dimensions: 384,
            NuGetPackageId: "qdrantskillsmcp.models.minilm"),
        ["bge-small-en-v1.5"] = new(
            ModelUrl: "https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/onnx/model_quantized.onnx",
            TokenizerUrl: "https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/tokenizer.json",
            VocabUrl: "https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/vocab.txt",
            Dimensions: 384,
            NuGetPackageId: "qdrantskillsmcp.models.bgesmall"),
        ["bge-base-en-v1.5"] = new(
            ModelUrl: "https://huggingface.co/Xenova/bge-base-en-v1.5/resolve/main/onnx/model_quantized.onnx",
            TokenizerUrl: "https://huggingface.co/Xenova/bge-base-en-v1.5/resolve/main/tokenizer.json",
            VocabUrl: "https://huggingface.co/BAAI/bge-base-en-v1.5/resolve/main/vocab.txt",
            Dimensions: 768,
            NuGetPackageId: "qdrantskillsmcp.models.bgebase"),
    };

    private static string CacheDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QdrantSkillsMCP",
            "models");

    /// <summary>
    /// Gets the vector dimensions for the configured ONNX model name.
    /// Returns 384 for unknown models (safe default).
    /// </summary>
    public static int GetModelDimensions(string? modelName)
    {
        var name = modelName ?? DefaultModelName;
        return KnownModels.TryGetValue(name, out var info) ? info.Dimensions : 384;
    }

    /// <summary>
    /// Resolves the ONNX model file path using three-tier resolution.
    /// </summary>
    public static string ResolveModelPath(QdrantSkillsOptions options, ILogger logger)
        => ResolvePath(options, logger, "model_quantized.onnx", options.OnnxModelPath,
            m => m.ModelUrl);

    /// <summary>
    /// Resolves the vocabulary file path using three-tier resolution.
    /// </summary>
    public static string ResolveVocabPath(QdrantSkillsOptions options, ILogger logger)
        => ResolvePath(options, logger, "vocab.txt", explicitPath: null,
            m => m.VocabUrl);

    private static string ResolvePath(
        QdrantSkillsOptions options,
        ILogger logger,
        string fileName,
        string? explicitPath,
        Func<ModelInfo, string> urlSelector)
    {
        var modelName = options.OnnxModelName ?? DefaultModelName;

        // Tier 1: Explicit config path
        if (!string.IsNullOrEmpty(explicitPath))
        {
            var candidatePath = File.Exists(explicitPath)
                ? explicitPath
                : Path.Combine(explicitPath, fileName);

            if (File.Exists(candidatePath))
            {
                logger.LogInformation("Using custom ONNX model path: {Path}", candidatePath);
                return candidatePath;
            }

            logger.LogWarning(
                "Configured ONNX path '{Path}' not found, falling back to discovery.",
                explicitPath);
        }

        // Tier 2a: Companion NuGet package location (AppContext.BaseDirectory/onnx-models/{model-name}/)
        var nugetPath = Path.Combine(AppContext.BaseDirectory, "onnx-models", modelName, fileName);
        if (File.Exists(nugetPath))
        {
            logger.LogInformation("Using companion ONNX file: {Path}", nugetPath);
            return nugetPath;
        }

        // Also check flat layout (legacy: files directly in BaseDirectory)
        var flatPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(flatPath))
        {
            logger.LogInformation("Using companion ONNX file (flat): {Path}", flatPath);
            return flatPath;
        }

        // Tier 2b: NuGet global packages cache (companion QdrantSkillsMCP.Models.* package)
        if (KnownModels.TryGetValue(modelName, out var modelInfoForCache))
        {
            var nugetCachePath = FindInNuGetCache(modelInfoForCache.NuGetPackageId, modelName, fileName);
            if (nugetCachePath is not null)
            {
                logger.LogInformation("Using NuGet cache ONNX file: {Path}", nugetCachePath);
                return nugetCachePath;
            }
        }

        // Tier 3: Auto-download from HuggingFace
        var cachedPath = Path.Combine(CacheDirectory, modelName, fileName);
        if (File.Exists(cachedPath))
        {
            logger.LogInformation("Using cached ONNX file: {Path}", cachedPath);
            return cachedPath;
        }

        if (options.DisableAutoDownload)
        {
            throw new InvalidOperationException(
                $"ONNX {fileName} not found for model '{modelName}'. Install the corresponding " +
                $"QdrantSkillsMCP.Models.* NuGet package, set OnnxModelPath, or enable auto-download " +
                "by setting DisableAutoDownload to false.");
        }

        if (!KnownModels.TryGetValue(modelName, out var modelInfo))
        {
            throw new InvalidOperationException(
                $"Unknown ONNX model '{modelName}'. Known models: {string.Join(", ", KnownModels.Keys)}. " +
                "Set OnnxModelPath to use a custom model.");
        }

        var downloadUrl = urlSelector(modelInfo);
        logger.LogWarning(
            "Auto-downloading ONNX {FileName} for {ModelName} from HuggingFace to {CacheDir}...",
            fileName, modelName, CacheDirectory);

        DownloadFile(downloadUrl, cachedPath);

        logger.LogInformation("Downloaded ONNX {FileName} to {Path}", fileName, cachedPath);
        return cachedPath;
    }

    /// <summary>
    /// Scans the NuGet global packages cache for a companion model package.
    /// The package stores model files under contentFiles/any/any/onnx-models/{modelName}/.
    /// </summary>
    private static string? FindInNuGetCache(string packageId, string modelName, string fileName)
    {
        // Resolve NuGet global packages folder (respects NUGET_PACKAGES env var)
        var nugetPackagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages");

        var packageDir = Path.Combine(nugetPackagesRoot, packageId);
        if (!Directory.Exists(packageDir))
            return null;

        // Find latest version directory (lexicographic sort is good enough for semver)
        var versionDirs = Directory.GetDirectories(packageDir);
        if (versionDirs.Length == 0)
            return null;

        Array.Sort(versionDirs);
        var latestVersionDir = versionDirs[^1];

        // contentFiles layout (SDK-style)
        var contentFilesPath = Path.Combine(
            latestVersionDir, "contentfiles", "any", "any", "onnx-models", modelName, fileName);
        if (File.Exists(contentFilesPath))
            return contentFilesPath;

        // content layout (packages.config-style, also available in cache)
        var contentPath = Path.Combine(latestVersionDir, "content", "onnx-models", modelName, fileName);
        if (File.Exists(contentPath))
            return contentPath;

        return null;
    }

    private static void DownloadFile(string url, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var tmp = destination + ".tmp";
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            using var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using (var fileStream = File.Create(tmp))
                stream.CopyTo(fileStream);

            File.Move(tmp, destination, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
            throw;
        }
    }

    private sealed record ModelInfo(
        string ModelUrl,
        string TokenizerUrl,
        string VocabUrl,
        int Dimensions,
        string NuGetPackageId);
}
