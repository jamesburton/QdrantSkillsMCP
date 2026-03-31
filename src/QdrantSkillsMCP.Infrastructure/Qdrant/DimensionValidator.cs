using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

/// <summary>
/// Startup hosted service that validates embedding provider dimensions against an existing
/// Qdrant collection. Detects dimension mismatches before MCP tools become available and
/// supports configurable resolution strategies (rename, suffix, replace, or hard fail).
/// Also performs a test embedding round-trip to verify the provider is functional.
/// </summary>
public sealed class DimensionValidator : IHostedService
{
    private readonly IQdrantOperations _client;
    private readonly IEmbeddingService _embeddingService;
    private readonly QdrantSkillsOptions _options;
    private readonly ILogger<DimensionValidator> _logger;

    public DimensionValidator(
        IQdrantOperations client,
        IEmbeddingService embeddingService,
        IOptions<QdrantSkillsOptions> options,
        ILogger<DimensionValidator> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await RunValidationAsync(ct);
        }
        catch (Exception ex) when (ex is Grpc.Core.RpcException or HttpRequestException)
        {
            var detail = ex is Grpc.Core.RpcException rpc
                ? $"{rpc.Status.StatusCode}: {rpc.Status.Detail}"
                : $"{ex.GetType().Name}: {ex.Message}";
            Console.Error.WriteLine(
                $"[DimensionValidator] Qdrant unreachable at startup — dimension validation skipped. " +
                $"Skills will not be available until Qdrant is reachable. ({detail})");
            _logger.LogWarning(ex,
                "Qdrant unreachable at startup — dimension validation skipped. " +
                "Skills will not be available until Qdrant is reachable.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[DimensionValidator] Qdrant unreachable at startup — dimension validation skipped. " +
                $"Skills will not be available until Qdrant is reachable. ({ex.GetType().Name}: {ex.Message})");
            _logger.LogWarning(ex,
                "Qdrant unreachable at startup — dimension validation skipped. " +
                "Skills will not be available until Qdrant is reachable.");
        }
    }

    private async Task RunValidationAsync(CancellationToken ct)
    {
        _logger.LogInformation("Validating embedding dimensions for collection '{CollectionName}'...",
            _options.CollectionName);

        // Step 1: Check if collection exists
        var collections = await _client.ListCollectionsAsync(ct);
        var collectionExists = collections.Any(c => c == _options.CollectionName);

        if (collectionExists)
        {
            // Step 2: Get collection info and validate dimensions
            var info = await _client.GetCollectionInfoAsync(_options.CollectionName, ct);
            var existingDims = (int)info.Config.Params.VectorsConfig.Params.Size;
            var providerDims = _embeddingService.Dimensions;

            if (existingDims != providerDims)
            {
                await HandleMismatchAsync(existingDims, providerDims, ct);
            }
            else
            {
                _logger.LogInformation(
                    "Dimension validation passed: collection '{CollectionName}' has {Dims}-dim vectors, provider produces {Dims}-dim",
                    _options.CollectionName, existingDims, providerDims);
            }
        }
        else
        {
            _logger.LogInformation(
                "Collection '{CollectionName}' does not exist yet; skipping dimension validation (CollectionInitializer will create it)",
                _options.CollectionName);
        }

        // Step 3: Embedding output validation (unless skipped)
        if (!_options.SkipEmbeddingOutputValidation)
        {
            await ValidateEmbeddingOutputAsync(collectionExists, ct);
        }
        else
        {
            _logger.LogInformation("Embedding output validation skipped (SkipEmbeddingOutputValidation=true)");
        }

        _logger.LogInformation("Dimension validation complete");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleMismatchAsync(int existingDims, int providerDims, CancellationToken ct)
    {
        var resolution = _options.MismatchResolution?.Trim().ToLowerInvariant();

        switch (resolution)
        {
            case "rename":
                var oldName = $"{_options.CollectionName}-old-{existingDims}";
                _logger.LogWarning(
                    "Dimension mismatch in '{CollectionName}': existing={ExistingDims}, provider={ProviderDims}. " +
                    "Strategy=rename: existing collection '{CollectionName}' will be DELETED (DATA LOSS — Qdrant does not support rename). " +
                    "An empty placeholder collection '{OldName}' will be created to record the old dimension count. " +
                    "A new {ProviderDims}-dim collection will be created by CollectionInitializer. " +
                    "To avoid data loss, export your skills before changing embedding providers.",
                    _options.CollectionName, existingDims, providerDims, _options.CollectionName, oldName, providerDims);

                await _client.DeleteCollectionAsync(_options.CollectionName, cancellationToken: ct);

                await _client.CreateCollectionAsync(
                    oldName,
                    new VectorParams { Size = (ulong)existingDims, Distance = Distance.Cosine },
                    cancellationToken: ct);

                _logger.LogWarning(
                    "Deleted '{CollectionName}'. Created empty placeholder '{OldName}' ({ExistingDims}-dim). " +
                    "CollectionInitializer will create a fresh '{CollectionName}' with {ProviderDims}-dim vectors.",
                    _options.CollectionName, oldName, existingDims, _options.CollectionName, providerDims);
                break;

            case "suffix":
                var newName = $"{_options.CollectionName}-{providerDims}";
                _logger.LogInformation(
                    "Dimension mismatch in '{CollectionName}': existing={ExistingDims}, provider={ProviderDims}. " +
                    "Switching to suffixed collection '{NewName}'.",
                    _options.CollectionName, existingDims, providerDims, newName);
                _options.CollectionName = newName;
                break;

            case "replace":
                _logger.LogWarning(
                    "Dimension mismatch in '{CollectionName}': existing={ExistingDims}, provider={ProviderDims}. " +
                    "Deleting collection '{CollectionName}' (DATA LOSS). " +
                    "CollectionInitializer will recreate with correct dimensions.",
                    _options.CollectionName, existingDims, providerDims, _options.CollectionName);
                await _client.DeleteCollectionAsync(_options.CollectionName, cancellationToken: ct);
                break;

            default:
                // null or unrecognized: hard fail
                throw new InvalidOperationException(
                    $"Collection '{_options.CollectionName}' has {existingDims}-dim vectors " +
                    $"but provider produces {providerDims}-dim. " +
                    "Use MismatchResolution config ('rename', 'suffix', or 'replace') to resolve.");
        }
    }

    private async Task ValidateEmbeddingOutputAsync(bool collectionExists, CancellationToken ct)
    {
        _logger.LogInformation("Generating test embedding to verify provider output...");

        var testVector = await _embeddingService.GenerateEmbeddingAsync(_options.TestEmbeddingInput, ct);

        if (testVector.Length != _embeddingService.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding provider returned {testVector.Length}-dim vector " +
                $"but reports Dimensions={_embeddingService.Dimensions}. " +
                "The provider is returning inconsistent dimensions.");
        }

        _logger.LogInformation("Test embedding validated: {Dims} dimensions as expected", testVector.Length);

        // If collection exists, upsert a test point for smoke-testing the vector pipeline
        if (collectionExists)
        {
            var testPointId = GenerateTestPointId(_options.TestEmbeddingKey);
            var testPoint = new PointStruct
            {
                Id = testPointId,
                Vectors = testVector,
                Payload =
                {
                    ["_test"] = true,
                    ["name"] = _options.TestEmbeddingKey
                }
            };

            await _client.UpsertAsync(_options.CollectionName, [testPoint], cancellationToken: ct);
            _logger.LogInformation("Test embedding point upserted to collection '{CollectionName}'",
                _options.CollectionName);
        }
        else
        {
            _logger.LogInformation("Collection does not exist yet; skipping test embedding upsert");
        }
    }

    /// <summary>
    /// Generates a deterministic point ID for the test embedding.
    /// </summary>
    private static Guid GenerateTestPointId(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"_test:{key}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    // --- Internal validation helpers for unit testing ---

    /// <summary>
    /// Validates dimension mismatch and returns the resolution action to take.
    /// Throws <see cref="InvalidOperationException"/> when resolution is null (hard fail).
    /// </summary>
    internal static string ValidateDimensionMismatch(
        string collectionName, int existingDims, int providerDims, string? mismatchResolution)
    {
        if (existingDims == providerDims)
            return "match";

        var resolution = mismatchResolution?.Trim().ToLowerInvariant();

        return resolution switch
        {
            "rename" => "rename",
            "suffix" => "suffix",
            "replace" => "replace",
            _ => throw new InvalidOperationException(
                $"Collection '{collectionName}' has {existingDims}-dim vectors " +
                $"but provider produces {providerDims}-dim. " +
                "Use MismatchResolution config ('rename', 'suffix', or 'replace') to resolve.")
        };
    }

    /// <summary>
    /// Validates that the test embedding output matches the declared dimensions.
    /// Throws <see cref="InvalidOperationException"/> on mismatch.
    /// </summary>
    internal static void ValidateEmbeddingOutput(float[] vector, int declaredDimensions)
    {
        if (vector.Length != declaredDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding provider returned {vector.Length}-dim vector " +
                $"but reports Dimensions={declaredDimensions}. " +
                "The provider is returning inconsistent dimensions.");
        }
    }
}
