using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-09-07-b7e2: the derivation's tool host as tests compose it — a real
/// factory over the real sandbox resolution, a recording sandbox, an in-memory reader.</summary>
internal static class DerivationTestLooks
{
    public const string Repo = "Sample.Server";

    /// <summary>A factory over the pipeline's sandboxes; with none set, it yields no host.</summary>
    public static DerivationLookFactory Factory(
        ISandboxFileReaderFactory? files = null, ISourceScopeSandboxFactory? scopes = null,
        AgentSmith.Application.Services.Turns.TurnActivityTools? activity = null) =>
        new(new SandboxTargets(), files ?? new StubSandboxFileReaderFactory(),
            new PackageEcosystemDetector(),
            new ProjectTemplateScopes(
                scopes ?? new NoScopes(), NullLogger<ProjectTemplateScopes>.Instance),
            activity ?? TurnActivityRecorder.Tools(),
            NullLogger<DerivationLook>.Instance);

    /// <summary>2026-09-13-9f84: the template-proof read as the product composes it — the
    /// real context.yaml parse over whatever reader the caller hands in.</summary>
    public static TemplateProofReport Proof(ISandboxFileReaderFactory? files = null) =>
        new(new AgentSmith.Infrastructure.Services.ContextYamlSerializer(
                new AgentSmith.Infrastructure.Services.ContextYamlBuilders()),
            files ?? new StubSandboxFileReaderFactory(),
            NullLogger<TemplateProofReport>.Instance);

    /// <summary>A source-scope factory that would spawn nothing, for runs with no template.</summary>
    private sealed class NoScopes : ISourceScopeSandboxFactory
    {
        public ISourceScopeSandbox Create(
            Contracts.Models.Configuration.ResolvedProject project,
            Contracts.Models.Configuration.RepoConnection repo, string? revision = null) =>
            throw new InvalidOperationException("no template was declared in this test");
    }

    /// <summary>A host over one recording sandbox, reading the given files.</summary>
    public static DerivationLook Over(CountingSandbox sandbox, InMemorySandboxFileReader? files = null) =>
        new(new Dictionary<string, ISandbox> { [Repo] = sandbox },
            new FixedReaderFactory(files ?? new InMemorySandboxFileReader()),
            new PackageEcosystemDetector(), NullLogger.Instance);

    /// <summary>Records every step and answers each with the configured exit code and output.</summary>
    public sealed class CountingSandbox(int exitCode, string output = "") : ISandbox
    {
        public string JobId => "derivation";
        public List<Step> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public sealed class FixedReaderFactory(ISandboxFileReader reader) : ISandboxFileReaderFactory
    {
        public ISandboxFileReader Create(ISandbox sandbox) => reader;
    }
}
