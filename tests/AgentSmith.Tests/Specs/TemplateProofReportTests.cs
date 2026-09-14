using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-13-9f84: what a template declares as its OWN proof is read while its scope is
/// open and REPORTED — never refused. A template is held up as exemplary and nothing
/// checks that; the count of how many declare any proof at all has to exist before a gate
/// can refuse one for having none.
/// </summary>
public sealed class TemplateProofReportTests
{
    private const string Consumer = "server";
    private const string Address = ProjectTemplateScopes.NamePrefix + Consumer;
    private const string TemplateContext = "reference";
    private const string Path = "/work/.agentsmith/contexts/reference/context.yaml";

    private const string DeclaresTwo = """
        meta:
          workdir: "."
        verify:
          - label: "build"
            command: "dotnet build"
          - label: "test"
            command: "dotnet test"
        """;

    private const string DeclaresNone = """
        meta:
          workdir: "."
        stack:
          lang: "csharp"
        """;

    [Fact]
    public async Task TemplateVerify_ContextDeclaresStages_MintedAsEvidence()
    {
        var (report, look, pipeline) = Build(DeclaresTwo);

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        look.Evidence.Lines.Should().ContainSingle()
            .Which.Should().StartWith("[L1] " + Address)
            .And.Contain("declares 2 verify stage(s)")
            .And.Contain("build, test")
            .And.Contain("abc123", "the count is of the revision the scope landed on");
    }

    [Fact]
    public async Task TemplateVerify_ContextDeclaresNone_ReportedNotRefused()
    {
        var (report, look, pipeline) = Build(DeclaresNone);

        var act = () => report.RecordAsync(look, pipeline, CancellationToken.None);

        await act.Should().NotThrowAsync("the count comes first; refusing is a successor's job");
        look.Evidence.Lines.Should().ContainSingle()
            .Which.Should().Contain("declares NO verify stage");
    }

    [Fact]
    public async Task TemplateVerify_NoTemplateInTheLook_NoEvidenceLine()
    {
        var report = DerivationTestLooks.Proof();
        var look = new DerivationLook(
            Targets(), new DerivationTestLooks.FixedReaderFactory(new InMemorySandboxFileReader()),
            new PackageEcosystemDetector(), NullLogger.Instance);

        await report.RecordAsync(look, Pipeline(), CancellationToken.None);

        look.Evidence.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task TemplateVerify_ScopeNeverOpened_IsNotMaterialisedToReadIt()
    {
        var (report, look, pipeline) = Build(DeclaresTwo, materialized: false);

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        look.Evidence.Lines.Should().BeEmpty(
            "2026-09-13-84c0 keeps the scope lazy — a clone for one file is a pod nobody asked for");
    }

    [Fact]
    public async Task TemplateVerify_NoContextUnderThatName_SaysSoInsteadOfDeclaringNone()
    {
        var (report, look, pipeline) = Build(yaml: null);

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        look.Evidence.Lines.Should().ContainSingle()
            .Which.Should().Contain("carries no context under that name")
            .And.Contain("proves nothing", "a file nobody found measured nothing");
    }

    [Fact]
    public async Task TemplateVerify_EveryAttemptOfTheRetryLoop_MintsOneLine()
    {
        var (report, look, pipeline) = Build(DeclaresTwo);

        await report.RecordAsync(look, pipeline, CancellationToken.None);
        await report.RecordAsync(look, pipeline, CancellationToken.None);

        look.Evidence.Lines.Should().ContainSingle(
            "four ids for one unchanged file would be four facts where one was measured");
    }

    [Fact]
    public void RememberOnce_TheSameFrameworkLookTwice_MintsOneIdAndThenNull()
    {
        var evidence = new DerivationEvidence();

        evidence.RememberOnce(Address, "read context.yaml", 0, ran: true).Should().Be("L1");
        evidence.RememberOnce(Address, "read context.yaml", 0, ran: true).Should().BeNull();
        evidence.Remember(Address, "read context.yaml", 0, ran: true).Should().Be("L2",
            "a look the MODEL asked for twice is still two looks; this guard is the framework's");
    }

    private static (TemplateProofReport Report, DerivationLook Look, PipelineContext Pipeline) Build(
        string? yaml, bool materialized = true)
    {
        var files = new InMemorySandboxFileReader();
        if (yaml is not null) files.Files[Path] = yaml;
        var scope = new OpenScope(materialized);
        var look = new DerivationLook(
            Targets(), new DerivationTestLooks.FixedReaderFactory(files),
            new PackageEcosystemDetector(), NullLogger.Instance,
            new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal) { [Address] = scope });
        return (DerivationTestLooks.Proof(new DerivationTestLooks.FixedReaderFactory(files)),
            look, Pipeline());
    }

    private static Dictionary<string, ISandbox> Targets() =>
        new(StringComparer.Ordinal) { ["Sample.Server"] = new DerivationTestLooks.CountingSandbox(0) };

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Name = "sample",
            Templates =
            [
                new ProjectTemplate(Consumer, TemplateContext, "v1.0.0", new RepoConnection
                {
                    Name = "reference-project", Type = RepoType.GitHub,
                    Url = "https://stub.test/reference-project",
                }),
            ],
        });
        return pipeline;
    }

    /// <summary>A template scope the model already opened — the only state the read needs.</summary>
    private sealed class OpenScope(bool materialized) : ISourceScopeSandbox
    {
        public string RepoName => "reference-project";
        public bool IsMaterialized => materialized;
        public string? ResolvedSha => materialized ? "abc123" : null;
        public string JobId => "template-scope";

        public Task<string> MaterializeAsync(CancellationToken ct) =>
            throw new InvalidOperationException("the report materialises nothing");

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? p, CancellationToken ct) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, string.Empty));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
