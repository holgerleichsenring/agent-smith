using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0395: the viewer's copy of the spec set. Done-criteria render as a titled
/// "Definition of done" list (the raw yaml key used to leak as a literal
/// "done:" prefix on every line), and each phase's markdown companion is part
/// of the copy — present phases render the server-held document, absent ones
/// name the path that was looked up instead of a silently blank section.
/// </summary>
public sealed class SpecMarkdownTests
{
    [Fact]
    public void Render_DoneCriteria_AreATitledListWithoutTheRawYamlKey()
    {
        var markdown = SpecMarkdown.Render(Set(Phase(done: ["Every call site is renamed."])));

        markdown.Should().Contain("Definition of done:");
        markdown.Should().Contain("- Every call site is renamed.");
        markdown.Should().NotContain("done: Every call site is renamed.");
    }

    [Fact]
    public void Render_PhaseWithDocument_IncludesTheServerHeldCopy()
    {
        var markdown = SpecMarkdown.Render(
            Set(Phase(document: "## Carried segments\nRename `IFoo` to `IBar`.")));

        markdown.Should().Contain("## Phase documents");
        markdown.Should().Contain("### p19106a — `p19106a-rename.md`");
        markdown.Should().Contain("Rename `IFoo` to `IBar`.");
    }

    [Fact]
    public void Render_DerivedPhaseWithoutDocument_NamesTheLookedUpPath()
    {
        var markdown = SpecMarkdown.Render(Set(Phase(document: "")));

        markdown.Should().Contain("No phase document found");
        markdown.Should().Contain(".agentsmith/specs/planned/p19106a-rename.md");
    }

    // 2026-10-08-e8b9f: a set approved in a named design conversation has no companion by construction.
    [Fact]
    public void Render_ApprovedPhaseWithoutDocument_SaysApprovedNotNotFound()
    {
        var markdown = SpecMarkdown.Render(Set(Phase()) with
        {
            Source = SpecSource.Approved, Approval = new SpecApproval(DateTimeOffset.UnixEpoch, "job-1", "person"),
        });

        markdown.Should().Contain("Approved in the design conversation");
        markdown.Should().NotContain("No phase document found");
    }

    [Fact]
    public void Render_BranchReadApprovedSet_SaysApprovedNotNotFound()
    {
        var markdown = SpecMarkdown.Render(Set(Phase()) with
        {
            Source = SpecSource.BranchArtifact, Approval = new SpecApproval(DateTimeOffset.UnixEpoch, "job-1", "person"),
        });

        markdown.Should().Contain("Approved in the design conversation");
        markdown.Should().NotContain("No phase document found");
    }

    // A ticket-demanded re-cut records an approval with no conversation on a model-cut set,
    // whose companions are never blank — blank there is a real failure.
    [Fact]
    public void Render_DemandRecutSetWithoutDocument_NamesTheLookedUpPath()
    {
        var markdown = SpecMarkdown.Render(Set(Phase()) with
        {
            Source = SpecSource.BranchArtifact, Approval = new SpecApproval(DateTimeOffset.UnixEpoch, string.Empty, "author"),
        });

        markdown.Should().Contain("No phase document found");
        markdown.Should().Contain(".agentsmith/specs/planned/p19106a-rename.md");
        markdown.Should().NotContain("Approved in the design conversation");
    }

    // 2026-09-07-c9d4: the run detail shows a question the way the ticket does — both
    // readings, and which one the run takes if nobody answers.
    [Fact]
    public void Render_QuestionHandback_ListsTheReadingsAndMarksTheTakenOne()
    {
        var set = Set(Phase()) with
        {
            Phases = [],
            Handback = new SpecHandback(
                SpecHandbackCase.Question, "reads two ways",
                Readings: ["only where an advisory forces it", "everywhere"], Taken: 1),
        };

        var markdown = SpecMarkdown.Render(set);

        markdown.Should().Contain("## Handed back — Question");
        markdown.Should().Contain("- (a) only where an advisory forces it");
        markdown.Should().Contain("- (b) everywhere _(taken if nobody answers)_");
    }

    private static SpecPhase Phase(IReadOnlyList<string>? done = null, string document = "")
        => new(
            new PhaseDraft("p19106a", "Rename the call sites", "spec: p19106a", [])
            {
                Done = done ?? [],
            },
            "rename", document, []);

    private static SpecSet Set(SpecPhase phase) => new(
        "azdo-19106", [phase], SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived, TicketPinnedWhole: true);
}
