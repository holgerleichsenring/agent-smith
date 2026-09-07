using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0496: the bootstrap refusal states what it read. Three runs in a row aborted with
/// "the repo may be empty or newly renamed" while the file the probe wanted was sitting on
/// the base branch — the operator was sent to check a repository name that was correct.
/// </summary>
public sealed class BootstrapRefusalTests
{
    private const string WorkBranch = "agent-smith/19106";
    private const string BaseBranch = "main";

    [Fact]
    public async Task BootstrapRefusal_NamesTheProbedBranch_AndItsBase()
    {
        var refusal = await RefuseAsync();

        refusal.Should().Contain(WorkBranch).And.Contain(BaseBranch);
    }

    [Fact]
    public async Task BootstrapRefusal_NamesTheProbedPaths()
    {
        var refusal = await RefuseAsync();

        refusal.Should()
            .Contain("/work/.agentsmith/contexts/default/context.yaml")
            .And.Contain("/work/.agentsmith/contexts/default/principles.md");
    }

    [Fact]
    public async Task BootstrapRefusal_NoLongerClaimsTheRepoMayBeEmptyOrRenamed()
    {
        var refusal = await RefuseAsync();

        refusal.Should().NotContain("empty or newly renamed");
        refusal.Should().Contain("init-project", "the actionable half of the old text stays");
    }

    // 2026-09-01-eec0: nothing reads the old name as principles, so this repository is
    // refused — but "file missing" would send the operator after a file they already
    // wrote, so the refusal says which name went and what brings the new one back.
    [Fact]
    public async Task BootstrapGate_ARepositoryCarryingOnlyTheOldName_IsRefusedWithTheRemedy()
    {
        var refusal = await RefuseAsync(CarriesOnlyTheRetiredName);

        refusal.Should()
            .Contain(ProjectMetaPaths.RetiredPrinciplesFile)
            .And.Contain(ProjectMetaPaths.PrinciplesFile)
            .And.Contain("re-run init-project");
    }

    // 2026-09-04-ae3a: a two-context sandbox where ONE context was migrated and the other
    // was not. Folded over the sandbox, the refusal told a repository that IT predates the
    // rename — false of the migrated context, and its remedy was the run that skipped the
    // other one.
    [Fact]
    public async Task BootstrapRefusal_OneContextOfTwoIsMissingItsFile_NamesThatContext()
    {
        var refusal = await RefuseAsync(FrontendKeepsTheRetiredName, TwoContexts);

        refusal.Should().Contain("'frontend' has no principles.md");
        refusal.Should().NotContain("'backend' has no");
    }

    [Fact]
    public async Task BootstrapRefusal_TheRenameSentence_GoesWithTheContextItIsTrueOf()
    {
        var refusal = await RefuseAsync(FrontendKeepsTheRetiredName, TwoContexts);

        var frontend = refusal.IndexOf("'frontend' has no principles.md", StringComparison.Ordinal);
        var retired = refusal.IndexOf(ProjectMetaPaths.RetiredPrinciplesFile, StringComparison.Ordinal);
        frontend.Should().BeGreaterThan(-1);
        retired.Should().BeGreaterThan(frontend, "the sentence belongs to the context it describes");
    }

    private static readonly IReadOnlyList<RemoteContextDiscovery> TwoContexts =
    [
        new RemoteContextDiscovery("backend", "backend", "typescript"),
        new RemoteContextDiscovery("frontend", "frontend", "typescript"),
    ];

    // backend was migrated by an earlier init; frontend still carries the old name.
    private static bool FrontendKeepsTheRetiredName(string path) =>
        path.EndsWith(ProjectMetaPaths.ContextYamlFile, StringComparison.Ordinal)
        || (path.Contains("/backend/", StringComparison.Ordinal)
            && path.EndsWith(ProjectMetaPaths.PrinciplesFile, StringComparison.Ordinal))
        || (path.Contains("/frontend/", StringComparison.Ordinal)
            && path.EndsWith(ProjectMetaPaths.RetiredPrinciplesFile, StringComparison.Ordinal));

    private static bool CarriesOnlyTheRetiredName(string path) =>
        path.EndsWith(ProjectMetaPaths.RetiredPrinciplesFile, StringComparison.Ordinal)
        || path.EndsWith(ProjectMetaPaths.ContextYamlFile, StringComparison.Ordinal);

    private static async Task<string> RefuseAsync(
        Func<string, bool>? exists = null, IReadOnlyList<RemoteContextDiscovery>? contexts = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName(WorkBranch), "https://x/server.git"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["server"] = OriginHeadSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["server"] = new RemoteContextDiscovery("default", ".", "csharp")
            });
        if (contexts is not null)
            pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.SandboxContexts,
                new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
                {
                    ["server"] = contexts,
                });
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            PipelineName: "fix-bug", Agent: new AgentConfig(), SkillsPath: "skills",
            CodingPrinciplesPath: null));

        await CheckHandler(exists).ExecuteAsync(new BootstrapCheckContext(pipeline), CancellationToken.None);
        var gate = await new BootstrapGateHandler(
                RunStateConceptsTestFactory.Default, EventTestStubs.NoOp,
                NullLogger<BootstrapGateHandler>.Instance)
            .ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        gate.IsSuccess.Should().BeFalse();
        return gate.Message ?? string.Empty;
    }

    private static BootstrapCheckHandler CheckHandler(Func<string, bool>? exists)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => exists?.Invoke(path) ?? false);
        var readerFactory = new Mock<ISandboxFileReaderFactory>();
        readerFactory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return new BootstrapCheckHandler(
            new BootstrapContextProbe(readerFactory.Object, NullLogger<BootstrapContextProbe>.Instance),
            TestGit.BaseBranch,
            RunStateConceptsTestFactory.Default,
            new SandboxTargets(), NullLogger<BootstrapCheckHandler>.Instance);
    }

    // The clone's own answer to "what do you merge into", which is where the missing file was.
    private static ISandbox OriginHeadSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                    $"origin/{BaseBranch}\n")));
        return sandbox.Object;
    }
}
