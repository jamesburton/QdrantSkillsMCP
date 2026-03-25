using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

/// <summary>
/// Lazily creates the Qdrant collection with correct vector configuration and payload indexes.
/// Thread-safe via <see cref="SemaphoreSlim"/> double-checked locking.
/// Called before first Qdrant operation (lazy init avoids startup failure if Qdrant is slow -- QDR-04).
/// </summary>
public sealed class CollectionInitializer : IDisposable
{
    private readonly QdrantClient _client;
    private readonly QdrantSkillsOptions _options;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile bool _initialized;

    public CollectionInitializer(QdrantClient client, IOptions<QdrantSkillsOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Ensures the Qdrant collection exists with the correct vector dimensions and payload indexes.
    /// Safe to call from multiple threads; only the first call creates the collection.
    /// </summary>
    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _semaphore.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var collections = await _client.ListCollectionsAsync(ct);
            var collectionExists = collections.Any(c => c == _options.CollectionName);

            if (!collectionExists)
            {
                await _client.CreateCollectionAsync(
                    _options.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_options.VectorDimensions,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);
            }

            // Create payload indexes for efficient filtering
            await CreatePayloadIndexesAsync(ct);

            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task CreatePayloadIndexesAsync(CancellationToken ct)
    {
        // name: Keyword index for exact-match lookups
        await _client.CreatePayloadIndexAsync(
            _options.CollectionName,
            "name",
            PayloadSchemaType.Keyword,
            cancellationToken: ct);

        // archived: Bool index for filtering archived skills
        // Qdrant supports Bool as a payload schema type
        await _client.CreatePayloadIndexAsync(
            _options.CollectionName,
            "archived",
            PayloadSchemaType.Bool,
            cancellationToken: ct);

        // updated_at: Datetime index for recency ordering
        await _client.CreatePayloadIndexAsync(
            _options.CollectionName,
            "updated_at",
            PayloadSchemaType.Datetime,
            cancellationToken: ct);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
