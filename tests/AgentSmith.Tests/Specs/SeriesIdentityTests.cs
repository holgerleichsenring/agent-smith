using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-06-03c7c: a series' identity is minted in code and read back thereafter — never derived
/// from a ticket number, which two trackers hand out independently, and never taken from a model.
/// </summary>
public sealed class SeriesIdentityTests
{
    private const string Reply = """
        {"phases": [{"slug": "rename", "goal": "Rename the clients", "done": ["Renamed."], "carries": [1]}]}
        """;

    [Fact]
    public void Series_TwoTrackersSameTicketNumber_DifferentIds()
    {
        // Two fixed bases stand in for two mints: the claim is that the id follows the series,
        // never the ticket number, and two random mints would collide once in 65,536 runs.
        var segments = TicketSegmenter.Segment("Rename the clients.");

        var jira = Parse(TicketKey.For("jira", "1"), "2026-10-06-0a0a", segments);
        var github = Parse(TicketKey.For("github", "1"), "2026-10-06-0b0b", segments);

        jira.Key.Should().NotBe(github.Key, "the ticket key names the tracker");
        jira.Phases[0].PhaseId.Should().NotBe(github.Phases[0].PhaseId,
            "ticket 1 on two trackers is two series, and a number both share is not an identity");
        jira.Phases[0].PhaseId.Should().NotStartWith("p", "no id is derived from the ticket number any more");
    }

    [Fact]
    public void SeriesResolver_TheBranchOutranksTheRecordAndThePointer()
    {
        var resolver = new SeriesResolver(new SeriesIdFactory(TimeProvider.System));
        var record = new SpecApprovalRecord("jira-1", Set("2026-10-06-0002"), [], "t");
        var pointer = new SpecSetPointer("jira-1", "repo", "sha", 1, SeriesId: "2026-10-06-0003");

        resolver.Resolve(Set("2026-10-06-0001"), record, pointer).Should().Be("2026-10-06-0001");
        resolver.Resolve(null, record, pointer).Should().Be("2026-10-06-0002");
        resolver.Resolve(null, null, pointer).Should().Be("2026-10-06-0003", "the pointer row caches the base");
    }

    [Fact]
    public void PhaseDraftIdRewriter_SpecAndRequires_AreRewrittenAndFreeTextIsKept()
    {
        var ids = new Dictionary<string, string> { ["p12a"] = "2026-10-06-0a0aa", ["p12b"] = "2026-10-06-0a0ab" };
        const string yaml = "spec: p12b\ngoal: \"keep p12a in prose\"\nrequires:\n- p12a-the-label\n- the API is live\ndone:\n- d\n";

        var rewritten = new PhaseDraftIdRewriter().Rewrite(yaml, ids);

        rewritten.Should().Contain("spec: 2026-10-06-0a0ab")
            .And.Contain("- 2026-10-06-0a0aa-the-label", "an id-label entry matches by its id prefix")
            .And.Contain("- the API is live")
            .And.Contain("keep p12a in prose", "only the id fields are rewritten, never the prose");
        new PhaseDraftIdRewriter().RewriteValue("p12ab", ids).Should().Be("p12ab", "a longer token is not an id");
    }

    [Fact]
    public void SeriesDraftIds_Assign_RenumbersInOrderAndFollowsEdges()
    {
        var drafts = new[]
        {
            new PhaseDraft("p7b", "second", "spec: p7b\ngoal: second", []),
            new PhaseDraft("p7a", "first", "spec: p7a\ngoal: first\nrequires:\n- p7b", ["p7b"]),
        };

        var assigned = ApprovedSetDoubles.DraftIds().Assign("2026-10-06-0a0a", drafts);

        assigned.Select(d => d.PhaseId).Should().Equal("2026-10-06-0a0aa", "2026-10-06-0a0ab");
        assigned[1].Requires.Should().Equal("2026-10-06-0a0aa");
        assigned[1].Yaml.Should().Contain("spec: 2026-10-06-0a0ab").And.Contain("- 2026-10-06-0a0aa");
    }

    [Fact]
    public void FiledSeriesFactory_KeptOrMinted_KeepsARecordsBaseAndMintsForNone()
    {
        var factory = ApprovedSetDoubles.SeriesFiling();
        var record = new SpecApprovalRecord("jira-1", Set("2026-10-06-0001"), [], "t");

        factory.KeptOrMinted(record).Should().Be("2026-10-06-0001");
        factory.KeptOrMinted(null).Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}-[0-9a-f]{4}$");
        factory.Under("2026-10-06-0001", [new PhaseDraft("x", "g", "spec: x", [])]).Drafts[0].PhaseId
            .Should().Be("2026-10-06-0001a");
    }

    [Fact]
    public void SpecSetIndex_Series_RoundTrips()
    {
        var index = new SpecSetIndex();

        var doc = index.Parse(index.Serialize(Set("2026-10-06-0001")))!;

        index.SeriesOf(doc).Should().Be("2026-10-06-0001");
        index.SeriesOf(index.Parse(index.Serialize(Set(null)))!).Should().BeNull();
    }

    private static SpecSet Parse(TicketKey key, string series, IReadOnlyList<TicketSegment> segments) =>
        DerivationTestParsers.Real().Parse(Reply, key.Value, series, "1", segments, SpecSource.Derived)
            .Derivation!.Set;

    private static SpecSet Set(string? series) =>
        new("jira-1", [], SpecAccounting.Empty, [], SpecSource.Derived, Series: series);
}
