using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureGateFixture;

namespace AgentSmith.Tests.Structure;

/// <summary>What the commit gate reads per repository: which paths, which contexts, which sandbox.</summary>
public sealed class StructureGateRepositoryTests
{
    private const string Long = "class A\n{\n    void Long()\n    {\n        a();\n        b();\n    }\n}\n";

    private static RepoSandbox Changed(string path)
    {
        var repo = Repo();
        repo.Files[path] = Long;
        repo.Changed.Add(path);
        return repo;
    }

    [Fact]
    public async Task StructureGate_UntrackedFile_IsJudged()
    {
        var repo = Repo();
        repo.Files["src/Api/New.cs"] = Long;
        repo.Untracked.Add("src/Api/New.cs");

        var verdict = await Gate().JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None);

        verdict.Findings.Should().ContainSingle().Which.Path.Should().Be("src/Api/New.cs");
    }

    [Fact]
    public async Task StructureGate_ListingsAreNulSeparated_PathKeptRaw()
    {
        var repo = Changed("src/Api/With Space.cs");

        var verdict = await Gate().JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None);

        verdict.Findings.Should().ContainSingle().Which.Path.Should().Be("src/Api/With Space.cs");
    }

    [Fact]
    public async Task StructureGate_UnreadableFile_IsNotJudged()
    {
        var repo = Changed("src/Api/A.cs");
        repo.Unreadable.Add("src/Api/A.cs");

        (await Gate().JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None))
            .Passed.Should().BeTrue();
    }

    [Fact]
    public async Task StructureGate_GitListingFails_JudgesNothing()
    {
        var repo = Changed("src/Api/A.cs");
        repo.ChangedExitCode = 128;

        (await Gate().JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None))
            .Passed.Should().BeTrue("a listing git could not give is logged, not invented");
    }

    [Fact]
    public async Task StructureGate_TwoSandboxesOfOneRepository_JudgesOnceInThePushedOneWithEveryContext()
    {
        var pushed = Changed("src/Api/A.cs");
        var other = Changed("src/Api/A.cs");
        var pipeline = PipelineOf(pushed, ("web", "src/Web"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = pushed, ["repo-node"] = other });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [Key] = [new RemoteContextDiscovery("web", "src/Web", "csharp")],
                ["repo-node"] = [new RemoteContextDiscovery("api", "src/Api", "csharp")],
            });

        var verdict = await Gate().JudgeAsync(pipeline, CancellationToken.None);

        verdict.Findings.Should().ContainSingle("the api context's terms judge the pushed tree once");
        other.RanSteps.Should().BeEmpty("only the sandbox the checkpoint pushes is read");
    }

    [Fact]
    public async Task StructureGate_ContextOnlyInTheInventory_OwnsItsFiles()
    {
        var repo = Changed("src/Api/Worker/A.cs");
        var pipeline = PipelineOf(repo, ("api", "src/Api"));
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [Key] = [new RemoteContextDiscovery("worker", "src/Api/Worker", "csharp")],
            });

        (await Gate().JudgeAsync(pipeline, CancellationToken.None)).Passed.Should().BeTrue(
            "the worker context scoping left without a sandbox still owns its files");
    }
}
