using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests;

/// <summary>
/// Tests for MCP-02: tool discovery via reflection.
/// Scans the Infrastructure assembly for McpServerTool-attributed methods
/// and verifies all 7 expected tools are registered with correct attributes.
/// These are pure reflection tests -- no Qdrant or network needed.
/// </summary>
public sealed class ToolDiscoveryTests
{
    private static readonly Assembly InfraAssembly =
        typeof(QdrantSkillsMCP.Infrastructure.ServiceRegistration).Assembly;

    private static readonly IReadOnlyList<(MethodInfo Method, McpServerToolAttribute Attribute)> DiscoveredTools =
        DiscoverTools();

    private static List<(MethodInfo Method, McpServerToolAttribute Attribute)> DiscoverTools()
    {
        var tools = new List<(MethodInfo, McpServerToolAttribute)>();

        var toolTypes = InfraAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null);

        foreach (var type in toolTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;
                tools.Add((method, attr));
            }
        }

        return tools;
    }

    [Fact]
    public void InfrastructureAssemblyContainsExpected7Tools()
    {
        var toolNames = DiscoveredTools.Select(t => t.Attribute.Name).OrderBy(n => n).ToArray();

        Assert.Equal(7, toolNames.Length);

        Assert.Contains("add-skill", toolNames);
        Assert.Contains("update-skill", toolNames);
        Assert.Contains("delete-skill", toolNames);
        Assert.Contains("archive-skill", toolNames);
        Assert.Contains("search-skills", toolNames);
        Assert.Contains("load-skill", toolNames);
        Assert.Contains("list-skills", toolNames);
    }

    [Fact]
    public void AllToolsHaveDescriptionAttribute()
    {
        foreach (var (method, attr) in DiscoveredTools)
        {
            var description = method.GetCustomAttribute<DescriptionAttribute>();
            Assert.True(description is not null,
                $"Tool '{attr.Name}' (method {method.Name}) is missing [Description] attribute.");
            Assert.False(string.IsNullOrWhiteSpace(description!.Description),
                $"Tool '{attr.Name}' has an empty description.");
        }
    }

    [Theory]
    [InlineData("add-skill")]
    [InlineData("update-skill")]
    [InlineData("delete-skill")]
    [InlineData("archive-skill")]
    public void CrudToolsAreMarkedDestructive(string toolName)
    {
        var tool = DiscoveredTools.FirstOrDefault(t => t.Attribute.Name == toolName);
        Assert.True(tool.Attribute is not null, $"Tool '{toolName}' not found.");
        Assert.True(tool.Attribute.Destructive, $"Tool '{toolName}' should be marked Destructive=true.");
    }

    [Theory]
    [InlineData("search-skills")]
    [InlineData("load-skill")]
    [InlineData("list-skills")]
    public void SearchToolsAreMarkedReadOnly(string toolName)
    {
        var tool = DiscoveredTools.FirstOrDefault(t => t.Attribute.Name == toolName);
        Assert.True(tool.Attribute is not null, $"Tool '{toolName}' not found.");
        Assert.True(tool.Attribute.ReadOnly, $"Tool '{toolName}' should be marked ReadOnly=true.");
    }
}
