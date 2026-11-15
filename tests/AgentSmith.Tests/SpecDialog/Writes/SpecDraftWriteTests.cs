using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Writes;

public sealed class SpecDraftWriteTests
{
    [Fact]
    public void WriteSpec_WithoutStatedId_TakesTheFileNameId()
    {
        var write = DraftWriteRig.Specs().Write(ProposalDraft.Empty, "store", "goal: \"Widgets are stored\"", false, []);

        write.Written().Specs.Should().ContainSingle().Which.Draft.SpecId.Should().Be("store");
        write.Written().Specs[0].Draft.Yaml.Should().StartWith("spec: store");
    }

    [Fact]
    public void WriteSpec_StatedIdOtherThanTheFileName_IsRefused()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "store", DraftWriteRig.Spec("api"), false, []).Refusal
            .Should().Contain("the file names spec 'store'");
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
    public void WriteSpec_LabelledEpic_OrdersByItsLabels()
    {
        var specs = DraftWriteRig.Specs();
        var draft = specs.Write(ProposalDraft.Empty, "platform", "goal: \"Platform\"", true, []).Written();
        draft = specs.Write(draft, "store", "goal: \"Store\"", false, []).Written();

        var epic = specs.Write(draft, "api", "goal: \"Api\"\nrequires: [store]", false, []).Written();

        epic.Specs.Select(s => s.Draft.SpecId).Should().Equal("platform", "store", "api");
        epic.Specs[2].Draft.Yaml.Should().StartWith("spec: api", "the label is the name the text states until filing");
        specs.Write(epic, "store", "goal: \"Store\"\nrequires: [api]", false, []).Refusal
            .Should().Contain("cannot be put in an order");
        specs.Write(epic, "store", "goal: \"Store\"\nrequires: [platform]", false, []).Refusal
            .Should().Contain("requires the epic's parent 'platform'");
    }

    [Fact]
    public void SpecDraftIds_IsLabel_TellsALabelFromASpecId()
    {
        SpecDraftIds.IsLabel("server-foundation").Should().BeTrue();
        SpecDraftIds.IsLabel("p0001").Should().BeFalse();
        SpecDraftIds.IsLabel("p0001-export").Should().BeFalse();
        SpecDraftIds.IsLabel("2026-11-15-06fca").Should().BeFalse();
        SpecDraftIds.IsLabel("Server Foundation").Should().BeFalse();
    }

    [Fact]
    public void WriteSpec_SchemaInvalid_IsRefusedWithTheSchemaError()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "store", "goal: \"g\"\nscope:\n  invented: \"x\"", false, []).Refusal
            .Should().Contain("it allows in, out");
    }

    [Fact]
    public void WriteSpec_FileNameThatIsNoSpecName_IsRefused()
    {
        DraftWriteRig.Specs().Write(ProposalDraft.Empty, "Export Queue", "goal: \"g\"", false, []).Refusal
            .Should().Contain("is no spec name");
    }
}
