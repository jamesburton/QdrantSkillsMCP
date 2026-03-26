using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Core.Models;
using QdrantSkillsMCP.Infrastructure.Cli;
using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Cli;

[Collection("ConsoleOutput")]
public class ConsoleHostTests : IDisposable
{
    private readonly ISkillRepository _repo;
    private readonly IEmbeddingService _embedding;
    private readonly ISessionTracker _session;
    private readonly ServiceProvider _serviceProvider;
    private readonly ConsoleHost _host;
    private readonly StringWriter _stdout;
    private readonly TextWriter _originalOut;

    public ConsoleHostTests()
    {
        _repo = Substitute.For<ISkillRepository>();
        _embedding = Substitute.For<IEmbeddingService>();
        _session = Substitute.For<ISessionTracker>();

        var services = new ServiceCollection();
        services.AddSingleton(_repo);
        services.AddSingleton(_embedding);
        services.AddSingleton(_session);
        services.AddSingleton(Options.Create(new QdrantSkillsOptions()));

        _serviceProvider = services.BuildServiceProvider();
        _host = new ConsoleHost(_serviceProvider);

        _stdout = new StringWriter();
        _originalOut = Console.Out;
        Console.SetOut(_stdout);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        _stdout.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task RunAsync_Search_DispatchesToSearchCommandAndReturnsZero()
    {
        _embedding.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f, 0.2f });
        _repo.SearchAsync(Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchResult>
            {
                new() { Skill = new Skill { Name = "auth-skill", RawContent = "", MarkdownBody = "", Description = "Auth" }, Score = 0.95f }
            });

        var exitCode = await _host.RunAsync(new[] { "--console", "search", "auth" });

        Assert.Equal(0, exitCode);
        var output = _stdout.ToString();
        Assert.Contains("auth-skill", output);
    }

    [Fact]
    public async Task RunAsync_List_DispatchesToListCommandAndReturnsZero()
    {
        _repo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SkillMetadata>
            {
                new() { Name = "skill-one", Description = "First skill" },
                new() { Name = "skill-two", Description = "Second skill" }
            });

        var exitCode = await _host.RunAsync(new[] { "--console", "list" });

        Assert.Equal(0, exitCode);
        var output = _stdout.ToString();
        Assert.Contains("skill-one", output);
        Assert.Contains("skill-two", output);
    }

    [Fact]
    public async Task RunAsync_Load_DispatchesToLoadCommandAndReturnsZero()
    {
        _repo.GetByNameAsync("my-skill", Arg.Any<CancellationToken>())
            .Returns(new Skill { Name = "my-skill", RawContent = "# My Skill", MarkdownBody = "body", Description = "A skill" });

        var exitCode = await _host.RunAsync(new[] { "--console", "load", "my-skill" });

        Assert.Equal(0, exitCode);
        var output = _stdout.ToString();
        Assert.Contains("my-skill", output);
    }

    [Fact]
    public async Task RunAsync_Status_DispatchesToStatusCommandAndReturnsZero()
    {
        var exitCode = await _host.RunAsync(new[] { "--console", "status" });

        Assert.Equal(0, exitCode);
        var output = _stdout.ToString();
        Assert.Contains("localhost", output);
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_ReturnsNonZero()
    {
        var exitCode = await _host.RunAsync(new[] { "--console", "foobar" });

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_JsonFlag_ProducesJsonOutput()
    {
        _repo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SkillMetadata>
            {
                new() { Name = "json-skill", Description = "Test" }
            });

        var exitCode = await _host.RunAsync(new[] { "--console", "--json", "list" });

        Assert.Equal(0, exitCode);
        var output = _stdout.ToString();
        Assert.Contains("\"Name\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("json-skill", output);
    }

    [Fact]
    public async Task RunAsync_SearchWithJsonFlag_ProducesJsonOutput()
    {
        _embedding.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f });
        _repo.SearchAsync(Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchResult>
            {
                new() { Skill = new Skill { Name = "test-skill", RawContent = "", MarkdownBody = "", Description = "Desc" }, Score = 0.8f }
            });

        var exitCode = await _host.RunAsync(new[] { "--console", "--json", "search", "test" });

        Assert.Equal(0, exitCode);
        var output = _stdout.ToString();
        Assert.Contains("\"Name\"", output, StringComparison.OrdinalIgnoreCase);
    }
}
