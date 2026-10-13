using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.SpecRename;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>
/// 2026-10-12-967ah: the frozen table the store rewrite alone reads. Its old side is history; its
/// new side must still name what this release declares, or the rewrite would write a name no
/// reader knows.
/// </summary>
public sealed class SpecRenameTableTests
{
    private readonly SpecMemberRewriter _members = new();

    [Fact]
    public void SpecMemberRewriter_OldMembersDownTheTree_AreRenamedAndAnUnrelatedMemberStays()
    {
        var node = JsonNode.Parse("""{"Phases":[{"PhaseId":"s1","Goal":"g","State":2,"Phase":"keep"}]}""")!;

        _members.Rewrite(node, typeof(SpecSequenceProgress), policy: null).Should().BeTrue();

        node.ToJsonString().Should().Be("""{"Specs":[{"Goal":"g","State":2,"Phase":"keep","SpecId":"s1"}]}""");
        _members.Rewrite(node, typeof(SpecSequenceProgress), policy: null).Should().BeFalse();
    }

    [Fact]
    public void SpecMemberRewriter_AnEnumStoredByItsOldName_KeepsItsCasing()
    {
        var node = JsonNode.Parse("""[{"kind":"phaseProposal"},{"kind":"answer"}]""")!;

        _members.Rewrite(node[0]!["kind"], typeof(SpecDialogTurnKind?), JsonNamingPolicy.CamelCase).Should().BeTrue();

        node.ToJsonString().Should().Be("""[{"kind":"specProposal"},{"kind":"answer"}]""");
    }

    [Fact]
    public void CheckpointEntryNames_Rewrite_NamesTheCurrentSideOrNothing()
    {
        var names = new CheckpointEntryNames();

        names.Rewrite("PhaseSpec", "AgentSmith.Contracts.Models.PhaseDraft, AgentSmith.Contracts")
            .Should().Be(("CurrentSpec", "AgentSmith.Contracts.Models.SpecDraft, AgentSmith.Contracts"));
        names.Rewrite("CurrentSpec", typeof(SpecDraft).AssemblyQualifiedName!)
            .Should().Be(((string?)null, (string?)null));
    }

    [Fact]
    public void SpecRenameTable_EveryRenamedMember_IsDeclaredUnderItsNewName()
    {
        foreach (var (type, renames) in SpecRenameTable.Members)
            foreach (var rename in renames)
                type.GetProperty(rename.New, BindingFlags.Public | BindingFlags.Instance)
                    .Should().NotBeNull($"{type.Name}.{rename.New} is what the rewrite writes");
        foreach (var (type, renames) in SpecRenameTable.EnumMembers)
            Enum.GetNames(type).Should().Contain(renames.Select(r => r.New));
    }

    [Fact]
    public void SpecRenameTable_EveryNewName_IsOneThisReleaseDeclares()
    {
        var keys = typeof(ContextKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        var commands = typeof(CommandNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();

        keys.Should().Contain(SpecRenameTable.ContextKeys.Select(r => r.New));
        commands.Should().Contain(SpecRenameTable.CommandNames.Select(r => r.New));
        SpecRenameTable.TypeNames.Should().OnlyContain(r => FindType(r.New) != null, "every renamed type still exists");
        Enum.GetNames<EventType>().Should().Contain(SpecRenameTable.EventTypeNames.Select(r => r.New));
        SpecRenameTable.ArtifactPrefixes.Select(r => r.New)
            .Should().Equal(RunSpecProjection.RecordKindPrefix, RunSpecProjection.ReviewKindPrefix);
        SpecRenameTable.SpecKind.Should().Be(SpecDialogProposalComposer.SpecKind);
    }

    [Fact]
    public void SpecRenameTable_CommandName_KeepsAParameterSuffixAndLeavesCurrentNamesAlone()
    {
        var verify = SpecRenameTable.CommandNames.Single(r => r.New == CommandNames.VerifySpec);

        SpecRenameTable.CommandName(verify.Old + ":api").Should().Be(CommandNames.VerifySpec + ":api");
        SpecRenameTable.CommandName(CommandNames.VerifySpec + ":api").Should().BeNull();
    }

    private static Type? FindType(string fullName) =>
        new[] { typeof(SpecDraft).Assembly, typeof(AgentSmith.Domain.Models.RunSpecReviews).Assembly,
                typeof(AgentSmith.Application.Models.CheckpointCommand).Assembly }
            .Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);
}
