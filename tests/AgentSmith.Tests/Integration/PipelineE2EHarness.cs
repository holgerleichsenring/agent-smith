using AgentSmith.Application;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0196: builds production DI (AddAgentSmithInfrastructure + AddAgentSmithCommands)
/// then overrides the 4 external boundaries — IChatClientFactory, ISandboxFactory,
/// ISourceProviderFactory, ITicketProviderFactory — so each preset's handlers run
/// for real against deterministic stubs. No LLM cost, no docker, no git.
///
/// Per-preset tests resolve IPipelineExecutor + run the preset's command list end-
/// to-end against a synthetic PipelineContext, asserting result.IsSuccess.
/// </summary>
internal sealed class PipelineE2EHarness : IAsyncDisposable
{
    public IServiceProvider Services { get; }
    public StubSandboxFactory SandboxFactory { get; }
    public ScriptedChatClientFactory ChatClientFactory { get; }

    public PipelineE2EHarness(Func<ChatResponse>? llmResponder = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddInProcessSandbox();

        SandboxFactory = new StubSandboxFactory();
        ChatClientFactory = new ScriptedChatClientFactory(llmResponder);

        // Override the 4 external boundaries — production code stays in place
        // for every other service so handler logic + DI wiring are exercised.
        services.RemoveAll<IChatClientFactory>();
        services.AddSingleton<IChatClientFactory>(ChatClientFactory);
        services.RemoveAll<ISandboxFactory>();
        services.AddSingleton<ISandboxFactory>(SandboxFactory);
        services.RemoveAll<ISourceProviderFactory>();
        services.AddSingleton<ISourceProviderFactory>(new StubSourceProviderFactory());
        services.RemoveAll<ITicketProviderFactory>();
        services.AddSingleton<ITicketProviderFactory>(new StubTicketProviderFactory());

        // The progress reporter + dialogue transport that interactive runs use:
        // headless no-op for the test.
        services.RemoveAll<IProgressReporter>();
        services.AddSingleton<IProgressReporter, NullProgressReporter>();
        services.RemoveAll<IDialogueTransport>();
        services.AddSingleton<IDialogueTransport, NullDialogueTransport>();
        services.RemoveAll<IPromptCatalog>();
        services.AddSingleton<IPromptCatalog, StubPromptCatalog>();
        services.RemoveAll<ISwaggerProvider>();
        services.AddSingleton<ISwaggerProvider, StubSwaggerProvider>();

        Services = services.BuildServiceProvider();
    }

    public async Task<CommandResult> RunPresetAsync(string presetName, CancellationToken ct = default)
    {
        var executor = Services.GetRequiredService<IPipelineExecutor>();
        var preset = PipelinePresets.TryResolve(presetName)
            ?? throw new InvalidOperationException($"Unknown preset '{presetName}'");
        return await executor.ExecuteAsync(preset, BuildProject(presetName), BuildContext(presetName), ct);
    }

    private static ResolvedProject BuildProject(string presetName) => new()
    {
        Repos = [new RepoConnection { Name = "primary", Type = RepoType.Local, Path = "/tmp" }],
        Tracker = new TrackerConnection { Type = TrackerType.GitHub, Url = "https://stub.test" },
        Agent = new AgentConfig { Type = "claude", Model = "sonnet" },
        Pipeline = presetName,
        CodingPrinciplesPath = "config/coding-principles.md",
    };

    private static PipelineContext BuildContext(string presetName)
    {
        var pipeline = new PipelineContext();
        var agent = new AgentConfig { Type = "claude", Model = "sonnet" };
        // The concept vocab's pipeline_name enum uses canonical slugs; map
        // CLI-level aliases (add-feature, fix-no-test) onto the enum value
        // PipelineNameInitializer would write. Without this, the handler
        // rejects the preset name as not-in-enum.
        var conceptValue = presetName switch
        {
            "add-feature" => "feature-implementation",
            "fix-no-test" => "fix-bug",
            _ => presetName,
        };
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig(conceptValue, agent,
                PipelinePresets.GetDefaultSkillsPath(presetName), "config/coding-principles.md"));
        pipeline.Set(ContextKeys.PipelineName, conceptValue);
        pipeline.Set(ContextKeys.AgentConfig, agent);
        pipeline.Set(ContextKeys.Headless, true);
        pipeline.Set(ContextKeys.TicketId, new TicketId("1"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos,
            [new RepoConnection { Name = "primary", Type = RepoType.Local, Path = "/tmp" }]);
        pipeline.Set(ContextKeys.SourcePath, "/tmp/source");
        pipeline.Set(ContextKeys.SourceUrl, "git://stub");
        var legalTempPath = Path.Combine(Path.GetTempPath(), $"agentsmith-e2e-legal-{Guid.NewGuid():N}.txt");
        File.WriteAllText(legalTempPath, "Stub legal document content.");
        pipeline.Set(ContextKeys.SourceFilePath, legalTempPath);
        pipeline.Set(ContextKeys.SwaggerPath, "https://stub.test/swagger.json");
        pipeline.Set(ContextKeys.ApiTarget, "https://stub.test");
        pipeline.Set(ContextKeys.RunId, "2026-06-02T00-00-00-test");
        pipeline.Set(ContextKeys.ConceptVocabulary, RunStateConceptsTestFactory.FallbackMinimal);
        return pipeline;
    }

    public ValueTask DisposeAsync() => ((ServiceProvider)Services).DisposeAsync();
}

internal sealed class NullProgressReporter : IProgressReporter
{
    public Task ReportProgressAsync(int step, int total, PipelineCommand command, CancellationToken ct) => Task.CompletedTask;
    public Task<bool> AskYesNoAsync(string questionId, string text, bool defaultAnswer, CancellationToken ct) => Task.FromResult(defaultAnswer);
    public Task ReportDoneAsync(string summary, string? prUrl, CancellationToken ct) => Task.CompletedTask;
    public Task ReportErrorAsync(string text, int step, int total, string stepName, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class NullDialogueTransport : IDialogueTransport
{
    public Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken ct) => Task.CompletedTask;
    public Task<DialogAnswer?> WaitForAnswerAsync(string jobId, string questionId, TimeSpan timeout, CancellationToken ct) => Task.FromResult<DialogAnswer?>(null);
    public Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken ct) => Task.CompletedTask;
}
