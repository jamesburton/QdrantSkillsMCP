using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Core.Models;
using QdrantSkillsMCP.Core.Validation;

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
    /// Includes an "ALREADY LOADED SKILLS" prefix listing skills previously loaded in this session.
    /// </summary>
    [McpServerTool(Name = "search-skills", ReadOnly = true)]
    [Description("Search for skills by semantic similarity. Returns ranked results with scores. Use temperature (0.0=strict, 1.0=loose) to control match threshold. Set includeContent=false for metadata-only results.")]
    public async Task<string> SearchSkills(
        [Description("Natural language search query")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5,
        [Description("Search strictness: 0.0 (strict/exact) to 1.0 (loose/broad). Omit for no threshold.")] float? temperature = null,
        [Description("If true (default), returns full skill content. If false, returns metadata only (name, description, tags, score).")] bool includeContent = true,
        CancellationToken ct = default)
    {
        try
        {
            var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query, ct);

            // Map temperature to score threshold: temperature is 0.0 (strict) to 1.0 (loose)
            // Score threshold is the minimum similarity score; higher = stricter
            float? scoreThreshold = temperature.HasValue
                ? 1.0f - temperature.Value
                : null;

            var results = await repository.SearchAsync(queryEmbedding, maxResults, scoreThreshold, ct);

            // Build already-loaded prefix
            var loadedSkills = sessionTracker.GetLoadedSkills();
            var alreadyLoadedPrefix = loadedSkills.Count > 0
                ? $"ALREADY LOADED SKILLS: {string.Join(", ", loadedSkills)}\n\n"
                : string.Empty;

            // Build response based on includeContent flag
            object responseData;
            if (includeContent)
            {
                // Mark skills as loaded in session (only when content is included)
                foreach (var result in results)
                {
                    sessionTracker.MarkLoaded(result.Skill.Name);
                }

                responseData = new SearchResponse
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
                };
            }
            else
            {
                // Metadata only -- do NOT mark as loaded (content not fetched)
                responseData = new SearchResponse
                {
                    AlreadyLoaded = loadedSkills.Count > 0 ? loadedSkills.ToArray() : null,
                    Results = results.Select(r => new SearchResultDto
                    {
                        Name = r.Skill.Name,
                        Description = r.Skill.Description,
                        Tags = r.Skill.Tags,
                        Score = r.Score,
                        RawContent = null
                    }).ToArray()
                };
            }

            var json = JsonSerializer.Serialize(responseData, JsonOptions);

            logger.LogInformation("Search for '{Query}' returned {Count} results (includeContent={IncludeContent})",
                query, results.Count, includeContent);

            return $"{alreadyLoadedPrefix}{json}";
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
            sessionTracker.MarkLoaded(skill.Name);

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
    /// Lists all non-archived skills with their metadata (name, description, tags, updated date).
    /// </summary>
    [McpServerTool(Name = "list-skills", ReadOnly = true)]
    [Description("List all available (non-archived) skills. Returns metadata for each skill: name, description, tags, and last updated date.")]
    public async Task<string> ListSkills(CancellationToken ct = default)
    {
        try
        {
            var skills = await repository.ListAsync(ct);

            var response = new ListSkillsResponse
            {
                Skills = skills.Select(s => new SkillMetadataDto
                {
                    Name = s.Name,
                    Description = s.Description,
                    Tags = s.Tags,
                    UpdatedAt = s.UpdatedAt
                }).ToArray(),
                Total = skills.Count
            };

            logger.LogInformation("Listed {Count} skills", skills.Count);
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list skills");
            return $"Error listing skills: {ex.Message}";
        }
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
