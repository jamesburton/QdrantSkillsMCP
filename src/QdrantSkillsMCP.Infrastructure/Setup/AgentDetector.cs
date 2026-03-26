namespace QdrantSkillsMCP.Infrastructure.Setup;

/// <summary>
/// Detects installed AI agents by probing known filesystem paths via registered config writers.
/// </summary>
public sealed class AgentDetector
{
    private readonly IEnumerable<IAgentConfigWriter> _writers;

    public AgentDetector(IEnumerable<IAgentConfigWriter> writers)
    {
        _writers = writers ?? throw new ArgumentNullException(nameof(writers));
    }

    /// <summary>
    /// Probes all registered writers for installed agents across all supported scopes.
    /// Returns a list of detected agents with their config paths and skill directory info.
    /// </summary>
    public IReadOnlyList<DetectedAgent> DetectInstalledAgents()
    {
        var detected = new List<DetectedAgent>();

        foreach (var writer in _writers)
        {
            foreach (var scope in writer.SupportedScopes)
            {
                var configPath = writer.DetectInstallation(scope);
                if (configPath is not null)
                {
                    detected.Add(new DetectedAgent(
                        Name: writer.AgentName,
                        ConfigPath: configPath,
                        Scope: scope,
                        WriterId: writer.WriterId,
                        SkillDirectoryPath: writer.SkillDirectoryPath));
                }
            }
        }

        return detected.AsReadOnly();
    }
}
