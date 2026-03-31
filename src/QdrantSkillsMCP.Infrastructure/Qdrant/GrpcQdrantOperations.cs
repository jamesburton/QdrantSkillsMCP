using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

/// <summary>
/// <see cref="IQdrantOperations"/> implementation that delegates to the official
/// <see cref="QdrantClient"/> (gRPC). Thin wrapper with no logic of its own.
/// </summary>
public sealed class GrpcQdrantOperations : IQdrantOperations
{
    private readonly QdrantClient _client;

    public GrpcQdrantOperations(QdrantClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken)
    {
        return await _client.ListCollectionsAsync(cancellationToken);
    }

    public async Task CreateCollectionAsync(string collectionName, VectorParams vectorParams, CancellationToken cancellationToken)
    {
        await _client.CreateCollectionAsync(collectionName, vectorParams, cancellationToken: cancellationToken);
    }

    public async Task<CollectionInfo> GetCollectionInfoAsync(string collectionName, CancellationToken cancellationToken)
    {
        return await _client.GetCollectionInfoAsync(collectionName, cancellationToken);
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        await _client.DeleteCollectionAsync(collectionName, cancellationToken: cancellationToken);
    }

    public async Task CreatePayloadIndexAsync(string collectionName, string fieldName, PayloadSchemaType schemaType, CancellationToken cancellationToken)
    {
        await _client.CreatePayloadIndexAsync(collectionName, fieldName, schemaType, cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken)
    {
        await _client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(string collectionName, Guid id, bool withPayload, CancellationToken cancellationToken)
    {
        return await _client.RetrieveAsync(collectionName, id, withPayload: withPayload, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string collectionName, Guid id, CancellationToken cancellationToken)
    {
        await _client.DeleteAsync(collectionName, id, cancellationToken: cancellationToken);
    }

    public async Task SetPayloadAsync(string collectionName, IDictionary<string, Value> payload, Guid id, CancellationToken cancellationToken)
    {
        // QdrantClient.SetPayloadAsync expects IReadOnlyDictionary, so convert if needed
        IReadOnlyDictionary<string, Value> readOnlyPayload = payload is IReadOnlyDictionary<string, Value> rod
            ? rod
            : new Dictionary<string, Value>(payload);
        await _client.SetPayloadAsync(collectionName, readOnlyPayload, id, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName, float[] queryVector, Filter? filter, ulong limit, float? scoreThreshold, CancellationToken cancellationToken)
    {
        return await _client.SearchAsync(collectionName, queryVector, filter: filter, limit: limit, scoreThreshold: scoreThreshold, cancellationToken: cancellationToken);
    }

    public async Task<ScrollResponse> ScrollAsync(string collectionName, Filter? filter, CancellationToken cancellationToken)
    {
        return await _client.ScrollAsync(collectionName, filter: filter, cancellationToken: cancellationToken);
    }
}
