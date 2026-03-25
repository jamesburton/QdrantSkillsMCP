using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests.Fixtures;

/// <summary>
/// Shared test fixture that starts a Qdrant container via Aspire AppHost.
/// Uses a unique collection name per test run to avoid cross-test pollution.
/// Implements <see cref="IAsyncLifetime"/> for xunit.v3 async setup/teardown.
/// </summary>
public sealed class QdrantFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    /// <summary>Qdrant client connected to the Aspire-managed container.</summary>
    public QdrantClient QdrantClient { get; private set; } = null!;

    /// <summary>Unique collection name for this test run.</summary>
    public string CollectionName { get; } = $"skills-test-{Guid.NewGuid():N}";

    /// <summary>Test options with the unique collection name and smaller vector dimensions for speed.</summary>
    public QdrantSkillsOptions Options { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.QdrantSkillsMCP_AppHost>();

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        // Wait for Qdrant to be healthy with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("qdrant", cts.Token)
                .WaitAsync(TimeSpan.FromSeconds(60), cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Fallback: poll Qdrant REST health endpoint (Aspire #5768 workaround)
            await WaitForQdrantHealthAsync(cts.Token);
        }

        // Get the Qdrant connection string from Aspire
        var connectionString = await _app.GetConnectionStringAsync("qdrant", cts.Token);

        // Parse connection endpoint for QdrantClient
        // Aspire Qdrant connection strings are typically: Endpoint=http://host:port;Key=...
        var (host, grpcPort) = ParseConnectionString(connectionString);

        QdrantClient = new QdrantClient(host, grpcPort);

        Options = new QdrantSkillsOptions
        {
            QdrantHost = host,
            QdrantGrpcPort = grpcPort,
            CollectionName = CollectionName,
            VectorDimensions = 64, // Smaller dimensions for faster tests
            EmbeddingModel = "test-model"
        };
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up test collection
        try
        {
            if (QdrantClient is not null)
            {
                await QdrantClient.DeleteCollectionAsync(CollectionName);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        // Dispose the distributed app
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// Polls Qdrant REST health endpoint as a fallback when Aspire health checks are unavailable.
    /// </summary>
    private async Task WaitForQdrantHealthAsync(CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        var maxAttempts = 30;

        for (var i = 0; i < maxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Qdrant REST health check on the HTTP port (typically 6333)
                var response = await httpClient.GetAsync("http://localhost:6333/healthz", ct);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        throw new TimeoutException("Qdrant did not become healthy within 30 seconds.");
    }

    /// <summary>
    /// Parses Aspire connection string format into host and gRPC port.
    /// Expected format: Endpoint=http://host:port;Key=...
    /// The HTTP port is typically 6333; gRPC port is 6334 (HTTP port + 1).
    /// </summary>
    private static (string Host, int GrpcPort) ParseConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return ("localhost", 6334);

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(kv[1].Trim());
                // Aspire exposes the HTTP port; gRPC is typically on port + 1
                return (uri.Host, uri.Port + 1);
            }
        }

        return ("localhost", 6334);
    }
}

/// <summary>
/// xunit.v3 collection definition to share the QdrantFixture across test classes.
/// Avoids starting multiple Qdrant containers.
/// </summary>
[CollectionDefinition(Name)]
public class QdrantCollection : ICollectionFixture<QdrantFixture>
{
    public const string Name = "Qdrant";
}
