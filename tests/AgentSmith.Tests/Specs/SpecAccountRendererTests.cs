using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0420: the account IS the reviewer's text. Not a confidence score and not a hedge —
/// the claim, itemised, each satisfied criterion pointing at the file that satisfies it,
/// so refuting a line costs opening one file instead of re-deriving the phase.
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
}
