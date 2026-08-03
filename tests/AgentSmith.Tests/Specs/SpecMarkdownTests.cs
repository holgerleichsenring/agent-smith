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
    public void Render_PhaseWithoutDocument_NamesTheLookedUpPathInsteadOfABlankSection()
    {
        var markdown = SpecMarkdown.Render(Set(Phase(document: "")));

        markdown.Should().Contain("No phase document found");
        markdown.Should().Contain(".agentsmith/specs/azdo-19106/p19106a-rename.md");
    }

    private static SpecPhase Phase(IReadOnlyList<string>? done = null, string document = "")
        => new(
            new PhaseDraft("p19106a", "Rename the call sites", "phase: p19106a", [])
            {
                Done = done ?? [],
            },
            "rename", document, []);

    private static SpecSet Set(SpecPhase phase) => new(
        "azdo-19106", [phase], SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived, TicketPinnedWhole: true);
}
