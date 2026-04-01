using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Core.Validation;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Tools;

/// <summary>
/// MCP tools for skill search and retrieval: search-skills, load-skill, list-skills.
/// All methods return string results (JSON) and never throw -- errors are returned as user-friendly messages.
/// </summary>
[McpServerToolType]
public sealed class SkillSearchTools(
    ISkillRepository repository,
    IEmbeddingService embeddingService,
    ISessionTracker sessionTracker,
    ILogger<SkillSearchTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Searches for skills by semantic similarity. Returns ranked results with scores.
    /// In full mode, already-loaded skills are included as a JSON field in the response.
    /// </summary>
    [McpServerTool(Name = "search-skills", ReadOnly = true)]
    [Description("Search for skills by semantic similarity. Returns ranked results with scores. Use temperature (0.0=strict, 1.0=loose) to control match threshold. outputMode: 'full' (default, returns content and marks loaded), 'names' (name strings only), 'summaries' (name+description+tags+score).")]
    public async Task<string> SearchSkills(
        [Description("Natural language search query")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5,
        [Description("Search strictness: 0.0 (strict/exact) to 1.0 (loose/broad). Omit for no threshold.")] float? temperature = null,
        [Description("Output detail level: 'full' (default), 'names', or 'summaries'")] string outputMode = "full",
        [Description("Optional session ID for tracking loaded skills. Omit for default process-scoped session.")] string? sessionId = null,
        CancellationToken ct = default)
    {
        try
        {
            var mode = ParseOutputMode(outputMode);

            var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query, ct);

            // Map temperature to score threshold: temperature is 0.0 (strict) to 1.0 (loose)
            // Score threshold is the minimum similarity score; higher = stricter
            float? scoreThreshold = temperature.HasValue
                ? 1.0f - temperature.Value
                : null;

            var results = await repository.SearchAsync(queryEmbedding, maxResults, scoreThreshold, ct);

            // Get already-loaded skills for inclusion in full-mode JSON response
            var loadedSkills = sessionTracker.GetLoadedSkills(sessionId);

            string json;
            switch (mode)
            {
                case OutputMode.Names:
                    // Return JSON array of skill name strings only. Do NOT mark as loaded.
                    json = JsonSerializer.Serialize(
                        results.Select(r => r.Skill.Name).ToArray(), JsonOptions);
                    break;

                case OutputMode.Summaries:
                    // Return array of {name, description, tags, score}. Do NOT mark as loaded.
                    json = JsonSerializer.Serialize(
                        results.Select(r => new SummaryDto
                        {
                            Name = r.Skill.Name,
                            Description = r.Skill.Description,
                            Tags = r.Skill.Tags,
                            Score = r.Score
                        }).ToArray(), JsonOptions);
                    break;

                default: // Full
                    // Mark skills as loaded in session (only when content is included)
                    foreach (var result in results)
                    {
                        sessionTracker.MarkLoaded(result.Skill.Name, sessionId);
                    }

                    json = JsonSerializer.Serialize(new SearchResponse
                    {
                        AlreadyLoaded = loadedSkills.Count > 0 ? loadedSkills.ToArray() : null,
                        Results = results.Select(r => new SearchResultDto
                        {
                            Name = r.Skill.Name,
                            Description = r.Skill.Description,
                            Tags = r.Skill.Tags,
                            Score = r.Score,
                            RawContent = r.Skill.RawContent
                        }).ToArray()
                    }, JsonOptions);
                    break;
            }

            logger.LogInformation("Search for '{Query}' returned {Count} results (outputMode={OutputMode})",
                query, results.Count, mode);

            return json;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search skills for query '{Query}'", query);
            return $"Error searching skills: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads a specific skill by exact name. Always returns the current version (no caching).
    /// Marks the skill as loaded in the current session.
    /// </summary>
    [McpServerTool(Name = "load-skill", ReadOnly = true)]
    [Description("Load a specific skill by exact name. Returns the full skill content (current version, no cache). Marks the skill as loaded in this session.")]
    public async Task<string> LoadSkill(
        [Description("Exact name of the skill to load")] string name,
        [Description("Optional session ID for tracking loaded skills. Omit for default process-scoped session.")] string? sessionId = null,
        CancellationToken ct = default)
    {
        try
        {
            var (isValid, error) = SkillNameValidator.Validate(name);
            if (!isValid)
                return $"Error: {error}";

            var skill = await repository.GetByNameAsync(name, ct);
            if (skill is null)
                return $"Error: Skill '{name}' not found.";

            // Mark as loaded in session
            sessionTracker.MarkLoaded(skill.Name, sessionId);

            logger.LogInformation("Skill '{SkillName}' loaded", name);

            // Return full skill content (raw_content for lossless retrieval)
            var response = new LoadSkillResponse
            {
                Name = skill.Name,
                Description = skill.Description,
                Tags = skill.Tags,
                UpdatedAt = skill.UpdatedAt,
                RawContent = skill.RawContent
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load skill '{SkillName}'", name);
            return $"Error loading skill '{name}': {ex.Message}";
        }
    }

    /// <summary>
    /// Lists all non-archived skills with their metadata.
    /// Output detail controlled by outputMode parameter.
    /// </summary>
    [McpServerTool(Name = "list-skills", ReadOnly = true)]
    [Description("List all available (non-archived) skills. outputMode: 'full' (default, name+description+tags+updatedAt), 'names' (name strings only), 'summaries' (name+description only).")]
    public async Task<string> ListSkills(
        [Description("Output detail level: 'full' (default), 'names', or 'summaries'")] string outputMode = "full",
        CancellationToken ct = default)
    {
        try
        {
            var mode = ParseOutputMode(outputMode);
            var skills = await repository.ListAsync(ct);

            string json;
            switch (mode)
            {
                case OutputMode.Names:
                    json = JsonSerializer.Serialize(
                        skills.Select(s => s.Name).ToArray(), JsonOptions);
                    break;

                case OutputMode.Summaries:
                    json = JsonSerializer.Serialize(new ListSkillsResponse
                    {
                        Skills = skills.Select(s => new SkillMetadataDto
                        {
                            Name = s.Name,
                            Description = s.Description
                        }).ToArray(),
                        Total = skills.Count
                    }, JsonOptions);
                    break;

                default: // Full
                    json = JsonSerializer.Serialize(new ListSkillsResponse
                    {
                        Skills = skills.Select(s => new SkillMetadataDto
                        {
                            Name = s.Name,
                            Description = s.Description,
                            Tags = s.Tags,
                            UpdatedAt = s.UpdatedAt
                        }).ToArray(),
                        Total = skills.Count
                    }, JsonOptions);
                    break;
            }

            logger.LogInformation("Listed {Count} skills (outputMode={OutputMode})", skills.Count, mode);
            return json;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list skills");
            return $"Error listing skills: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses a string outputMode to the <see cref="OutputMode"/> enum.
    /// Defaults to <see cref="OutputMode.Full"/> on invalid input.
    /// </summary>
    private static OutputMode ParseOutputMode(string? outputMode)
    {
        if (Enum.TryParse<OutputMode>(outputMode, ignoreCase: true, out var parsed))
            return parsed;
        return OutputMode.Full;
    }

    // --- Response DTOs for JSON serialization ---

    private sealed class SearchResponse
    {
        public string[]? AlreadyLoaded { get; init; }
        public required SearchResultDto[] Results { get; init; }
    }

    private sealed class SearchResultDto
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public string[]? Tags { get; init; }
        public float Score { get; init; }
        public string? RawContent { get; init; }
    }

    private sealed class SummaryDto
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public string[]? Tags { get; init; }
        public float Score { get; init; }
    }

    private sealed class LoadSkillResponse
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public string[]? Tags { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public required string RawContent { get; init; }
    }

    private sealed class ListSkillsResponse
    {
        public required SkillMetadataDto[] Skills { get; init; }
        public int Total { get; init; }
    }

    private sealed class SkillMetadataDto
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public string[]? Tags { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
