using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;

namespace QdrantSkillsMCP.Infrastructure.Health;

/// <summary>
/// Health check that reports Degraded (not Unhealthy) when Qdrant is unreachable.
/// Per D-07: server is still live even if Qdrant is down.
/// </summary>
public sealed class QdrantHealthCheck : IHealthCheck
{
    private readonly QdrantClient _client;

    public QdrantHealthCheck(QdrantClient client) => _client = client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.HealthAsync(cancellationToken);
            return HealthCheckResult.Healthy("Qdrant reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Qdrant unreachable", ex);
        }
    }
}
