using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-13-8762n: a build note against the series contract — a mechanical hint, shown and never enforced.</summary>
public sealed class BuildNoteContractCheckTests
{
    private const string A = "2026-10-14-5678a";
    private const string B = "2026-10-14-5678b";
    private const string C = "2026-10-14-5678c";

    private static SpecSet Set(SeriesContract contract, IReadOnlyList<string>? repoA = null, IReadOnlyList<string>? repoB = null) =>
        new SpecSet("2026-10-14-5678", [Spec(A, repoA ?? ["shop"]), Spec(B, repoB ?? ["shop"]), Spec(C, ["shop"])],
            SpecAccounting.Empty, [new SpecRevision(1, "initial derivation", DateTimeOffset.UnixEpoch)], SpecSource.BranchArtifact)
        { Contract = contract };

    private static DerivedSpec Spec(string id, IReadOnlyList<string> repos) =>
        new(new SpecDraft(id, "goal", $"spec: {id}", []) { Repos = repos }, "label", string.Empty, []);

    private static SeriesContract Contract(
        IReadOnlyList<SeriesContractName>? names = null, IReadOnlyList<SeriesContractDatum>? data = null,
        IReadOnlyList<SeriesContractArea>? areas = null) => new(names ?? [], data ?? [], areas ?? [], []);

    private static SpecBuildNote Note(
        IReadOnlyList<string>? renamed = null, IReadOnlyList<string>? moved = null,
        IReadOnlyList<string>? persisted = null, IReadOnlyList<string>? deviations = null) =>
        new(B, renamed ?? [], moved ?? [], persisted ?? [], deviations ?? []);

    private static readonly SeriesContract Owned = Contract(data: [new SeriesContractDatum("RunDefaults", "table", A)]);

    [Fact]
    public void BuildNoteContractCheck_PersistedDatumOwnedByAnotherSpec_IsADeviation()
    {
        var found = BuildNoteContractCheck.Deviations(Note(persisted: ["table rundefaults with two columns"]), B, Set(Owned));

        found.Should().ContainSingle().Which.Should().BeEquivalentTo(new BuildNoteDeviation(
            "table rundefaults with two columns", $"persists 'RunDefaults', which the contract gives to {A}", false));
        BuildNoteContractCheck.Deviations(Note(persisted: ["table RunDefaultsArchive"]), B, Set(Owned))
            .Should().BeEmpty("a name matches by whole word only");
    }

    [Fact]
    public void BuildNoteContractCheck_PersistedDatumListingThisSpecAsWriter_IsNoDeviation()
    {
        var shared = Contract(data: [new SeriesContractDatum("RunDefaults", "table", A, [B])]);

        BuildNoteContractCheck.Deviations(Note(persisted: ["table RunDefaults"]), B, Set(shared)).Should().BeEmpty();
    }

    [Fact]
    public void BuildNoteContractCheck_MoveIntoAnotherSpecsAreaOnly_IsADeviation()
    {
        var areas = Contract(areas: [new SeriesContractArea(A, ["src/Rules/"]), new SeriesContractArea(B, ["src/Api/"])]);

        BuildNoteContractCheck.Deviations(Note(moved: ["src/Api/Orders.cs -> src/Rules/Orders.cs"]), B, Set(areas))
            .Should().ContainSingle().Which.Reason.Should().Be($"moves into src/Rules/Orders.cs, in the area of {A}");
    }

    [Fact]
    public void BuildNoteContractCheck_MoveIntoAreaSharedWithThisSpec_IsNoDeviation()
    {
        var areas = Contract(areas: [new SeriesContractArea(A, ["src/Rules/"]), new SeriesContractArea(B, ["src/Rules/Orders.cs"])]);

        BuildNoteContractCheck.Deviations(Note(moved: ["src/Api/Orders.cs -> src/Rules/Orders.cs"]), B, Set(areas))
            .Should().BeEmpty("the destination lies in an area of this spec too");
    }

    [Fact]
    public void BuildNoteContractCheck_MoveIntoAreaOfSpecInDisjointRepos_IsNoDeviation()
    {
        var areas = Contract(areas: [new SeriesContractArea(A, ["src/Rules/"])]);

        BuildNoteContractCheck.Deviations(Note(moved: ["src/Api/Orders.cs -> src/Rules/Orders.cs"]), B, Set(areas, ["web"], ["shop"]))
            .Should().BeEmpty("areas are repository-relative; another repository's area is another tree");
    }

    [Fact]
    public void BuildNoteContractCheck_RenamedContractName_IsADeviation()
    {
        var names = Contract(names: [new SeriesContractName("SpecReviewRoundEvent", "event", 87, A)]);

        BuildNoteContractCheck.Deviations(Note(renamed: ["SpecReviewRoundEvent -> ReviewRoundEvent"]), B, Set(names))
            .Should().ContainSingle().Which.Reason.Should().Contain("renames the contract's event 'SpecReviewRoundEvent'");
        BuildNoteContractCheck.Deviations(Note(renamed: ["OldThing -> SpecReviewRoundEvent"]), B, Set(names))
            .Should().BeEmpty("only the old side names what was renamed away");
    }

    [Fact]
    public void BuildNoteContractCheck_DeclaredInDeviations_IsMarkedDeclared()
    {
        var note = Note(persisted: ["table RunDefaults"], deviations: ["wrote RunDefaults because A's migration was missing"]);

        BuildNoteContractCheck.Deviations(note, B, Set(Owned)).Should().ContainSingle().Which.Declared.Should().BeTrue();
    }

    [Fact]
    public void SpecBuildNoteSection_WithSet_MarksUndeclaredDeviations_WithoutSet_Unmarked()
    {
        PrecedingNote[] notes = [new(B, "done/b.yaml", Note(persisted: ["table RunDefaults"]))];

        SpecBuildNoteSection.Render(notes, set: Set(Owned)).Should().Contain(
            $"- {SpecBuildNoteSection.Undeclared}: \"table RunDefaults\" persists 'RunDefaults', which the contract gives to {A}");
        SpecBuildNoteSection.Render(notes).Should().NotContain(SpecBuildNoteSection.Undeclared);
    }

    [Fact]
    public void HandForward_ConflictWithContract_AnswerNamesIt()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, Set(Owned));

        var answer = new HandForwardToolHost(pipeline, B).HandForward(persisted: ["table RunDefaults"]);

        answer.Should().StartWith($"Build note for {B} recorded").And.Contain("conflicts with the series contract")
            .And.Contain($"\"table RunDefaults\" persists 'RunDefaults', which the contract gives to {A} (not declared in your deviations)");
    }
}
