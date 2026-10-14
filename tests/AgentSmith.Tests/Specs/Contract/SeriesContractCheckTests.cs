using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Contract.SeriesContractFixtures;

namespace AgentSmith.Tests.Specs.Contract;

/// <summary>
/// 2026-10-13-8762j: a series contract collides on one owner per thing, one number per (kind, number), one
/// budget per (path, member) and a shared file without every claimant's budget — never on overlap itself.
/// </summary>
public sealed class SeriesContractCheckTests
{
    private const string Shared = "src/Shared.cs";
    private readonly SeriesContractCheck _check = new();

    [Fact]
    public void SeriesContractCheck_DatumListedTwice_IsDuplicateOwner()
    {
        var contract = Of(data: [new("the verdict", "Verdicts", Id('a')), new("The verdict ", "Verdicts", Id('a'))]);

        _check.Problems(contract, Members("ab")).Should().Contain("datum 'the verdict' is claimed by 2 owner entries");
    }

    [Fact]
    public void SeriesContractCheck_NameWithTwoOwners_IsRefused()
    {
        var contract = Of(names: [new("Verdicts", "table", null, Id('a')), new("verdicts", "TABLE", null, Id('b'))]);

        _check.Problems(contract, Members("ab")).Should().Contain("table 'Verdicts' is claimed by 2 owner entries");
    }

    [Fact]
    public void SeriesContractCheck_TwoOwnerClaims_AreRefused()
    {
        var contract = Of(data: [new("rounds", "Rounds", Id('a')), new("rounds", "Rounds", Id('b'))]);

        _check.Problems(contract, Members("ab")).Should().Contain("name one owner and the others as writers");
    }

    [Fact]
    public void SeriesContractCheck_OwnerPlusWriters_Passes()
    {
        var contract = Of(
            names: [new("RunDefaultDecisions", "table", null, Id('a'), [Id('b')])],
            data: [new("the verdict", "RunDefaultDecisions", Id('a'), [Id('b'), Id('c')])]);

        _check.Problems(contract, Members("abc")).Should().BeNull();
    }

    [Fact]
    public void SeriesContractCheck_SameNumberSameKind_Collides()
    {
        var contract = Of(names: [new("SpecReviewRound", "event", 87, Id('a')), new("DefaultDecisionTaken", "event", 87, Id('b'))]);

        _check.Problems(contract, Members("ab")).Should().Contain("event 87 is given to 'SpecReviewRound' and 'DefaultDecisionTaken'");
    }

    [Fact]
    public void SeriesContractCheck_SameNumberOtherKind_Passes()
    {
        var contract = Of(names: [new("SpecReviewRound", "event", 87, Id('a')), new("SeriesHeld", "system-event", 87, Id('b'))]);

        _check.Problems(contract, Members("ab")).Should().BeNull();
    }

    [Fact]
    public void SeriesContractCheck_KindCaseInsensitive_EventAndEVENTCollide()
    {
        var contract = Of(names: [new("A", "event", 87, Id('a')), new("B", "EVENT", 87, Id('b'))]);

        _check.Problems(contract, Members("ab")).Should().Contain("event 87 is given to 'A' and 'B'");
    }

    [Fact]
    public void SeriesContractCheck_UnknownKind_IsRefused()
    {
        var contract = Of(names: [new("A", "events", 87, Id('a'))]);

        _check.Problems(contract, Members("a")).Should().Contain("kind 'events', which is not one of event, system-event");
    }

    [Fact]
    public void SeriesContractCheck_OwnerNotAMember_IsRefused()
    {
        var contract = Of(data: [new("rounds", "Rounds", Id('z'), [Id('a')])]);

        _check.Problems(contract, Members("a")).Should().Contain($"datum 'rounds' names owner '{Id('z')}', which is not a spec of this set");
    }

    [Fact]
    public void SeriesContractCheck_SharedFileWithEveryClaimantsBudget_Passes()
    {
        var contract = Of(areas: [new(Id('a'), [Shared]), new(Id('b'), [Shared])],
            budgets: [new(Shared, Id('a'), 100), new(Shared, Id('b'), 110)]);

        _check.Problems(contract, Members("ab")).Should().BeNull();
    }

    [Fact]
    public void SeriesContractCheck_SharedFileMissingOneClaimantsBudget_IsRefused()
    {
        var contract = Of(areas: [new(Id('a'), [Shared]), new(Id('b'), [Shared])], budgets: [new(Shared, Id('a'), 100)]);

        _check.Problems(contract, Members("ab")).Should().Contain($"{Shared} is claimed by {Id('a')}, {Id('b')} without a line budget for {Id('b')}");
    }

