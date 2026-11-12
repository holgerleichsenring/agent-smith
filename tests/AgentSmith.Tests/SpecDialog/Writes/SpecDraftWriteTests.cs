using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

public sealed class SpecDraftWriteTests
{
    [Fact]
    public void WriteSpec_WithoutId_MintsPlaceholder()
    {
        var write = DraftWriteRig.Specs().Write(ProposalDraft.Empty, "goal: \"Widgets are stored\"", false, []);

        write.Handle.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}-[0-9a-f]{4}$");
        write.Written().Specs.Should().ContainSingle().Which.Draft.SpecId.Should().Be(write.Handle);
        write.Written().Specs[0].Draft.Yaml.Should().StartWith($"spec: {write.Handle}");
    }

    [Fact]
    public void WriteSpec_StatedIdOfTheDraft_RewritesIt()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, DraftWriteRig.Spec("p9001", "Old"), false, []).Written();

        var rewritten = specs.Write(draft, DraftWriteRig.Spec("p9001", "New"), false, []).Written();

        rewritten.Specs.Should().ContainSingle().Which.Draft.Goal.Should().Be("New");
    }

    [Fact]
    public void WriteSpec_SecondRootWithoutParent_IsRefused()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, DraftWriteRig.Spec("p9001"), false, []).Written();

        specs.Write(draft, DraftWriteRig.Spec("p9002"), false, []).Refusal
            .Should().Contain("write its parent with parent: true");
    }

    [Fact]
    public void WriteSpec_ParentThenChildren_FormsAnEpic()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, DraftWriteRig.Spec("p9000"), true, []).Written();
        draft = specs.Write(draft, DraftWriteRig.Spec("p9000a"), false, []).Written();

        var epic = specs.Write(draft, DraftWriteRig.Spec("p9000b", "B", "p9000a"), false, []).Written();

        epic.Specs.Select(s => (s.Draft.SpecId, s.Parent)).Should().Equal(("p9000", true), ("p9000a", false), ("p9000b", false));
    }

    [Fact]
    public void WriteSpec_ChildRequiringNoSibling_IsRefused()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, DraftWriteRig.Spec("p9000"), true, []).Written();

        specs.Write(draft, DraftWriteRig.Spec("p9000a", "A", "p9000z"), false, []).Refusal
            .Should().Contain("which is not a sibling");
    }

    [Fact]
    public void WriteSpec_SecondParent_IsRefused()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, DraftWriteRig.Spec("p9000"), true, []).Written();

        specs.Write(draft, DraftWriteRig.Spec("p9100"), true, []).Refusal.Should().Contain("is the parent already");
    }

    [Fact]
    public void WriteSpec_SchemaInvalid_IsRefusedWithTheSchemaError()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "goal: \"g\"\nscope:\n  invented: \"x\"", false, []).Refusal
            .Should().Contain("it allows in, out");
    }

    [Fact]
    public void WriteSpec_IdThatIsNoSpecId_IsRefused()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "spec: 2026-xx-xx-0000\ngoal: \"g\"", false, []).Refusal
            .Should().Contain("is no spec id");
    }
}
