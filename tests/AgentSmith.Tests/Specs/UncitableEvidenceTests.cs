using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0481: an evidence line that quotes no command names none. Found while reviewing this
/// phase — the budget's own notice was citable, because it carries no apostrophe and the
/// reading fell back to the whole line, so any sufficiently long prefix of the notice
/// resolved as a command that ran. p0470 kept it out of the STEP reading and left this open.
/// </summary>
public sealed class UncitableEvidenceTests
{
    private static CriterionAccount Resolve(string citation, params string[] commands) =>
        new CitationResolver(CitedFileIndex.FromDiff(string.Empty), commands)
            .Resolve(new AccountRow("criterion", AccountDisposition.Satisfied, citation, "note"));

    private static string Notice()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < 400; i++) log.Record("api", $"dotnet build Sample.Module{i}.csproj", new string('o', 900));
        return log.Evidence()[0];
    }

    [Fact]
    public void EvidenceCommand_ALineWithNoQuotedSpan_NamesNoCommand()
    {
        EvidenceCommand.InEvidence("DependencyAuditCommand: 0 advisories").Should().BeEmpty();
    }

    [Fact]
    public void Citation_TheBudgetNotice_IsNotCitable()
    {
        var notice = Notice();
        notice.Should().StartWith("not every command", "the fixture is the real notice");

        Resolve(notice, notice).IsSatisfied.Should().BeFalse(
            "a notice reports that evidence is missing; it is not evidence");
    }

    [Fact]
    public void Citation_APipelineStep_IsStillCitedByName()
    {
        Resolve("DependencyAuditCommand", "DependencyAuditCommand: 0 advisories")
            .IsSatisfied.Should().BeTrue("a step names itself before the colon and is cited by that");
    }
}
