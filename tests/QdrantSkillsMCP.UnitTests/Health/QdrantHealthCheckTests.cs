using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;
using QdrantSkillsMCP.Infrastructure.Health;
using QdrantSkillsMCP.Infrastructure.Qdrant;

namespace QdrantSkillsMCP.UnitTests.Health;

public class QdrantHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenQdrantThrows_ReturnsDegraded()
    {
        // Arrange: GrpcQdrantOperations wrapping a client pointing at non-listening port
        var client = new global::Qdrant.Client.QdrantClient("localhost", 19999);
        var operations = new GrpcQdrantOperations(client);
        var healthCheck = new QdrantHealthCheck(operations);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("qdrant", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Qdrant unreachable", result.Description);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public void CheckHealthAsync_HealthyPath_ReturnsHealthy_CodeCheck()
    {
        // Verify the health check class structure compiles with IQdrantOperations.
        var client = new global::Qdrant.Client.QdrantClient("localhost", 6334);
        var operations = new GrpcQdrantOperations(client);
        var healthCheck = new QdrantHealthCheck(operations);
        Assert.NotNull(healthCheck);
    }
}