    [Fact]
    public void SeriesContractCheck_FileUnderClaimedDirectory_CountsAsShared()
    {
        var contract = Of(areas: [new(Id('a'), ["src/"]), new(Id('b'), [Shared])], budgets: [new(Shared, Id('b'), 100)]);

        _check.Problems(contract, Members("ab")).Should().Contain($"without a line budget for {Id('a')}");
    }

    [Fact]
    public void SeriesContractCheck_SamePathInTwoRepositories_IsNotShared()
    {
        var contract = Of(areas: [new(Id('a'), ["package.json"]), new(Id('b'), ["package.json"])]);
        IReadOnlyList<(string, IReadOnlyList<string>)> members = [(Id('a'), ["web"]), (Id('b'), ["api"])];
        IReadOnlyList<(string, IReadOnlyList<string>)> unplaced = [(Id('a'), ["web"]), (Id('b'), [])];

        _check.Problems(contract, members).Should().BeNull("two repositories' package.json are two files");
        _check.Problems(contract, unplaced).Should().Contain("package.json", "a member not placed yet intersects every member");
    }

    [Fact]
    public void SeriesContractCheck_LaterMemberLowerBudget_Passes()
    {
        var contract = Of(areas: [new(Id('a'), [Shared]), new(Id('b'), [Shared])],
            budgets: [new(Shared, Id('a'), 747), new(Shared, Id('b'), 700)]);

        _check.Problems(contract, Members("ab")).Should().BeNull("a later member may lower a file by extraction");
    }

    [Fact]
    public void SeriesContractCheck_BudgetTwoNumbersForOnePathAndSpec_IsRefused()
    {
        var contract = Of(budgets: [new(Shared, Id('a'), 100), new("./" + Shared, Id('a'), 110), new(Shared, Id('b'), 0)]);

        var problems = _check.Problems(contract, Members("ab"));

        problems.Should().Contain($"{Shared} has two budgets for {Id('a')} (100, 110)");
        problems.Should().Contain($"the budget of {Shared} for {Id('b')} is 0, below 1");
    }

    [Fact]
    public void SeriesContractCheck_ThreeProblems_AllNamedInOneMessage()
    {
        var contract = Of(
            names: [new("A", "event", 87, Id('a')), new("B", "event", 87, Id('b')), new("C", "widget", null, Id('a'))],
            data: [new("rounds", "Rounds", Id('q'))]);

        var problems = _check.Problems(contract, Members("ab"));

        problems.Should().StartWith("the series contract collides: ")
            .And.Contain("event 87 is given to").And.Contain("kind 'widget'").And.Contain($"owner '{Id('q')}'");
    }

    [Fact]
    public void SeriesContractCheck_Series8762OwnContract_Passes()
    {
        _check.Problems(Series8762(), Members("abcdefghijklmnopqrstuvwx", "agent-smith")).Should().BeNull(
            "the check must not refuse the contract it imitates");
    }

    [Fact]
    public void SeriesContract_Within_RemovesEntriesNamingNonMembers()
    {
        var contract = Of(
            names: [new("A", "event", 87, Id('a'), [Id('b'), Id('c')]), new("B", "event", 88, Id('b'))],
            areas: [new(Id('a'), [Shared]), new(Id('b'), [Shared])], budgets: [new(Shared, Id('b'), 10)]);

        var within = contract.Within([Id('a'), Id('c')]);

        within.Contract!.Names.Should().ContainSingle().Which.Writers.Should().Equal(Id('c'));
        within.Contract.Areas.Should().ContainSingle().Which.Spec.Should().Be(Id('a'));
        within.Contract.Budgets.Should().BeEmpty();
        within.Removed.Should().HaveCount(4);
        contract.Within([Id('z')]).Contract.Should().BeNull("nothing is left of it");
    }

    [Fact]
    public void SeriesContract_Rekeyed_MapsOwnersAndAreas()
    {
        var contract = Of(
            names: [new("A", "event", 87, "c-1", ["c-2"])], data: [new("d", "T", "c-2")],
            areas: [new("c-1", [Shared])], budgets: [new(Shared, "c-1", 10), new(Shared, "other", 20)]);
        var map = new Dictionary<string, string> { ["c-1"] = Id('a'), ["c-2"] = Id('b') };

        var rekeyed = contract.Rekeyed(map);

        rekeyed.Names[0].Should().Be(new SeriesContractName("A", "event", 87, Id('a'), null) with { Writers = rekeyed.Names[0].Writers });
        rekeyed.Names[0].Writers.Should().Equal(Id('b'));
        rekeyed.Data[0].Owner.Should().Be(Id('b'));
        rekeyed.Areas[0].Spec.Should().Be(Id('a'));
        rekeyed.Budgets.Select(b => b.Spec).Should().Equal(Id('a'), "other");
    }
}
