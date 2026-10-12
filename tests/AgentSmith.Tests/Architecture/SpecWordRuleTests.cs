using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-12-967ag: the old noun for a unit of planned work does not come back.
/// <para>
/// 967ac to 967af renamed phase to spec wherever it meant the spec. What still says phase
/// is either another meaning — the execution stage of a model call, a skill's workflow step,
/// a Kubernetes pod phase, the triage stages — or a name the stored data and the readers of
/// older data still carry until 967ab and 967ah. <see cref="SpecWordAllowlist"/> names each
/// of those; every other token that says phase fails here, on the day it is written.
/// </para>
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SpecWordRuleTests
{
    [Fact]
    public void SpecWord_PhaseOutsideAllowlist_Fails()
    {
        var written = SpecWordFile.Of(
            "src/backend/AgentSmith.Contracts/Specs/SpecSummaryView.cs",
            "public sealed record SpecSummaryView(string PhaseId, SkillExecutionPhase Stage, string PhaseTitle);");

        SpecWordAllowlist.Load().Offenders([written]).Should().BeEquivalentTo(
        [
            "src/backend/AgentSmith.Contracts/Specs/SpecSummaryView.cs: PhaseId — allowed only in the files spec-word-files.tsv lists",
            "src/backend/AgentSmith.Contracts/Specs/SpecSummaryView.cs: PhaseTitle — not allowlisted",
        ], "a new file may not name the spec id PhaseId, nor coin a phase word; the execution stage stays");
    }

    [Fact]
    public void SpecWord_AListedTokenBeyondItsCount_Fails()
    {
        var allowlist = SpecWordAllowlist.Load();
        var ((path, token), allowed) = allowlist.FileRows.First();
        var grown = new SpecWordFile(path, new Dictionary<string, int> { [token] = allowed + 1 });

        allowlist.Offenders([grown]).Should().ContainSingle(
            "a listed file may keep the word it has, not gain another");
    }

    [Fact]
    public void SpecWord_Tree_IsClean()
    {
        var offenders = SpecWordAllowlist.Load().Offenders(SpecWordScope.Files()).ToList();

        offenders.Should().BeEmpty(
            "phase was renamed to spec. Write spec; if the word means another thing (an execution "
            + "stage, a skill workflow step, a pod phase), add the token to spec-word-allowlist.tsv "
            + "with its reason.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void SpecWord_Allowlist_NamesNothingThatIsGone()
    {
        var stale = SpecWordAllowlist.Load().Stale(SpecWordScope.Files()).ToList();

        stale.Should().BeEmpty(
            "a row whose token left the tree must leave the list, so the list keeps saying what is "
            + "left.\n  " + string.Join("\n  ", stale));
    }

    [Theory]
    [InlineData(".agentsmith/specs/done/p0001-first.yaml", false)]
    [InlineData(".agentsmith/decisions/p0001.yaml", false)]
    [InlineData(".agentsmith/contexts/default/context.yaml", false)]
    [InlineData(".agentsmith/contexts/default/principles.md", true)]
    [InlineData("src/backend/AgentSmith.Infrastructure.Persistence.Sqlite/Migrations/X.cs", false)]
    [InlineData("docs/reference/run-logs/run.md", false)]
    [InlineData("CHANGELOG.md", false)]
    [InlineData("src/dashboard/src/App.tsx", true)]
    public void SpecWord_Scope_ReadsTheTreeAndNotItsHistory(string path, bool read) =>
        SpecWordScope.InScope(path).Should().Be(read);
}
