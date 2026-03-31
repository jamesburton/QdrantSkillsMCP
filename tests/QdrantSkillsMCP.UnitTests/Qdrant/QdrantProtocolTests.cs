using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Qdrant;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Qdrant;

public class QdrantProtocolTests
{
    private static QdrantClientFactory CreateFactory(string? protocol = null, string host = "localhost")
    {
        var options = Options.Create(new QdrantSkillsOptions
        {
            QdrantHost = host,
            QdrantGrpcPort = 6334,
            QdrantProtocol = protocol
        });
        return new QdrantClientFactory(options, NullLogger<QdrantClientFactory>.Instance);
    }

    [Fact]
    public void ResolveProtocol_DefaultLocalhost_ReturnsGrpc()
    {
        var factory = CreateFactory(host: "localhost");
        Assert.Equal(QdrantProtocolType.Grpc, factory.ResolveProtocol());
    }

    [Fact]
    public void ResolveProtocol_DefaultRemote_ReturnsGrpcWeb()
    {
        var factory = CreateFactory(host: "qdrant-qhub.azurewebsites.net");
        Assert.Equal(QdrantProtocolType.GrpcWeb, factory.ResolveProtocol());
    }

    [Theory]
    [InlineData("grpc", QdrantProtocolType.Grpc)]
    [InlineData("grpc-web", QdrantProtocolType.GrpcWeb)]
    [InlineData("grpcweb", QdrantProtocolType.GrpcWeb)]
    [InlineData("http", QdrantProtocolType.Http)]
    [InlineData("rest", QdrantProtocolType.Http)]
    public void ResolveProtocol_ExplicitValue_ReturnsCorrectType(string protocol, QdrantProtocolType expected)
    {
        var factory = CreateFactory(protocol: protocol);
        Assert.Equal(expected, factory.ResolveProtocol());
    }

    [Fact]
    public void ResolveProtocol_InvalidValue_Throws()
    {
        var factory = CreateFactory(protocol: "invalid");
        Assert.Throws<InvalidOperationException>(() => factory.ResolveProtocol());
    }

    [Fact]
    public void ResolveProtocol_127001_ReturnsGrpc()
    {
        var factory = CreateFactory(host: "127.0.0.1");
        Assert.Equal(QdrantProtocolType.Grpc, factory.ResolveProtocol());
    }

    [Fact]
    public void ResolveProtocol_Ipv6Loopback_ReturnsGrpc()
    {
        var factory = CreateFactory(host: "::1");
        Assert.Equal(QdrantProtocolType.Grpc, factory.ResolveProtocol());
    }

    [Fact]
    public void ResolveProtocol_DockerInternal_ReturnsGrpc()
    {
        var factory = CreateFactory(host: "host.docker.internal");
        Assert.Equal(QdrantProtocolType.Grpc, factory.ResolveProtocol());
    }

    [Fact]
    public void ResolveProtocol_ExplicitGrpcOverridesRemoteAutoDetect()
    {
        var factory = CreateFactory(protocol: "grpc", host: "qdrant-remote.azurewebsites.net");
        Assert.Equal(QdrantProtocolType.Grpc, factory.ResolveProtocol());
    }

    [Fact]
    public void Create_DefaultLocalhost_ReturnsClient()
    {
        var factory = CreateFactory(host: "localhost");
        var client = factory.Create();
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_GrpcWeb_ReturnsClient()
    {
        var factory = CreateFactory(protocol: "grpc-web", host: "remote.example.com");
        var client = factory.Create();
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_Http_ReturnsClient()
    {
        var factory = CreateFactory(protocol: "http", host: "remote.example.com");
        var client = factory.Create();
        Assert.NotNull(client);
    }
}
