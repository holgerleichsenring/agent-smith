using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0420: the account IS the reviewer's text. Not a confidence score and not a hedge —
/// the claim, itemised, each satisfied criterion pointing at the file that satisfies it,
/// so refuting a line costs opening one file instead of re-deriving the spec.
/// </summary>
public sealed class SpecAccountRendererTests
{
    [Fact]
    public void EverySatisfiedCriterion_PointsAtTheFileThatSatisfiesIt()
    {
        var text = SpecAccountRenderer.ToMarkdown([
            new SpecAccount("sample-repo", [
                new CriterionAccount("packages reach their pinned versions", AccountDisposition.Satisfied, "src/Api/Api.csproj"),
                new CriterionAccount("build and test are green", AccountDisposition.Satisfied, Mechanical: true)])]);

        text.Should().Contain("- [x] packages reach their pinned versions — `src/Api/Api.csproj`");
        text.Should().Contain("verified by command", "a green build is evidence of a different kind");
    }

    [Fact]
    public void AnOutstandingCriterion_SaysWhatIsMissing_NotJustThatSomethingIs()
    {
        var text = SpecAccountRenderer.ToMarkdown([
            new SpecAccount("worker-repo", [
                new CriterionAccount("packages reach their pinned versions", AccountDisposition.NotSatisfied,
                    Note: "nothing in the diff touches a manifest")])]);

        text.Should().Contain("- [ ] packages reach their pinned versions");
        text.Should().Contain("nothing in the diff touches a manifest");
    }

    [Fact]
    public void NoAccounts_RenderNothing_RatherThanAnEmptyHeading()
    {
        SpecAccountRenderer.ToMarkdown([]).Should().BeEmpty();
    }

    // 2026-11-18-c8e2d (characterised first): a scan's not-applicable row keeps naming the antecedent its search disproved.
    [Fact]
    public void AScansNotApplicableRow_KeepsTheAntecedentWording()
    {
        var md = SpecAccountRenderer.ToMarkdown([new SpecAccount("scan",
            [new CriterionAccount("No Redis client", AccountDisposition.NotApplicable, "grep -rn Redis", Antecedent: "Redis client")])]);

        md.Should().Contain("- [~] No Redis client — not applicable: the base carries no Redis client (`grep -rn Redis`)");
    }

    [Fact]
    public void AMastersReport_ShowsItsWhyFixedInPassingAndFollowUps()
    {
        var md = SpecAccountRenderer.ToMarkdown([new SpecAccount("api",
            [new CriterionAccount("Legacy DTO stays", AccountDisposition.NotApplicable, Note: "no DTO in this repository"),
             new CriterionAccount("Exports stream CSV", AccountDisposition.NotSatisfied, Note: "needs paging; follow-up")],
            FixedInPassing: ["a null check"], FollowUps: [new MasterFollowUp("Exports stream CSV over paging", "paging first")])]);

        md.Should().Contain("- [~] Legacy DTO stays — not applicable: no DTO in this repository")
            .And.Contain("- [ ] Exports stream CSV — needs paging; follow-up")
            .And.Contain("Fixed in passing: a null check")
            .And.Contain("Proposed follow-up: Exports stream CSV over paging — paging first");
    }
}
