using Qdrant.Client.Grpc;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

/// <summary>
/// Protocol-agnostic abstraction over the Qdrant operations used by this project.
/// Implemented by <see cref="GrpcQdrantOperations"/> (gRPC) and <see cref="RestQdrantOperations"/> (HTTP REST).
/// Uses gRPC-generated types as the shared data model since consumers already depend on them.
/// </summary>
public interface IQdrantOperations
{
    // --- Collection operations ---

    /// <summary>Lists all collection names.</summary>
    Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a collection with the given vector parameters.</summary>
    Task CreateCollectionAsync(string collectionName, VectorParams vectorParams, CancellationToken cancellationToken = default);

    /// <summary>Gets collection info (used for dimension validation).</summary>
    Task<CollectionInfo> GetCollectionInfoAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a collection.</summary>
    Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>Creates a payload field index.</summary>
    Task CreatePayloadIndexAsync(string collectionName, string fieldName, PayloadSchemaType schemaType, CancellationToken cancellationToken = default);

    // --- Point operations ---

    /// <summary>Upserts points into a collection.</summary>
    Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken = default);

    /// <summary>Retrieves points by ID(s), optionally with payload.</summary>
    Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(string collectionName, Guid id, bool withPayload = true, CancellationToken cancellationToken = default);

    /// <summary>Deletes a point by ID.</summary>
    Task DeleteAsync(string collectionName, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sets payload fields on a point by ID.</summary>
    Task SetPayloadAsync(string collectionName, IDictionary<string, Value> payload, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Searches for similar vectors with optional filter, limit, and score threshold.</summary>
    Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] queryVector,
        Filter? filter = null,
        ulong limit = 10,
        float? scoreThreshold = null,
        CancellationToken cancellationToken = default);

    /// <summary>Scrolls points in a collection with optional filter.</summary>
    Task<ScrollResponse> ScrollAsync(string collectionName, Filter? filter = null, CancellationToken cancellationToken = default);
}
