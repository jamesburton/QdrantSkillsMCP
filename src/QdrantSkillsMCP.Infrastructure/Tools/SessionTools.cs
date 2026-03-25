using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.Infrastructure.Tools;

/// <summary>
/// MCP tools for session management: reset-session.
/// </summary>
[McpServerToolType]
public sealed class SessionTools(
    ISessionTracker sessionTracker,
    ILogger<SessionTools> logger)
{
    /// <summary>
    /// Clears the loaded-skills list for the current session (or a specific session if sessionId provided).
    /// </summary>
    [McpServerTool(Name = "reset-session")]
    [Description("Clears the loaded-skills list for the current session (or a specific session if sessionId provided).")]
    public string ResetSession(
        [Description("Optional session ID to reset. Omit to reset the default session.")] string? sessionId = null)
    {
        sessionTracker.Reset(sessionId);

        var message = sessionId is null
            ? "Session reset successfully."
            : $"Session '{sessionId}' reset successfully.";

        logger.LogInformation("Session reset (sessionId={SessionId})", sessionId ?? "(default)");
        return message;
    }
}
