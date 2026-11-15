using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

public sealed class SpecDraftWriteTests
{
    [Fact]
    public void WriteSpec_WithoutStatedId_TakesTheFileNameId()
    {
        var write = DraftWriteRig.Specs().Write(ProposalDraft.Empty, "p0101", "goal: \"Widgets are stored\"", false, []);

        write.Written().Specs.Should().ContainSingle().Which.Draft.SpecId.Should().Be("p0101");
        write.Written().Specs[0].Draft.Yaml.Should().StartWith("spec: p0101");
    }

    [Fact]
    public void WriteSpec_StatedIdOtherThanTheFileName_IsRefused()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "p0101", DraftWriteRig.Spec("p0102"), false, []).Refusal
            .Should().Contain("the file names spec 'p0101'");
    }

    [Fact]
    public void WriteSpec_SameIdAgain_RewritesIt()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "p9001", DraftWriteRig.Spec("p9001", "Old"), false, []).Written();

        var rewritten = specs.Write(draft, "p9001", DraftWriteRig.Spec("p9001", "New"), false, []).Written();

        rewritten.Specs.Should().ContainSingle().Which.Draft.Goal.Should().Be("New");
    }

    [Fact]
    public void WriteSpec_SecondRootWithoutParent_IsRefused()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "p9001", DraftWriteRig.Spec("p9001"), false, []).Written();

        specs.Write(draft, "p9002", DraftWriteRig.Spec("p9002"), false, []).Refusal
            .Should().Contain("write its parent as draft:epic/<id>.yaml first");
    }

    [Fact]
    public void WriteSpec_ParentThenChildren_FormsAnEpic()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "p9000", DraftWriteRig.Spec("p9000"), true, []).Written();
        draft = specs.Write(draft, "p9000a", DraftWriteRig.Spec("p9000a"), false, []).Written();

        var epic = specs.Write(draft, "p9000b", DraftWriteRig.Spec("p9000b", "B", "p9000a"), false, []).Written();

        epic.Specs.Select(s => (s.Draft.SpecId, s.Parent)).Should().Equal(("p9000", true), ("p9000a", false), ("p9000b", false));
    }

    [Fact]
    public void WriteSpec_ChildRequiringNoSpecOfTheDraft_SaysWhichFileToWrite()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "p9000", DraftWriteRig.Spec("p9000"), true, []).Written();

        specs.Write(draft, "p9000a", DraftWriteRig.Spec("p9000a", "A", "p9000z"), false, []).Refusal
            .Should().Contain("write draft:specs/p9000z.yaml first");
    }

    [Fact]
    public void WriteSpec_SecondParent_IsRefused()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "p9000", DraftWriteRig.Spec("p9000"), true, []).Written();

        specs.Write(draft, "p9100", DraftWriteRig.Spec("p9100"), true, []).Refusal
            .Should().Contain("draft:epic/p9000.yaml is the epic's parent already");
    }

    [Fact]
    public void WriteSpec_IdUnderTheOtherDirectory_IsRefused()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "p9000", DraftWriteRig.Spec("p9000"), false, []).Written();

        specs.Write(draft, "p9000", DraftWriteRig.Spec("p9000"), true, []).Refusal
            .Should().Contain("draft:specs/p9000.yaml holds 'p9000' already");
    }

    [Fact]
    public void WriteSpec_SchemaInvalid_IsRefusedWithTheSchemaError()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "p0101", "goal: \"g\"\nscope:\n  invented: \"x\"", false, []).Refusal
            .Should().Contain("it allows in, out");
    }

    [Fact]
    public void WriteSpec_FileNameThatIsNoSpecId_IsRefused()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "2026-xx-xx-0000", "goal: \"g\"", false, []).Refusal
            .Should().Contain("is no spec id");
    }
}
