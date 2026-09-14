using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-04-cf3d fast-tier end-to-end: run a109's shape. One repository, two contexts in
/// ONE sandbox (same toolchain image), the scope call names both. Live, the master read the
/// backend context's principles.md three times and never saw the frontend's — the loaders
/// handed it one representative context per sandbox. Now the master prompt carries both
/// principles files and both context.yaml files, each under the context it came from.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class EveryContextLoadedTests
{
    private const string ScopeReply =
        """{"repos":[{"name":"primary","affected":true,"confidence":0.95}],"contexts":{"primary":["backend","frontend"]},"rationale":"both contexts change"}""";

    private const string BothContextsJson = """
        {"phases": [
           {"slug": "dependency-updates",
            "goal": "Raise the package floors the audit names in both contexts",
            "contexts": ["backend", "frontend"],
            "steps": [{"id": "raise", "action": "Raise the versions in backend/ and frontend/"}],
            "done": ["both package.json files carry versions the audit no longer flags."],
            "carries": [1,2,3,4,5,6,7,8,9,10,11,12]}],
         "discarded": [],
         "discarded_contexts": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"raised","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"raised"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private const string BackendPrinciples = "# Backend rules\n\nEvery handler is a record.";
    private const string FrontendPrinciples = "# Frontend rules\n\nEvery component is a function.";

    [Fact]
    public async Task Harness_TwoContextsInOneSandbox_TheMasterIsPromptedWithBothPrinciplesFiles()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueScopeReply(ScopeReply)
            .EnqueueText(BothContextsJson)
            .EnqueueText("Planning: raise both floors.")
            .EnqueueToolCall("write_file", """{"path":"primary/backend/package.json","content":"{}"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { NeedsClarificationStatus = "needs-info" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        var masterPrompt = harness.ChatClient.LastMessages.First(m => m.Role == ChatRole.System).Text!;
        masterPrompt.Should().Contain("## Context: backend (workdir: backend)")
            .And.Contain(BackendPrinciples)
            .And.Contain("## Context: frontend (workdir: frontend)")
            .And.Contain(FrontendPrinciples, "the sibling context's rules govern its subtree");
        masterPrompt.IndexOf("## Context: backend", StringComparison.Ordinal).Should()
            .BeLessThan(masterPrompt.IndexOf("## Context: frontend", StringComparison.Ordinal),
                "documents arrive in the sandbox's context order");
        masterPrompt.Should().Contain("purpose: the backend context")
            .And.Contain("purpose: the frontend context", "both context.yaml files reach the master");
    }

    private static RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(
                new MultiContextSourceProviderFactory(["backend", "frontend"], ContextYamlFor));
            services.RemoveAll<ISandboxFactory>();
            services.AddSingleton<ISandboxFactory>(new ContextFilesSandboxFactory());
            services.RemoveAll<IPromptCatalog>();
            services.AddSingleton<IPromptCatalog, MasterTokensPromptCatalog>();
        });

    private static string? ContextYamlFor(string path)
    {
        foreach (var name in new[] { "backend", "frontend" })
        {
            if (path.Contains($"contexts/{name}/", StringComparison.Ordinal) && path.EndsWith("context.yaml", StringComparison.Ordinal))
                return ContextYaml(name);
        }
        return null;
    }

    private static string ContextYaml(string name) =>
        $"meta:\n  workdir: {name}\n  project: stub\n  purpose: the {name} context\nstack:\n  lang: csharp\nprerequisites: \"npm ci\"\n";

    /// <summary>
    /// The stub sandbox answers every ReadFile with exit 0, so the flat legacy principles path
    /// would always "exist" and shadow the per-context files. This one serves the two contexts'
    /// files and declares the flat path absent — the layout of a contexts repository.
    /// </summary>
    private sealed class ContextFilesSandboxFactory : ISandboxFactory
    {
        public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken) =>
            Task.FromResult<ISandbox>(new ContextFilesSandbox());
    }

    private sealed class ContextFilesSandbox : ISandbox
    {
        private static readonly Dictionary<string, string> Files = new(StringComparer.Ordinal)
        {
            ["/work/.agentsmith/contexts/backend/principles.md"] = BackendPrinciples,
            ["/work/.agentsmith/contexts/frontend/principles.md"] = FrontendPrinciples,
            ["/work/.agentsmith/contexts/backend/context.yaml"] = ContextYaml("backend"),
            ["/work/.agentsmith/contexts/frontend/context.yaml"] = ContextYaml("frontend"),
        };

        private readonly StubSandbox _inner = new();

        public string JobId => _inner.JobId;

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            if (step.Kind != StepKind.ReadFile || step.Path is null)
                return _inner.RunStepAsync(step, progress, cancellationToken);
            if (Files.TryGetValue(step.Path, out var content))
                return Task.FromResult(Result(step, 0, content));
            if (step.Path == "/work/.agentsmith/principles.md")
                return Task.FromResult(Result(step, 1, null));
            return _inner.RunStepAsync(step, progress, cancellationToken);
        }

        private static StepResult Result(Step step, int exitCode, string? content) => new(
            StepResult.CurrentSchemaVersion, step.StepId, exitCode, TimedOut: false,
            DurationSeconds: 0.01, ErrorMessage: null, OutputContent: content);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    /// <summary>
    /// The harness catalog's body declares no tokens; this one binds the two the coding master
    /// renders the loaded documents through, so the prompt under test carries them.
    /// </summary>
    private sealed class MasterTokensPromptCatalog : IPromptCatalog
    {
        private readonly StubPromptCatalog _inner = new();

        public string Get(string name) =>
            name == PipelinePresets.CodingMaster
                ? _inner.Get(name) + "\n{ProjectContextSection}\n{CodingPrinciples}\n"
                : _inner.Get(name);

        public string Render(string name, IReadOnlyDictionary<string, string> tokens)
        {
            var body = Get(name);
            foreach (var (key, value) in tokens)
                body = body.Replace("{" + key + "}", value);
            return body;
        }
    }
}
