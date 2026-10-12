using AgentSmith.Application.Services.Structure;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureGateFixture;

namespace AgentSmith.Tests.Structure;

public sealed class StructureGateTests
{
    private const string Long = "class A\n{\n    void Long()\n    {\n        a();\n        b();\n    }\n}\n";

    private static async Task<StructureGateVerdict> Judge(RepoSandbox repo, string workdir = "src/Api") =>
        await Gate().JudgeAsync(PipelineOf(repo, ("api", workdir)), CancellationToken.None);

    private static RepoSandbox Changed(RepoSandbox repo, string path, string content)
    {
        repo.Files[path] = content;
        repo.Changed.Add(path);
        return repo;
    }

    [Fact]
    public async Task StructureGate_NewOversizedFunction_ReturnsFinding()
    {
        var verdict = await Judge(Changed(Repo(), "src/Api/A.cs", Long));

        verdict.Findings.Should().ContainSingle().Which.Key.Should().Be("A.Long()");
    }

    [Fact]
    public async Task StructureGate_BaselinedUnitNotGrown_ReturnsNothing()
    {
        var verdict = await Judge(Changed(Repo("src/Api/A.cs\tA.Long()\tfunction_lines\t6\n"), "src/Api/A.cs", Long));

        verdict.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task StructureGate_BaselinedUnitGrown_ReturnsFinding()
    {
        var verdict = await Judge(Changed(Repo("src/Api/A.cs\tA.Long()\tfunction_lines\t4\n"), "src/Api/A.cs", Long));

        verdict.Findings.Should().ContainSingle().Which.Value.Should().Be(5, "the unit grew past its baselined 4");
    }

    [Fact]
    public async Task StructureGate_RootWorkdir_JudgesEveryPath()
    {
        var verdict = await Judge(Changed(Repo(), "anywhere/A.cs", Long), workdir: "./");

        verdict.Findings.Should().ContainSingle();
    }

    [Fact]
    public async Task StructureGate_DeletedFile_IsNotJudged()
    {
        var repo = Repo();
        repo.Changed.Add("src/Api/Gone.cs");

        (await Judge(repo)).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task StructureGate_NoLimitsBlock_ReturnsNothing()
    {
        var repo = Changed(Repo(), "src/Api/A.cs", Long);
        repo.BaseFiles[".agentsmith/contexts/api/principles.md"] = "## Additions\n";

        (await Judge(repo)).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task StructureGate_BaselineRaisedInWorkingTree_JudgesAgainstTheBase()
    {
        var repo = Changed(Repo(), "src/Api/A.cs", Long);
        repo.Files[".agentsmith/contexts/api/structure-baseline.tsv"] = "src/Api/A.cs\tA.Long()\tfunction_lines\t99\n";
        repo.Files[".agentsmith/contexts/api/principles.md"] = "## Limits\n\n```yaml\nfunction_lines: 99\n```\n";

        (await Judge(repo)).Findings.Should().ContainSingle("the master cannot raise its own ceiling");
    }

    [Fact]
    public async Task StructureGate_NoBaseline_Passes()
    {
        var repo = Changed(Repo(), "src/Api/A.cs", Long);
        repo.BaseFiles.Remove(".agentsmith/contexts/api/structure-baseline.tsv");

        (await Judge(repo)).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task StructureGate_UnavailableGrammar_FailsNamingIt()
    {
        var repo = Changed(Repo(), "src/Api/A.cs", Long);

        var verdict = await Gate(missingGrammar: true).JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None);

        verdict.Unavailable.Should().Contain("tree-sitter-does-not-exist");
    }

    [Fact]
    public async Task StructureGate_FileOfNestedContext_IsJudgedByItsOwner()
    {
        var repo = Changed(Repo(), "src/Api/Worker/A.cs", Long);

        var verdict = await Gate().JudgeAsync(
            PipelineOf(repo, ("api", "src/Api"), ("worker", "src/Api/Worker")), CancellationToken.None);

        verdict.Passed.Should().BeTrue("the worker context declares no Limits, so the api context does not judge its file");
    }
}
