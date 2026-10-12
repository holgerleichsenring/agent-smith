using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureGateFixture;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-e237: a mixed-stack repository has one clone per toolchain sandbox; the spec commit
/// judges every one of them with the terms read once at the pushed sandbox's merge-base.
/// </summary>
public sealed class StructureGateSecondarySandboxTests
{
    private const string Long = "class A\n{\n    void Long()\n    {\n        a();\n        b();\n    }\n}\n";
    private const string Secondary = "repo-node";

    private static RepoSandbox Changed(RepoSandbox repo, string path)
    {
        repo.Files[path] = Long;
        repo.Changed.Add(path);
        return repo;
    }

    private static PipelineContext TwoSandboxes(RepoSandbox pushed, RepoSandbox other)
    {
        var pipeline = PipelineOf(pushed, ("web", "src/Web"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = pushed, [Secondary] = other });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [Key] = [new RemoteContextDiscovery("web", "src/Web", "csharp")],
                [Secondary] = [new RemoteContextDiscovery("api", "src/Api", "csharp")],
            });
        return pipeline;
    }

    [Fact]
    public async Task StructureGate_SecondarySandboxExcess_IsFound()
    {
        // The secondary clone carries no terms of its own: they are read once, in the pushed sandbox.
        var other = Changed(new RepoSandbox(), "src/Api/A.cs");

        var verdict = await Gate().JudgeAsync(TwoSandboxes(Repo(), other), CancellationToken.None);

        verdict.Passed.Should().BeFalse();
        var finding = verdict.Excess.Should().ContainSingle().Which;
        finding.Path.Should().Be("src/Api/A.cs");
        finding.Repository.Should().Be(Key);
        verdict.JudgedContexts.Should().Be(1, "the terms are read once, not once per sandbox");
    }

    [Fact]
    public async Task StructureGate_SingleSandboxOrCleanSecondary_BehavesAsBefore()
    {
        var single = await Gate().JudgeAsync(
            PipelineOf(Changed(Repo(), "src/Api/A.cs"), ("api", "src/Api")), CancellationToken.None);
        var withCleanSecondary = await Gate().JudgeAsync(
            TwoSandboxes(Changed(Repo(), "src/Api/A.cs"), new RepoSandbox()), CancellationToken.None);

        withCleanSecondary.Findings.Should().BeEquivalentTo(single.Findings);
        withCleanSecondary.Passed.Should().Be(single.Passed);
        withCleanSecondary.JudgedFiles.Should().Be(single.JudgedFiles);
        withCleanSecondary.JudgedContexts.Should().Be(single.JudgedContexts);
        withCleanSecondary.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task StructureGate_SameExcessInTwoSandboxes_IsOneFinding()
    {
        var pushed = Changed(Repo(), "src/Api/A.cs");
        var other = Changed(new RepoSandbox(), "src/Api/A.cs");

        var verdict = await Gate().JudgeAsync(TwoSandboxes(pushed, other), CancellationToken.None);

        verdict.Findings.Should().ContainSingle("the same unit seen in two clones is one finding");
        other.RanSteps.Should().NotBeEmpty("the secondary clone is judged too");
    }

    [Fact]
    public async Task StructureGate_Divergences_ReadOnlyThePushedSandbox()
    {
        var other = Changed(new RepoSandbox(), "src/Api/A.cs");

        await Gate().DivergencesAsync(TwoSandboxes(Repo(), other), CancellationToken.None);

        other.RanSteps.Should().BeEmpty("at PR time the pushed sandbox already holds the consolidated edits");
    }
}
