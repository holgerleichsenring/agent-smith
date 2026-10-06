using AgentSmith.Application.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-02-3f06a: an assumption written as {claim, check} is read as its claim. A spec read
/// back from a branch is never validated, so a map without a claim is dropped rather than
/// stringified into a .NET type name.
/// </summary>
public sealed class PhaseDraftListsTests
{
    private readonly PhaseDraftReader _reader = new();

    [Fact]
    public void PhaseDraftReader_AssumptionMap_ReadsItsClaim()
    {
        var draft = _reader.Read(
            "spec: p9999\ngoal: g\nassumptions:\n"
            + "  - claim: \"the cache is warm\"\n    check: \"read the hit rate\"\n  - \"a bare one\"\n");

        draft.Assumptions.Should().Equal("the cache is warm", "a bare one");
    }

    [Fact]
    public void PhaseDraftReader_AssumptionMapWithoutClaim_IsDropped()
    {
        var draft = _reader.Read(
            "spec: p9999\ngoal: g\nassumptions:\n  - check: \"orphaned\"\n  - claim: [\"not\", \"a string\"]\n");

        draft.Assumptions.Should().BeEmpty();
    }

    [Fact]
    public void Facts_EntryWithoutClaim_IsDroppedAndEvidenceKept()
    {
        var map = Map("facts:\n  - claim: \"c\"\n    evidence: \"a.cs:1\"\n  - evidence: \"b.cs:2\"\n  - \"bare\"\n");

        var facts = PhaseDraftLists.Facts(map);

        facts.Should().ContainSingle().Which.Evidence.Should().Be("a.cs:1");
    }

    [Fact]
    public void Strings_SingleStringOrList_ReadsEveryEntry()
    {
        PhaseDraftLists.Strings(Map("contexts: default\n"), "contexts").Should().Equal("default");
        PhaseDraftLists.Strings(Map("contexts: [a, b]\n"), "contexts").Should().Equal("a", "b");
        PhaseDraftLists.Strings(Map("contexts:\n"), "contexts").Should().BeEmpty();
    }

    [Fact]
    public void GetString_BlankOrNonString_IsNull()
    {
        var entry = new Dictionary<object, object?> { ["id"] = " ", ["path"] = new List<object?>() };

        PhaseDraftLists.GetString(entry, "id").Should().BeNull();
        PhaseDraftLists.GetString(entry, "path").Should().BeNull();
    }

    private static IReadOnlyDictionary<string, object?> Map(string yaml) => OutcomeYamlReader.ReadMap(yaml);
}
