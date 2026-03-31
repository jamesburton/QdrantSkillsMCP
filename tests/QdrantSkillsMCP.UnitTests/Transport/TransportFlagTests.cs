using QdrantSkillsMCP.Infrastructure.Transport;

namespace QdrantSkillsMCP.UnitTests.Transport;

public class TransportFlagTests
{
    // --- WantsHttp tests ---

    [Fact]
    public void WantsHttp_WithHttpFlag_ReturnsTrue()
        => Assert.True(TransportFlags.WantsHttp(["--http"]));

    [Fact]
    public void WantsHttp_WithUrlFlag_ReturnsTrue()
        => Assert.True(TransportFlags.WantsHttp(["--url", "http://0.0.0.0:9090"]));

    [Fact]
    public void WantsHttp_WithUrlEqualsFlag_ReturnsTrue()
        => Assert.True(TransportFlags.WantsHttp(["--url=http://0.0.0.0:9090"]));

    [Fact]
    public void WantsHttp_NoFlags_ReturnsFalse()
        => Assert.False(TransportFlags.WantsHttp([]));

    // --- WantsStdio tests ---

    [Fact]
    public void WantsStdio_WithStdioFlag_ReturnsTrue()
        => Assert.True(TransportFlags.WantsStdio(["--stdio"]));

    [Fact]
    public void WantsStdio_NoFlags_ReturnsFalse()
        => Assert.False(TransportFlags.WantsStdio([]));

    // --- HasConflict tests ---

    [Fact]
    public void HasConflict_HttpAndStdio_ReturnsTrue()
        => Assert.True(TransportFlags.HasConflict(["--http", "--stdio"]));

    [Fact]
    public void HasConflict_HttpOnly_ReturnsFalse()
        => Assert.False(TransportFlags.HasConflict(["--http"]));

    [Fact]
    public void HasConflict_StdioOnly_ReturnsFalse()
        => Assert.False(TransportFlags.HasConflict(["--stdio"]));

    [Fact]
    public void HasConflict_UrlAndStdio_ReturnsTrue()
        => Assert.True(TransportFlags.HasConflict(["--url", "http://localhost:8080", "--stdio"]));

    // --- ResolveListenUrl tests ---

    [Fact]
    public void ResolveListenUrl_UrlFlag_TakesPriority()
    {
        var url = TransportFlags.ResolveListenUrl(
            ["--url", "http://0.0.0.0:9090"],
            envUrl: "http://env:1111",
            configUrl: "http://config:2222");
        Assert.Equal("http://0.0.0.0:9090", url);
    }

    [Fact]
    public void ResolveListenUrl_UrlEqualsFlag_TakesPriority()
    {
        var url = TransportFlags.ResolveListenUrl(
            ["--url=http://0.0.0.0:9090"],
            envUrl: "http://env:1111");
        Assert.Equal("http://0.0.0.0:9090", url);
    }

    [Fact]
    public void ResolveListenUrl_EnvVar_WhenNoFlag()
    {
        var url = TransportFlags.ResolveListenUrl(
            [],
            envUrl: "http://env:1111",
            configUrl: "http://config:2222");
        Assert.Equal("http://env:1111", url);
    }

    [Fact]
    public void ResolveListenUrl_Config_WhenNoFlagOrEnv()
    {
        var url = TransportFlags.ResolveListenUrl(
            [],
            envUrl: null,
            configUrl: "http://config:2222");
        Assert.Equal("http://config:2222", url);
    }

    [Fact]
    public void ResolveListenUrl_Default_WhenNothingSet()
    {
        var url = TransportFlags.ResolveListenUrl([]);
        Assert.Equal("http://localhost:8080", url);
    }

    // --- StripTransportFlags tests ---

    [Fact]
    public void StripTransportFlags_RemovesHttpFlag()
    {
        var result = TransportFlags.StripTransportFlags(["--http", "--other"]);
        Assert.Equal(["--other"], result);
    }

    [Fact]
    public void StripTransportFlags_RemovesUrlAndValue()
    {
        var result = TransportFlags.StripTransportFlags(["--url", "http://localhost:8080", "--other"]);
        Assert.Equal(["--other"], result);
    }

    [Fact]
    public void StripTransportFlags_RemovesUrlEqualsValue()
    {
        var result = TransportFlags.StripTransportFlags(["--url=http://localhost:8080", "--other"]);
        Assert.Equal(["--other"], result);
    }

    [Fact]
    public void StripTransportFlags_RemovesStdioFlag()
    {
        var result = TransportFlags.StripTransportFlags(["--stdio", "--other"]);
        Assert.Equal(["--other"], result);
    }

    [Fact]
    public void StripTransportFlags_PreservesOtherArgs()
    {
        var result = TransportFlags.StripTransportFlags(["--console", "--config", "set"]);
        Assert.Equal(["--console", "--config", "set"], result);
    }
}
