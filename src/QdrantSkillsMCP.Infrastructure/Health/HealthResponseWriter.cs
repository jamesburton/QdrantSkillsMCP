using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QdrantSkillsMCP.Infrastructure.Health;

/// <summary>
/// Writes detailed JSON health check responses for /health/json endpoint per D-08.
/// </summary>
public static class HealthResponseWriter
{
    public static Task WriteDetailedHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                exception = e.Value.Exception?.Message
            })
        };
        return context.Response.WriteAsJsonAsync(result);
    }
}
