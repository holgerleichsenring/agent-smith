using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Contract.SeriesContractFixtures;

namespace AgentSmith.Tests.Specs.Contract;

/// <summary>
/// 2026-10-13-8762j: a derivation reply names members by position, or by id only for an executed spec; an epic
/// block names children by id or position. A '*' in a path is refused.
/// </summary>
public sealed class SeriesContractReaderTests
{
    [Fact]
    public void SeriesContractReader_ReplyPositionAndId_ResolveToBuiltIds()
    {
        var built = Built("abc");

        var json = $$$"""
            {"contract": {"names": [{"name": "SpecReviewRound", "kind": "event", "number": 87, "owner": 2, "writers": ["{{{Id('a')}}}", "3"]}],
             "areas": [{"spec": 3, "paths": ["src/A.cs"]}], "budgets": [{"path": "src/A.cs", "spec": "3", "max_lines": 90}]}}
            """;

        var read = Read(json, built, head: built.Take(1).ToList());

        read.Problem.Should().BeNull();
        read.Contract!.Names[0].Owner.Should().Be(Id('b'));
        read.Contract.Names[0].Writers.Should().Equal(Id('a'), Id('c'));
        read.Contract.Areas[0].Spec.Should().Be(Id('c'));
        read.Contract.Budgets[0].Should().Be(new SeriesContractBudget("src/A.cs", Id('c'), 90));
    }

    [Fact]
    public void SeriesContractReader_ReplyNamesUnexecutedOldId_IsRejected()
    {
        var built = Built("ab");

        var read = Read($$$"""{"contract": {"data": [{"datum": "rounds", "lives_in": "Rounds", "owner": "{{{Id('b')}}}"}]}}""",
            built, head: built.Take(1).ToList());

        read.Contract.Should().BeNull();
        read.Problem.Should().Contain($"member '{Id('b')}'").And.Contain("only an executed spec keeps its id");
    }

    [Fact]
    public void SeriesContractReader_WildcardPath_IsRefused()
    {
        var read = Read("""{"contract": {"areas": [{"spec": 1, "paths": ["src/**/*.cs"]}]}}""", Built("a"), head: []);

        read.Problem.Should().Contain("path 'src/**/*.cs' holds a '*'");
    }

    [Fact]
    public void SeriesContractReader_NoBlock_ReadsNothing()
    {
        Read("""{"specs": []}""", Built("a"), head: []).Should().Be(SeriesContractRead.None);
    }

    [Fact]
    public void SeriesContractReader_OutcomeMap_ReadsEverySection()
    {
        var map = OutcomeYamlReader.ReadMap("""
            kind: epic
            contract:
              names:
                - {name: SpecReviewRound, kind: event, number: 87, owner: c-1, writers: [c-2]}
              data:
                - {datum: rounds, lives_in: RunSpecReviewRounds, owner: 2}
              areas:
                - {spec: c-1, paths: [src/A.cs, src/Review/]}
                - {spec: c-2, paths: [src/A.cs]}
              budgets:
                - {path: src/A.cs, spec: c-1, max_lines: 100}
                - {path: src/A.cs, spec: 2, max_lines: 110}
            """);

        var read = SeriesContracts.Reader().FromOutcome(map, [Child("c-1"), Child("c-2")]);

        read.Problem.Should().BeNull();
        read.Contract!.Names.Should().ContainSingle().Which.Should().Match<SeriesContractName>(
            n => n.Number == 87 && n.Owner == "c-1" && n.Writers!.Single() == "c-2");
        read.Contract.Data.Should().ContainSingle().Which.Should().Be(new SeriesContractDatum("rounds", "RunSpecReviewRounds", "c-2"));
        read.Contract.Areas.Select(a => a.Spec).Should().Equal("c-1", "c-2");
        read.Contract.Budgets.Select(b => (b.Spec, b.MaxLines)).Should().Equal(("c-1", 100), ("c-2", 110));
    }

    private static SeriesContractRead Read(string json, IReadOnlyList<DerivedSpec> built, IReadOnlyList<DerivedSpec> head)
    {
        using var doc = JsonDocument.Parse(json);
        return SeriesContracts.Reader().FromReply(doc.RootElement, built, head);
    }

    private static IReadOnlyList<DerivedSpec> Built(string letters) =>
        [.. letters.Select(l => new DerivedSpec(Child(Id(l)), Id(l), string.Empty, []))];

    private static SpecDraft Child(string id) => new(id, $"goal {id}", $"spec: {id}", []) { Repos = ["agent-smith"] };
}
