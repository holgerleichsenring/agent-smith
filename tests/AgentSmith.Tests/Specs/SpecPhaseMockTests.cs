using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-01-283dh: an HTML mock in a spec directory whose name starts with a phase id belongs to
/// that phase — matched by the frozen id, never the stem a goal edit re-slugs — the phase prompt
/// names it, and no revision deletes it.
/// </summary>
public sealed class SpecPhaseMockTests
{
    private const string Key = "azdo-19106";
    private const string Dir = ".agentsmith/specs/" + Key;

    [Fact]
    public async Task SpecSetPhaseFileReader_MockNamedByPhaseIdWithOldSlug_IsStillMatched()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.SeedSet($"key: {Key}\nsource: Approved\nphases:\n- p19106a-the-new-goal\n",
            new Dictionary<string, string> { ["p19106a-the-new-goal"] = "phase: p19106a\ngoal: \"Goal\"\ndone:\n  - \"Done.\"\n" });
        branch.Seed($"{Dir}/p19106a-the-old-goal-mock.html", "<h1>mock</h1>");
        branch.Seed($"{Dir}/p19106a.html", "<h1>bare</h1>");
        branch.Seed($"{Dir}/p19106ab-other.html", "<h1>another phase</h1>");
        branch.Seed($"{Dir}/p19106a-notes.md", "not a mock");

        var phase = (await ReadAsync(branch)).Read!.Set.Phases.Single();

        phase.MockPaths.Should().Equal($"{Dir}/p19106a-the-old-goal-mock.html", $"{Dir}/p19106a.html");
    }

    [Fact]
    public void SpecPromptSection_PhaseWithMock_NamesIt()
    {
        var phase = Phase("p1", [$"{Dir}/p1-mock.html"]);

        var section = SpecPromptSection.Build(Set(phase), phase);

        section.Should().Contain("### This phase's design mock").And.Contain($"`{Dir}/p1-mock.html`")
            .And.Contain("render_reference").And.Contain("never edit or delete it");
    }

    [Fact]
    public void SpecPromptSection_PhaseWithoutMock_SaysNothingAboutOne() =>
        SpecPromptSection.Build(Set(Phase("p1", null)), Phase("p1", null)).Should().NotContain("design mock");

    [Fact]
    public void SpecSetStaleFiles_HtmlBesideSpecs_IsNeverSelected()
    {
        var listed = new[] { $"{Dir}/p1-kept.yaml", $"{Dir}/p2-dropped.yaml", $"{Dir}/p2-dropped-mock.html", $"{Dir}/p1-mock.html" };

        var stale = SpecSetStaleFiles.Select(listed, new SpecSetKey(Key), Set(Phase("p1", null, "kept")));

        stale.Should().Equal($"{Dir}/p2-dropped.yaml");
    }

    private static SpecPhase Phase(string id, IReadOnlyList<string>? mocks, string slug = "slug") =>
        new(new PhaseDraft(id, "Goal", $"phase: {id}", []) { Done = ["Done."] }, slug, string.Empty, [], mocks);

    private static SpecSet Set(SpecPhase phase) =>
        new(Key, [phase], SpecAccounting.Empty, [], SpecSource.BranchArtifact);

    private static async Task<SpecSetOnBranch> ReadAsync(SpecBranchFiles branch)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, "branch-sha"));
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(branch);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox.Object });
        var reader = new SpecSetReader(readers.Object,
            new SandboxGitOperations(new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance, readers.Object,
                new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            new SpecSetPhaseFileReader(new PhaseDraftReader(), NullLogger<SpecSetPhaseFileReader>.Instance),
            new SpecSetIndex(), new SandboxTargets(), NullLogger<SpecSetReader>.Instance);
        return await reader.ReadAsync(pipeline, new RepoConnection { Name = "primary" }, new SpecSetKey(Key), default);
    }
}
