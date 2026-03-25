using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Core.Validation;
using QdrantSkillsMCP.Infrastructure.Yaml;

namespace QdrantSkillsMCP.Infrastructure.Tools;

/// <summary>
/// MCP tools for skill CRUD operations: add, update, delete, archive.
/// All methods return string results and never throw -- errors are returned as user-friendly messages.
/// </summary>
[McpServerToolType]
public sealed class SkillCrudTools(
    ISkillRepository repository,
    IEmbeddingService embeddingService,
    SkillParser parser,
    ILogger<SkillCrudTools> logger)
{
    /// <summary>
    /// Adds a new skill to the repository. Parses YAML frontmatter, generates an embedding,
    /// and persists to Qdrant. Use overwrite=true to replace an existing skill with the same name.
    /// </summary>
    [McpServerTool(Name = "add-skill", Destructive = true, ReadOnly = false)]
    [Description("Add a new skill. Provide the skill name and full markdown content (with optional YAML frontmatter). Set overwrite=true to replace an existing skill.")]
    public async Task<string> AddSkill(
        [Description("Unique skill name (lowercase letters, numbers, hyphens; max 64 chars)")] string name,
        [Description("Full skill content in markdown with optional YAML frontmatter")] string content,
        [Description("If true, replaces an existing skill with the same name")] bool overwrite = false,
        CancellationToken ct = default)
    {
        try
        {
            var (isValid, error) = SkillNameValidator.Validate(name);
            if (!isValid)
                return $"Error: {error}";

            var (metadata, markdownBody, rawContent) = parser.Parse(content);

            // CRITICAL: Validate name parameter matches frontmatter name field (CRUD-05 lossless round-trip)
            if (metadata.Name is not null && !string.Equals(metadata.Name, name, StringComparison.Ordinal))
            {
                return $"Error: Skill name parameter '{name}' does not match frontmatter name '{metadata.Name}'. " +
                       "These must be consistent to ensure lossless round-trip (CRUD-05).";
            }

            var skill = new Core.Models.Skill
            {
                Name = name,
                Description = metadata.Description,
                Tags = metadata.Tags?.ToArray(),
                RawContent = rawContent,
                MarkdownBody = markdownBody,
                UpdatedAt = DateTimeOffset.UtcNow,
                Archived = false
            };

            // Generate embedding from description + body for richer vector
            var embeddingText = string.IsNullOrEmpty(skill.Description)
                ? skill.MarkdownBody
                : $"{skill.Description}\n\n{skill.MarkdownBody}";
            var embedding = await embeddingService.GenerateEmbeddingAsync(embeddingText, ct);

            await repository.AddAsync(skill, embedding, overwrite, ct);

            logger.LogInformation("Skill '{SkillName}' added successfully (overwrite={Overwrite})", name, overwrite);
            return $"Skill '{name}' added successfully.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add skill '{SkillName}'", name);
            return $"Error adding skill '{name}': {ex.Message}";
        }
    }

    /// <summary>
    /// Updates an existing skill. Parses new content, generates a new embedding, and persists changes.
    /// </summary>
    [McpServerTool(Name = "update-skill", Destructive = true, ReadOnly = false)]
    [Description("Update an existing skill with new content. The skill must already exist.")]
    public async Task<string> UpdateSkill(
        [Description("Name of the skill to update")] string name,
        [Description("New full skill content in markdown with optional YAML frontmatter")] string content,
        CancellationToken ct = default)
    {
        try
        {
            var (isValid, error) = SkillNameValidator.Validate(name);
            if (!isValid)
                return $"Error: {error}";

            var (metadata, markdownBody, rawContent) = parser.Parse(content);

            // CRITICAL: Validate name parameter matches frontmatter name field (CRUD-05 lossless round-trip)
            if (metadata.Name is not null && !string.Equals(metadata.Name, name, StringComparison.Ordinal))
            {
                return $"Error: Skill name parameter '{name}' does not match frontmatter name '{metadata.Name}'. " +
                       "These must be consistent to ensure lossless round-trip (CRUD-05).";
            }

            var skill = new Core.Models.Skill
            {
                Name = name,
                Description = metadata.Description,
                Tags = metadata.Tags?.ToArray(),
                RawContent = rawContent,
                MarkdownBody = markdownBody,
                UpdatedAt = DateTimeOffset.UtcNow,
                Archived = false
            };

            var embeddingText = string.IsNullOrEmpty(skill.Description)
                ? skill.MarkdownBody
                : $"{skill.Description}\n\n{skill.MarkdownBody}";
            var embedding = await embeddingService.GenerateEmbeddingAsync(embeddingText, ct);

            await repository.UpdateAsync(skill, embedding, ct);

            logger.LogInformation("Skill '{SkillName}' updated successfully", name);
            return $"Skill '{name}' updated successfully.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update skill '{SkillName}'", name);
            return $"Error updating skill '{name}': {ex.Message}";
        }
    }

    /// <summary>
    /// Permanently deletes a skill by name. This action cannot be undone -- consider archive-skill instead.
    /// </summary>
    [McpServerTool(Name = "delete-skill", Destructive = true, ReadOnly = false)]
    [Description("Permanently delete a skill by name. This cannot be undone. Consider using archive-skill instead for soft deletion.")]
    public async Task<string> DeleteSkill(
        [Description("Name of the skill to delete")] string name,
        CancellationToken ct = default)
    {
        try
        {
            var (isValid, error) = SkillNameValidator.Validate(name);
            if (!isValid)
                return $"Error: {error}";

            await repository.DeleteAsync(name, ct);

            logger.LogInformation("Skill '{SkillName}' deleted", name);
            return $"Skill '{name}' deleted permanently.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete skill '{SkillName}'", name);
            return $"Error deleting skill '{name}': {ex.Message}";
        }
    }

    /// <summary>
    /// Archives a skill, hiding it from search results but keeping it restorable.
    /// </summary>
    [McpServerTool(Name = "archive-skill", Destructive = true, ReadOnly = false)]
    [Description("Archive a skill (soft delete). The skill is hidden from search and list results but can be restored later.")]
    public async Task<string> ArchiveSkill(
        [Description("Name of the skill to archive")] string name,
        CancellationToken ct = default)
    {
        try
        {
            var (isValid, error) = SkillNameValidator.Validate(name);
            if (!isValid)
                return $"Error: {error}";

            await repository.ArchiveAsync(name, ct);

            logger.LogInformation("Skill '{SkillName}' archived", name);
            return $"Skill '{name}' archived. It is hidden from search but can be restored.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to archive skill '{SkillName}'", name);
            return $"Error archiving skill '{name}': {ex.Message}";
        }
    }
}
