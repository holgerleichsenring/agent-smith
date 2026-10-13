using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureGateFixture;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-13-50c7: a file under a context's sources is judged by that context's terms.</summary>
public sealed class StructureGateSourcesTests
{
    private const string Long = "class B\n{\n    void Long()\n    {\n        a();\n        b();\n    }\n}\n";

    private static RepoSandbox ChangedLibrary(string baseline = "")
    {
        var repo = Repo(baseline);
        repo.Files["src/Core/B.cs"] = Long;
        repo.Changed.Add("src/Core/B.cs");
        return repo;
    }

    [Fact]
    public async Task StructureGate_FileUnderSources_IsJudgedByTheContext()
    {
        var repo = ChangedLibrary();

        var verdict = await Gate().JudgeAsync(
            PipelineOf(repo, [new RemoteContextDiscovery("api", "src/Api", "csharp", Sources: ["src/Core"])]),
            CancellationToken.None);

        verdict.Findings.Should().ContainSingle().Which.Path.Should().Be("src/Core/B.cs");
    }

    [Fact]
    public async Task StructureGate_FileUnderSourcesBaselined_Passes()
    {
        var repo = ChangedLibrary("src/Core/B.cs\tB.Long()\tfunction_lines\t5\n");

        var verdict = await Gate().JudgeAsync(
            PipelineOf(repo, [new RemoteContextDiscovery("api", "src/Api", "csharp", Sources: ["src/Core"])]),
            CancellationToken.None);

        verdict.Passed.Should().BeTrue();
        verdict.JudgedFiles.Should().Be(1);
    }

    [Fact]
    public async Task StructureGate_NoSources_LibraryIsNotJudged()
    {
        var verdict = await Gate().JudgeAsync(PipelineOf(ChangedLibrary(), ("api", "src/Api")), CancellationToken.None);

        verdict.Passed.Should().BeTrue();
        verdict.JudgedFiles.Should().Be(0, "without sources the library belongs to no context");
    }
}
