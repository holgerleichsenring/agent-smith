using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-01-283dc: the prompt says what a reference address is, and only when there is one.</summary>
public sealed class ReferencePromptSectionTests
{
    private readonly ReferenceSandboxFixture _fixture = new();

    [Fact]
    public void ReferencePromptSection_SaysMaterialNotWebsite()
    {
        var section = ReferencePromptSection.Build(["repo-a", "template:server", "reference:site"]);

        section.Should().Contain("## Material the operator uploaded").And.Contain("`reference:site`")
            .And.Contain("an application's source").And.Contain("exact values").And.NotContain("template:server");
    }

    [Fact]
    public void ReferencePromptSection_Build_WithoutAReference_IsEmpty()
    {
        ReferencePromptSection.Build(["repo-a", "template:server"]).Should().BeEmpty();
    }

    // 2026-11-04-96a0c: earlier turns' file content is not in this turn, so the line says so and lists what is unopened.
    [Fact]
    public void ReferencePromptSection_PartlyOpened_SaysNOfMNotInThisTurnAndListsTheFirstUnopened()
    {
        var readable = Enumerable.Range(0, 30).Select(i => $"app/f{i:00}.cs").ToHashSet();
        var section = Section(new ReferenceCoverage(readable, new HashSet<string> { "app/f00.cs", "app/f01.cs" }, 4));

        section.Should().Contain("Earlier turns of this conversation opened 2 of the 30 files read_file can read")
            .And.Contain("grep_in_tree and run_in_reference are not counted")
            .And.Contain("Their content is not in this turn; only the note carries it forward.")
            .And.Contain("`app/f02.cs`").And.Contain("`app/f26.cs`").And.NotContain("`app/f27.cs`")
            .And.Contain("… and 3 more.")
            .And.Contain("4 files read_file cannot open (binary or over 1 MB) are not counted.");
    }

    [Fact]
    public void ReferencePromptSection_AllOpened_SaysSo()
    {
        var readable = new HashSet<string> { "app/a.cs", "app/b.cs" };

        var section = Section(new ReferenceCoverage(readable, readable, 0));

        section.Should().Contain("opened 2 of the 2 files").And.Contain("All 2 opened earlier.")
            .And.NotContain("Not opened yet").And.NotContain("cannot open");
    }

    // 2026-11-11-205fd: the read tools by their surface names; no sentence about a write tool the turn never had.
    [Fact]
    public void ReferencePromptSection_NamesGrepInTree_NoWriteSentence()
    {
        var section = ReferencePromptSection.Build(["reference:site"]);

        section.Should().Contain("read_file, grep_in_tree and directory_tree")
            .And.NotContain("a write comes back").And.NotContain("file mode");
    }

    [Fact]
    public void ReferenceCoverageText_NamesGrepInTree() =>
        ReferenceCoverageText.Under(new ReferenceCoverage(new HashSet<string> { "a.cs" }, new HashSet<string>(), 0))
            .Should().Contain("grep_in_tree and run_in_reference are not counted").And.NotContain("; grep and");

    private string Section(ReferenceCoverage coverage)
    {
        var upload = _fixture.Factory(Holds.None()).Create(
            ReferenceSandboxFixture.Project, ReferenceSandboxFixture.Conversation, "reference:app", "set-1", coverage: coverage);
        return ReferencePromptSection.Build(["reference:app"], new Dictionary<string, ISandbox> { ["reference:app"] = upload });
    }
}
