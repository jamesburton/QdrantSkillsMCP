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
public class ReplLoopTests : IDisposable
{
    private readonly ISkillRepository _repo;
    private readonly IEmbeddingService _embedding;
    private readonly ISessionTracker _session;
    private readonly ServiceProvider _serviceProvider;
    private readonly ConsoleOutputFormatter _formatter;
    private readonly ReplLoop _repl;
    private readonly StringWriter _stdout;
    private readonly TextWriter _originalOut;

    public ReplLoopTests()
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
        _formatter = new ConsoleOutputFormatter(jsonOutput: false);
        _repl = new ReplLoop(_serviceProvider, _formatter);

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
    public async Task ProcessCommand_Search_ProducesSearchResults()
    {
        _embedding.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f, 0.2f });
        _repo.SearchAsync(Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchResult>
            {
                new() { Skill = new Skill { Name = "auth-skill", RawContent = "", MarkdownBody = "", Description = "Auth handling" }, Score = 0.9f }
            });

        var result = await _repl.ProcessCommandAsync("search auth");

        Assert.True(result.Continue);
        var output = _stdout.ToString();
        Assert.Contains("auth-skill", output);
    }

    [Fact]
    public async Task ProcessCommand_List_ProducesSkillList()
    {
        _repo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SkillMetadata>
            {
                new() { Name = "skill-a", Description = "Desc A" },
                new() { Name = "skill-b", Description = "Desc B" }
            });

        var result = await _repl.ProcessCommandAsync("list");

        Assert.True(result.Continue);
        var output = _stdout.ToString();
        Assert.Contains("skill-a", output);
        Assert.Contains("skill-b", output);
    }

    [Fact]
    public async Task ProcessCommand_Exit_ReturnsContinueFalse()
    {
        var result = await _repl.ProcessCommandAsync("exit");

        Assert.False(result.Continue);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ProcessCommand_Quit_ReturnsContinueFalse()
    {
        var result = await _repl.ProcessCommandAsync("quit");

        Assert.False(result.Continue);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ProcessCommand_Help_PrintsCommandList()
    {
        var result = await _repl.ProcessCommandAsync("help");

        Assert.True(result.Continue);
        var output = _stdout.ToString();
        Assert.Contains("search", output);
        Assert.Contains("list", output);
        Assert.Contains("exit", output);
    }

    [Fact]
    public async Task ProcessCommand_EmptyInput_HandlesGracefully()
    {
        var result = await _repl.ProcessCommandAsync("");

        Assert.True(result.Continue);
        // Should not throw or produce error output
    }

    [Fact]
    public async Task ProcessCommand_UnknownCommand_ProducesErrorButDoesNotCrash()
    {
        var originalErr = Console.Error;
        var errWriter = new StringWriter();
        Console.SetError(errWriter);

        try
        {
            var result = await _repl.ProcessCommandAsync("fizzle");

            Assert.True(result.Continue);
            var errOutput = errWriter.ToString();
            Assert.Contains("Unknown command", errOutput);
        }
        finally
        {
            Console.SetError(originalErr);
            errWriter.Dispose();
        }
    }
}
