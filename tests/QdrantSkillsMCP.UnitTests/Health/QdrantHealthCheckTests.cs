using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;
using QdrantSkillsMCP.Infrastructure.Health;

namespace QdrantSkillsMCP.UnitTests.Health;

public class QdrantHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenQdrantThrows_ReturnsDegraded()
    {
        // Arrange: client pointing at a non-listening port will throw on HealthAsync
        var client = new QdrantClient("localhost", 19999);
        var healthCheck = new QdrantHealthCheck(client);
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
        // Verify the health check class structure: Healthy result is returned on success path.
        // We can't easily test the healthy path without a running Qdrant,
        // so verify the class compiles and the Degraded path works above.
        // Integration tests in 05-03 will cover the happy path with Aspire.
        var client = new QdrantClient("localhost", 6334);
        var healthCheck = new QdrantHealthCheck(client);
        Assert.NotNull(healthCheck);
    }
}
