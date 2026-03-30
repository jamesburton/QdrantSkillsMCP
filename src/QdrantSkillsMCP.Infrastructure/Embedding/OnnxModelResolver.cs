using Microsoft.Extensions.Logging;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Resolves ONNX model and vocabulary file paths using a three-tier strategy:
/// 1. Explicit config path (OnnxModelPath)
/// 2. Companion NuGet package location (onnx-models/{model-name}/)
/// 3. Auto-download from HuggingFace
/// </summary>
public static class OnnxModelResolver
{
    private const string DefaultModelName = "all-MiniLM-L6-v2";

    /// <summary>
    /// Maps model names to their HuggingFace download URLs.
    /// Keys: model file name, tokenizer file name, vocab file name.
    /// </summary>
    private static readonly Dictionary<string, ModelInfo> KnownModels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["all-MiniLM-L6-v2"] = new(
            ModelUrl: "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx",
            TokenizerUrl: "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/tokenizer.json",
            VocabUrl: "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt",
            Dimensions: 384),
        ["bge-small-en-v1.5"] = new(
            ModelUrl: "https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/onnx/model_quantized.onnx",
            TokenizerUrl: "https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/tokenizer.json",
            VocabUrl: "https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/vocab.txt",
            Dimensions: 384),
        ["bge-base-en-v1.5"] = new(
            ModelUrl: "https://huggingface.co/Xenova/bge-base-en-v1.5/resolve/main/onnx/model_quantized.onnx",
            TokenizerUrl: "https://huggingface.co/Xenova/bge-base-en-v1.5/resolve/main/tokenizer.json",
            VocabUrl: "https://huggingface.co/BAAI/bge-base-en-v1.5/resolve/main/vocab.txt",
            Dimensions: 768),
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

        // Tier 2: Companion NuGet package location (content files land in onnx-models/{model-name}/)
        var nugetPath = Path.Combine(AppContext.BaseDirectory, "onnx-models", modelName, fileName);
        if (File.Exists(nugetPath))
        {
            logger.LogInformation(
                "Using companion NuGet ONNX file: {Path}", nugetPath);
            return nugetPath;
        }

        // Also check flat layout (legacy: files directly in BaseDirectory)
        var flatPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(flatPath))
        {
            logger.LogInformation(
                "Using companion NuGet ONNX file (flat): {Path}", flatPath);
            return flatPath;
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
        int Dimensions);
}
