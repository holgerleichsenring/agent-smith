using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Services.SpecRename;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>
/// 2026-10-12-967ab: the rewrite reads the readers' own tables — the [AcceptedName] attributes and
/// <see cref="CheckpointRenames"/> — and the few names it has to spell itself are pinned to the
/// reader that owns them.
/// </summary>
public sealed class SpecRenameTableTests
{
    [Fact]
    public void AcceptedNameRewriter_OldMembersDownTheTree_AreDeclaredAndAnUnrelatedMemberStays()
    {
        var node = JsonNode.Parse("""{"Phases":[{"PhaseId":"s1","Goal":"g","State":2,"Phase":"keep"}]}""")!;

        AcceptedNameRewriter.Rewrite(node, typeof(SpecSequenceProgress), policy: null).Should().BeTrue();

        node.ToJsonString().Should().Be("""{"Specs":[{"Goal":"g","State":2,"Phase":"keep","SpecId":"s1"}]}""");
        AcceptedNameRewriter.Rewrite(node, typeof(SpecSequenceProgress), policy: null).Should().BeFalse();
    }

    [Fact]
    public void AcceptedNameRewriter_AnEnumStoredByItsOldName_KeepsItsCasing()
    {
        var node = JsonNode.Parse("""[{"kind":"phaseProposal"},{"kind":"answer"}]""")!;

        AcceptedNameRewriter.Rewrite(node[0]!["kind"], typeof(SpecDialogTurnKind?), JsonNamingPolicy.CamelCase).Should().BeTrue();

        node.ToJsonString().Should().Be("""[{"kind":"specProposal"},{"kind":"answer"}]""");
    }

    [Fact]
    public void CheckpointRenames_Rewrite_NamesTheSpecSideOrNothing()
    {
        CheckpointRenames.Rewrite("PhaseSpec", "AgentSmith.Contracts.Models.PhaseDraft, AgentSmith.Contracts")
            .Should().Be(("CurrentSpec", "AgentSmith.Contracts.Models.SpecDraft, AgentSmith.Contracts"));
        CheckpointRenames.Rewrite("CurrentSpec", typeof(SpecDraft).AssemblyQualifiedName!)
            .Should().Be(((string?)null, (string?)null));
    }

    [Fact]
    public void SpecDialogKinds_AreTheOnesTheServerReadsAndWrites()
    {
        SpecDialogJsonRewriter.SpecKind.Should().Be(SpecDialogProposalComposer.SpecKind);
        SpecDialogJsonRewriter.OldSpecKind.Should().Be(SpecDialogProposalComposer.AcceptedSpecKind);
    }
}
