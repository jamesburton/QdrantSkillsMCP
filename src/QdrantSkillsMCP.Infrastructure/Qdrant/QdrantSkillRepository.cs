using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Core.Models;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Yaml;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

/// <summary>
/// Full <see cref="ISkillRepository"/> implementation backed by Qdrant vector database.
/// Uses deterministic SHA-256 hashing of skill names for stable Qdrant point IDs.
/// </summary>
public sealed class QdrantSkillRepository : ISkillRepository
{
    private readonly IQdrantOperations _client;
    private readonly QdrantSkillsOptions _options;
    private readonly CollectionInitializer _collectionInitializer;
    private readonly SkillParser _parser;
    private readonly ILogger<QdrantSkillRepository> _logger;

    // REP-04: shared filter — reused by SearchAsync and ListAsync
    private static readonly Filter _activeSkillsFilter = new()
    {
        Must =
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "archived",
                    Match = new Match { Boolean = false }
                }
            }
        }
    };

    public QdrantSkillRepository(
        IQdrantOperations client,
        IOptions<QdrantSkillsOptions> options,
        CollectionInitializer collectionInitializer,
        SkillParser parser,
        ILogger<QdrantSkillRepository> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _collectionInitializer = collectionInitializer ?? throw new ArgumentNullException(nameof(collectionInitializer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a deterministic Qdrant point ID from a skill name.
    /// SHA-256 hash of UTF-8 bytes, first 16 bytes converted to a <see cref="Guid"/>.
    /// </summary>
    public static Guid GeneratePointId(string skillName) => QdrantPointIdHelper.FromString(skillName);

    /// <inheritdoc />
    public async Task AddAsync(Skill skill, float[] embedding, bool overwrite, CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var pointId = GeneratePointId(skill.Name);

        if (!overwrite)
        {
            // BUG-01: withPayload: false — we only need the count, not the payload
            var existing = await _client.RetrieveAsync(
                _options.CollectionName,
                pointId,
                withPayload: false,
                cancellationToken: ct);

            if (existing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Skill '{skill.Name}' already exists. Use overwrite=true to replace.");
            }
        }

        var point = BuildPointStruct(pointId, skill, embedding);
        await _client.UpsertAsync(_options.CollectionName, [point], cancellationToken: ct);

        _logger.LogDebug("Added skill '{SkillName}' (overwrite={Overwrite})", skill.Name, overwrite);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Skill skill, float[] embedding, CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var pointId = GeneratePointId(skill.Name);

        // Verify skill exists — BUG-01: withPayload: false, we only need the count
        var existing = await _client.RetrieveAsync(
            _options.CollectionName,
            pointId,
            withPayload: false,
            cancellationToken: ct);

        if (existing.Count == 0)
        {
            throw new InvalidOperationException($"Skill '{skill.Name}' not found. Cannot update a non-existent skill.");
        }

        var point = BuildPointStruct(pointId, skill, embedding);
        await _client.UpsertAsync(_options.CollectionName, [point], cancellationToken: ct);

        _logger.LogDebug("Updated skill '{SkillName}'", skill.Name);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string skillName, CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var pointId = GeneratePointId(skillName);
        await _client.DeleteAsync(_options.CollectionName, pointId, cancellationToken: ct);

        _logger.LogDebug("Deleted skill '{SkillName}'", skillName);
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(string skillName, CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var pointId = GeneratePointId(skillName);

        await _client.SetPayloadAsync(
            _options.CollectionName,
            new Dictionary<string, Value>
            {
                ["archived"] = true,
                ["updated_at"] = DateTimeOffset.UtcNow.ToString("o")
            },
            pointId,
            cancellationToken: ct);

        _logger.LogDebug("Archived skill '{SkillName}'", skillName);
    }

    /// <inheritdoc />
    public async Task<Skill?> GetByNameAsync(string skillName, CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var pointId = GeneratePointId(skillName);

        var results = await _client.RetrieveAsync(
            _options.CollectionName,
            pointId,
            withPayload: true,
            cancellationToken: ct);

        if (results.Count == 0)
            return null;

        return PointToSkill(results[0]);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding, int maxResults, float? scoreThreshold, CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var searchResults = await _client.SearchAsync(
            _options.CollectionName,
            queryEmbedding,
            filter: _activeSkillsFilter,
            limit: (ulong)maxResults,
            scoreThreshold: scoreThreshold,
            cancellationToken: ct);

        // OPT-02: Qdrant returns results ordered by score descending already.
        // Stable sort on recency as a tiebreaker without re-sorting by score.
        var results = searchResults
            .Select(r => new
            {
                Skill = PointToSkill(r),
                r.Score
            })
            .OrderByDescending(r => r.Skill.UpdatedAt)
            .Select(r => new SearchResult
            {
                Skill = r.Skill,
                Score = r.Score
            })
            .ToList();

        return results.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillMetadata>> ListAsync(CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var scrollResponse = await _client.ScrollAsync(
            _options.CollectionName,
            filter: _activeSkillsFilter,
            cancellationToken: ct);

        var metadata = new List<SkillMetadata>();
        foreach (var point in scrollResponse.Result)
        {
            var payload = point.Payload;
            metadata.Add(new SkillMetadata
            {
                Name = payload.GetString("name"),
                Description = payload.GetString("description") is { Length: > 0 } d ? d : null,
                Tags = payload.GetStringList("tags"),
                Score = 0,
                UpdatedAt = payload.GetDateTimeOffset("updated_at")
            });
        }

        return metadata.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        await _collectionInitializer.EnsureCollectionAsync(ct);
    }

    /// <summary>
    /// Builds a Qdrant <see cref="PointStruct"/> from a skill and its embedding vector.
    /// Stores the full raw content in the payload for lossless round-trip (CRUD-05).
    /// </summary>
    private static PointStruct BuildPointStruct(Guid pointId, Skill skill, float[] embedding)
    {
        var payload = new Dictionary<string, Value>
        {
            ["name"] = skill.Name,
            ["description"] = skill.Description ?? string.Empty,
            ["tags"] = skill.Tags ?? [],
            ["raw_content"] = skill.RawContent,
            ["archived"] = skill.Archived,
            ["updated_at"] = skill.UpdatedAt.ToString("o")
        };

        return new PointStruct
        {
            Id = pointId,
            Vectors = embedding,
            Payload = { payload }
        };
    }

    // REP-02: thin overloads delegate to shared helper
    private Skill PointToSkill(RetrievedPoint p) => SkillFromPayload(p.Payload);
    private Skill PointToSkill(ScoredPoint p)    => SkillFromPayload(p.Payload);

    /// <summary>
    /// REP-02 / REP-03 / BUG-04: shared payload → Skill reconstruction.
    /// Uses extension methods to guard against null ListValue and bad date strings.
    /// </summary>
    private Skill SkillFromPayload(MapField<string, Value> payload)
    {
        var rawContent = payload.GetString("raw_content");
        var (_, markdownBody, _) = _parser.Parse(rawContent);

        return new Skill
        {
            Name        = payload.GetString("name"),
            Description = payload.GetString("description") is { Length: > 0 } d ? d : null,
            Tags        = payload.GetStringList("tags"),
            RawContent  = rawContent,
            MarkdownBody = markdownBody,
            UpdatedAt   = payload.GetDateTimeOffset("updated_at"),
            Archived    = payload.GetBool("archived")
        };
    }
}
