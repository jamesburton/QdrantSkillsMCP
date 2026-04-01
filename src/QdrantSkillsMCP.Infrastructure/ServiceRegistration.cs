using System.ClientModel;
using System.Runtime.InteropServices;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Embeddings;
using OllamaSharp;
using OpenAI;
using Qdrant.Client;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Embedding;
using QdrantSkillsMCP.Infrastructure.Qdrant;
using QdrantSkillsMCP.Infrastructure.Session;
using QdrantSkillsMCP.Infrastructure.Setup;
using QdrantSkillsMCP.Infrastructure.Setup.Writers;
using QdrantSkillsMCP.Infrastructure.Yaml;

namespace QdrantSkillsMCP.Infrastructure;

/// <summary>
/// DI extension methods for registering all infrastructure services.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers setup-mode services (SetupWizard, AgentDetector, config writers).
    /// Intentionally does NOT register Qdrant infrastructure — setup needs no Qdrant connection.
    /// </summary>
    public static IServiceCollection AddSetupServices(this IServiceCollection services)
    {
        // Config writers (all 12 implementations)
        services.AddSingleton<IAgentConfigWriter, ClaudeConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, ClaudeDesktopConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, CopilotConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, CopilotCliConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, CodexConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, OpenCodeConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, KiloCodeConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, FactoryDroidConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, CursorConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, WindsurfConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, ZedConfigWriter>();
        services.AddSingleton<IAgentConfigWriter, SnippetFallbackWriter>();

        // Detector and wizard
        services.AddSingleton<AgentDetector>();
        services.AddSingleton<SetupWizard>();

        return services;
    }

    /// <summary>
    /// Registers all QdrantSkills infrastructure services in the DI container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQdrantSkillsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Bind configuration
        services.Configure<QdrantSkillsOptions>(config.GetSection(QdrantSkillsOptions.SectionName));

        // IQdrantOperations -- singleton, protocol selected from config
        services.AddSingleton<IQdrantOperations>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QdrantSkillsOptions>>().Value;
            var protocol = options.QdrantProtocol?.ToLowerInvariant();

            if (protocol is "http" or "rest")
            {
                // REST API path -- for Azure App Service-hosted Qdrant (port 443, no gRPC)
                var scheme = options.UseTls ? "https" : "http";
                var port = options.QdrantRestPort > 0 ? options.QdrantRestPort : (options.UseTls ? 443 : 6333);
                var baseUri = new Uri($"{scheme}://{options.QdrantHost}:{port}");

                var httpClient = new HttpClient { BaseAddress = baseUri };
                if (!string.IsNullOrEmpty(options.QdrantApiKey))
                    httpClient.DefaultRequestHeaders.Add("api-key", options.QdrantApiKey);

                var logger = sp.GetRequiredService<ILogger<RestQdrantOperations>>();
                return new RestQdrantOperations(httpClient, logger);
            }
            else
            {
                // gRPC path -- default for localhost
                var grpcClient = new global::Qdrant.Client.QdrantClient(
                    options.QdrantHost,
                    options.QdrantGrpcPort,
                    https: options.UseTls,
                    apiKey: options.QdrantApiKey);
                return new GrpcQdrantOperations(grpcClient);
            }
        });

        // DimensionValidator -- IHostedService that validates embedding dimensions on startup
        // Registered before CollectionInitializer so dimension check runs first
        services.AddHostedService<DimensionValidator>();

        // CollectionInitializer -- singleton, lazy collection creation
        services.AddSingleton<CollectionInitializer>();

        // SkillParser -- singleton, stateless
        services.AddSingleton<SkillParser>();

        // ISkillRepository -> QdrantSkillRepository
        services.AddSingleton<ISkillRepository, QdrantSkillRepository>();

        // --- Embedding provider registration (provider-specific) ---
        RegisterEmbeddingProvider(services, config);

        // ISessionTracker -> InMemorySessionTracker (singleton -- one process = one session for stdio)
        services.AddSingleton<ISessionTracker, InMemorySessionTracker>();

        return services;
    }

    private static void RegisterEmbeddingProvider(IServiceCollection services, IConfiguration config)
    {
        // Read provider from config section (before full options binding is available)
        var section = config.GetSection(QdrantSkillsOptions.SectionName);
        var providerString = section["EmbeddingProvider"];

        EmbeddingProviderType provider;
        bool isDefault = false;

        if (string.IsNullOrEmpty(providerString) ||
            !Enum.TryParse(providerString, ignoreCase: true, out provider))
        {
            provider = EmbeddingProviderType.LocalONNX;
            isDefault = true;
        }

        switch (provider)
        {
            case EmbeddingProviderType.OpenAI:
                RegisterOpenAi(services);
                break;

            case EmbeddingProviderType.LocalONNX:
                RegisterLocalOnnx(services, isDefault);
                break;

            case EmbeddingProviderType.Ollama:
                RegisterOllama(services);
                break;

            case EmbeddingProviderType.AzureOpenAI:
                RegisterAzureOpenAi(services);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported embedding provider: {provider}");
        }
    }

    private static void RegisterOpenAi(IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QdrantSkillsOptions>>().Value;

            var apiKey = options.OpenAiApiKey
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException(
                    "OpenAI API key not configured. Set QdrantSkills:OpenAiApiKey in config " +
                    "or OPENAI_API_KEY environment variable.");

            return new OpenAIClient(apiKey)
                .GetEmbeddingClient(options.EmbeddingModel)
                .AsIEmbeddingGenerator();
        });

        services.AddSingleton<IEmbeddingService, OpenAiEmbeddingService>();
    }

    private const string OnnxNativesNotFoundMessage =
        "ONNX runtime natives not found. The tool package does not bundle ONNX natives. " +
        "Either: (1) install the full ONNX runtime: dotnet add package Microsoft.ML.OnnxRuntime, " +
        "or (2) switch to a different embedding provider: qdrant-skills-mcp --config set EmbeddingProvider=OpenAI";

    private static void RegisterLocalOnnx(IServiceCollection services, bool isDefault)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QdrantSkillsOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("QdrantSkillsMCP.Embedding.ONNX");

            if (isDefault)
            {
                logger.LogWarning(
                    "No embedding provider configured. Defaulting to LocalONNX which requires native ONNX runtime. " +
                    "If this fails, set EmbeddingProvider to OpenAI or Ollama via: " +
                    "qdrant-skills-mcp --config set EmbeddingProvider=OpenAI");
            }

            // Probe for the native ONNX runtime before calling into it.
            // An AV inside native code is uncatchable; this check gives a readable error instead.
            var nativeLibName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "onnxruntime"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "libonnxruntime.so"
                : "libonnxruntime.dylib";

            if (!NativeLibrary.TryLoad(nativeLibName, out _))
            {
                logger.LogError(OnnxNativesNotFoundMessage);
                throw new InvalidOperationException(OnnxNativesNotFoundMessage);
            }

            try
            {
                var modelPath = OnnxModelResolver.ResolveModelPath(options, logger);
                var vocabPath = OnnxModelResolver.ResolveVocabPath(options, logger);

                // Create ONNX service and bridge from SK's ITextEmbeddingGenerationService to M.E.AI's IEmbeddingGenerator
#pragma warning disable CS0618 // BertOnnxTextEmbeddingGenerationService is obsolete but still functional
                var onnxService = BertOnnxTextEmbeddingGenerationService.Create(modelPath, vocabPath);
#pragma warning restore CS0618
                return onnxService.AsEmbeddingGenerator();
            }
            catch (Exception ex) when (ex is DllNotFoundException or TypeLoadException or FileNotFoundException)
            {
                logger.LogError(ex, OnnxNativesNotFoundMessage);
                throw new InvalidOperationException(OnnxNativesNotFoundMessage, ex);
            }
        });

        // Override VectorDimensions to 384 if still at default 1536
        services.PostConfigure<QdrantSkillsOptions>(opts =>
        {
            if (opts.VectorDimensions == 1536)
                opts.VectorDimensions = OnnxEmbeddingService.DefaultOnnxDimensions;
        });

        services.AddSingleton<IEmbeddingService, OnnxEmbeddingService>();
    }

    private static void RegisterOllama(IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QdrantSkillsOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("QdrantSkillsMCP.Embedding.Ollama");

            var url = options.EmbeddingUrl ?? "http://localhost:11434";
            if (options.EmbeddingUrl is null)
            {
                logger.LogWarning(
                    "No EmbeddingUrl configured for Ollama. Defaulting to {Url}. " +
                    "Set QdrantSkills:EmbeddingUrl to silence this warning.", url);
            }

            var model = options.EmbeddingModel ?? "all-minilm:l6-v2";
            var client = new OllamaApiClient(new Uri(url), model);

            // OllamaApiClient natively implements IEmbeddingGenerator<string, Embedding<float>>
            return client;
        });

        services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
    }

    private static void RegisterAzureOpenAi(IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QdrantSkillsOptions>>().Value;

            var endpoint = options.AzureOpenAiEndpoint
                ?? throw new InvalidOperationException(
                    "AzureOpenAI requires AzureOpenAiEndpoint. Set QdrantSkills:AzureOpenAiEndpoint.");

            var apiKey = options.AzureOpenAiApiKey
                ?? throw new InvalidOperationException(
                    "AzureOpenAI requires AzureOpenAiApiKey. Set QdrantSkills:AzureOpenAiApiKey.");

            var deployment = options.AzureOpenAiDeployment
                ?? throw new InvalidOperationException(
                    "AzureOpenAI requires AzureOpenAiDeployment. Set QdrantSkills:AzureOpenAiDeployment.");

            var client = new AzureOpenAIClient(
                new Uri(endpoint),
                new ApiKeyCredential(apiKey));

            return client
                .GetEmbeddingClient(deployment)
                .AsIEmbeddingGenerator();
        });

        services.AddSingleton<IEmbeddingService, AzureOpenAiEmbeddingService>();
    }
}
