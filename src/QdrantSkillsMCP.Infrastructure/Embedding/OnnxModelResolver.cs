using Microsoft.Extensions.Logging;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Resolves ONNX model and vocabulary file paths using a three-tier strategy:
/// 1. Explicit config path (OnnxModelPath)
/// 2. Companion NuGet package location
/// 3. Auto-download from HuggingFace
/// </summary>
public static class OnnxModelResolver
{
    private const string ModelFileName = "model.onnx";
    private const string VocabFileName = "vocab.txt";

    private const string HuggingFaceBaseUrl =
        "https://huggingface.co/onnx-models/all-MiniLM-L6-v2-onnx/resolve/main/";

    private static string CacheDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QdrantSkillsMCP",
            "models");

    /// <summary>
    /// Resolves the ONNX model file path using three-tier resolution.
    /// </summary>
    public static string ResolveModelPath(QdrantSkillsOptions options, ILogger logger)
        => ResolvePath(options, logger, ModelFileName, options.OnnxModelPath);

    /// <summary>
    /// Resolves the vocabulary file path using three-tier resolution.
    /// </summary>
    public static string ResolveVocabPath(QdrantSkillsOptions options, ILogger logger)
        => ResolvePath(options, logger, VocabFileName, explicitPath: null);

    private static string ResolvePath(
        QdrantSkillsOptions options,
        ILogger logger,
        string fileName,
        string? explicitPath)
    {
        // Tier 1: Explicit config path
        if (!string.IsNullOrEmpty(explicitPath))
        {
            // For model path, user specifies the directory or full file path
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

        // Tier 2: Companion NuGet package location (scan AppContext.BaseDirectory)
        var nugetPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(nugetPath))
        {
            logger.LogInformation(
                "Using companion NuGet ONNX file: {Path}", nugetPath);
            return nugetPath;
        }

        // Tier 3: Auto-download from HuggingFace
        var cachedPath = Path.Combine(CacheDirectory, fileName);
        if (File.Exists(cachedPath))
        {
            logger.LogInformation("Using cached ONNX file: {Path}", cachedPath);
            return cachedPath;
        }

        if (options.DisableAutoDownload)
        {
            throw new InvalidOperationException(
                $"ONNX {fileName} not found. Set OnnxModelPath, install " +
                "QdrantSkillsMCP.Models.DefaultEmbedding NuGet package, or enable auto-download " +
                "by setting DisableAutoDownload to false.");
        }

        // Download synchronously at startup (this is a one-time operation)
        logger.LogWarning(
            "Auto-downloading ONNX {FileName} from HuggingFace to {CacheDir}...",
            fileName, CacheDirectory);

        DownloadFile(HuggingFaceBaseUrl + fileName, cachedPath);

        logger.LogInformation("Downloaded ONNX {FileName} to {Path}", fileName, cachedPath);
        return cachedPath;
    }

    private static void DownloadFile(string url, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        using var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var fileStream = File.Create(destination);
        stream.CopyTo(fileStream);
    }
}
