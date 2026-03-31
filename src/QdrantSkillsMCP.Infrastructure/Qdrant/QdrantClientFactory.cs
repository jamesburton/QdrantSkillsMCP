using System.Net;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

/// <summary>
/// Creates QdrantClient with the appropriate protocol handler based on configuration.
/// Supports: grpc (native), grpc-web (Azure App Service compatible), http (REST API fallback via gRPC-Web text mode).
/// </summary>
public sealed class QdrantClientFactory
{
    private readonly QdrantSkillsOptions _options;
    private readonly ILogger<QdrantClientFactory> _logger;

    public QdrantClientFactory(IOptions<QdrantSkillsOptions> options, ILogger<QdrantClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a QdrantClient using the resolved protocol.
    /// </summary>
    public QdrantClient Create()
    {
        var protocol = ResolveProtocol();
        return protocol switch
        {
            QdrantProtocolType.GrpcWeb => CreateGrpcWebClient(),
            QdrantProtocolType.Http => CreateHttpClient(),
            _ => CreateGrpcClient()
        };
    }

    /// <summary>
    /// Resolves the effective protocol from explicit config or auto-detection.
    /// Auto-detect: gRPC for localhost, gRPC-Web for remote hosts (Azure-compatible default).
    /// </summary>
    public QdrantProtocolType ResolveProtocol()
    {
        if (!string.IsNullOrEmpty(_options.QdrantProtocol))
        {
            return _options.QdrantProtocol.ToLowerInvariant() switch
            {
                "grpc-web" or "grpcweb" => QdrantProtocolType.GrpcWeb,
                "http" or "rest" => QdrantProtocolType.Http,
                "grpc" => QdrantProtocolType.Grpc,
                _ => throw new InvalidOperationException(
                    $"Unknown QdrantProtocol '{_options.QdrantProtocol}'. Valid values: grpc, grpc-web, http")
            };
        }

        // Auto-detect: use gRPC for localhost, gRPC-Web for remote (Azure-compatible default)
        var isLocal = _options.QdrantHost is "localhost" or "127.0.0.1" or "::1"
            || _options.QdrantHost.StartsWith("host.docker.internal", StringComparison.OrdinalIgnoreCase);

        if (!isLocal)
        {
            _logger.LogInformation(
                "Remote Qdrant host detected ({Host}). Using gRPC-Web for Azure compatibility. " +
                "Set QdrantProtocol=grpc to force native gRPC.", _options.QdrantHost);
            return QdrantProtocolType.GrpcWeb;
        }

        return QdrantProtocolType.Grpc;
    }

    private QdrantClient CreateGrpcClient()
    {
        _logger.LogDebug("Creating Qdrant client with native gRPC (host={Host}, port={Port}, tls={Tls})",
            _options.QdrantHost, _options.QdrantGrpcPort, _options.UseTls);
        return new QdrantClient(
            _options.QdrantHost,
            _options.QdrantGrpcPort,
            https: _options.UseTls,
            apiKey: _options.QdrantApiKey);
    }

    /// <summary>
    /// Creates a GrpcChannel that forces HTTP/1.1 via GrpcWebHandler.
    /// Azure App Service rejects HTTP/2 with HTTP_1_1_REQUIRED — the inner
    /// HttpClientHandler must be pinned to HTTP/1.1 so GrpcWebHandler can
    /// wrap the gRPC frames over a standard HTTP/1.1 POST.
    /// </summary>
    private GrpcChannel CreateHttp11Channel(GrpcWebMode mode)
    {
        var scheme = _options.UseTls ? "https" : "http";
        var address = $"{scheme}://{_options.QdrantHost}:{_options.QdrantGrpcPort}";

        // Build handler chain: ApiKeyHandler → GrpcWebHandler → HttpClientHandler
        HttpMessageHandler handler = new HttpClientHandler();
        handler = new GrpcWebHandler(mode, handler) { HttpVersion = HttpVersion.Version11 };

        if (!string.IsNullOrEmpty(_options.QdrantApiKey))
            handler = new ApiKeyDelegatingHandler(_options.QdrantApiKey, handler);

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = handler });
    }

    /// <summary>
    /// Injects the Qdrant api-key header into every outgoing HTTP request.
    /// Used for gRPC-Web/HTTP paths where the QdrantClient constructor doesn't
    /// accept an apiKey parameter.
    /// </summary>
    private sealed class ApiKeyDelegatingHandler(string apiKey, HttpMessageHandler inner)
        : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private QdrantClient CreateGrpcWebClient()
    {
        _logger.LogDebug("Creating Qdrant client with gRPC-Web over HTTP/1.1 (host={Host}, port={Port}, tls={Tls})",
            _options.QdrantHost, _options.QdrantGrpcPort, _options.UseTls);
        var channel = CreateHttp11Channel(GrpcWebMode.GrpcWeb);
        var grpcClient = new QdrantGrpcClient(channel);
        return new QdrantClient(grpcClient);
    }

    private QdrantClient CreateHttpClient()
    {
        _logger.LogDebug("Creating Qdrant client with gRPC-Web text over HTTP/1.1 (host={Host}, port={Port}, tls={Tls})",
            _options.QdrantHost, _options.QdrantGrpcPort, _options.UseTls);
        _logger.LogWarning("HTTP/REST mode uses gRPC-Web text encoding for maximum HTTP/1.1 compatibility. " +
            "Consider gRPC-Web (grpc-web) for better performance.");
        var channel = CreateHttp11Channel(GrpcWebMode.GrpcWebText);
        var grpcClient = new QdrantGrpcClient(channel);
        return new QdrantClient(grpcClient);
    }
}
