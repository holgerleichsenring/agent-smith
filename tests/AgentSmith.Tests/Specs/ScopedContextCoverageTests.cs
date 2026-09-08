using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-1830: the reader decides coverage in code. Run 8688's scope call named
/// two contexts and the cut carried one; nothing compared the two. The gap is named
/// minus carried minus discarded, and the check is armed by the reply — a cut whose
/// phases declare no contexts is an old catalog's cut and is read as before.
/// </summary>
public sealed class ScopedContextCoverageTests
{
    private const string Ticket = """
        Fix the high and critical advisories in the dependencies of the backend and the frontend.

        Thanks.
        """;

    private readonly ScopedContextCoverage _coverage = new();

    [Fact]
    public void Coverage_ANamedContextNoPhaseCarries_IsTheGap()
    {
        var set = Cut("""["frontend"]""", discardedContexts: "[]");

        _coverage.Gap(Named("frontend", "backend"), set).Should().Equal("backend");
    }

    [Fact]
    public void Coverage_ADiscardedContext_ClosesTheGap()
    {
        var set = Cut("""["frontend"]""",
            discardedContexts: """[{"context": "backend", "reason": "the audit flags nothing there"}]""");

        _coverage.Gap(Named("frontend", "backend"), set).Should().BeEmpty();
        set.Accounting.DiscardedContexts.Should().ContainSingle()
            .Which.Should().Be(new DiscardedContext("backend", "the audit flags nothing there"));
    }

    [Fact]
    public void Coverage_NoPhaseDeclaresContexts_IsOff()
    {
        // The field an old catalog never emits: the check must not fire on its absence.
        var set = Cut(contexts: null, discardedContexts: "[]");

        _coverage.Gap(Named("frontend", "backend"), set).Should().BeEmpty(
            "a reply that declares no contexts is read exactly as before this phase");
    }

    [Fact]
    public void Coverage_NothingNamed_IsOff()
    {
        var set = Cut("""["frontend"]""", discardedContexts: "[]");

        _coverage.Gap(null, set).Should().BeEmpty();
        _coverage.Gap(new ScopeNamedContexts([]), set).Should().BeEmpty();
    }

    [Fact]
    public void Coverage_ABareNameCoversAQualifiedOne_AndTheSpellingIsCaseInsensitive()
    {
        var set = Cut("""["Backend"]""", discardedContexts: "[]");

        _coverage.Gap(Named("primary/backend", "primary/frontend"), set).Should().Equal("primary/frontend");
    }

    private static ScopeNamedContexts Named(params string[] contexts) =>
        new(contexts, "both contexts need dependency auditing");

    private static SpecSet Cut(string? contexts, string discardedContexts)
    {
        var segments = Application.Services.Specs.TicketSegmenter.Segment(Ticket);
        var declared = contexts is null ? string.Empty : $"\"contexts\": {contexts},";
        var reply = $$$"""
            {"phases": [
               {"slug": "raise-the-floors", "goal": "Raise the package floors the audit names",
                {{{declared}}}
                "steps": [{"id": "raise", "action": "Raise the versions"}],
                "done": ["The manifests carry versions the audit no longer flags."],
                "carries": [{{{segments[0].Id}}}]}],
             "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
             "discarded_contexts": {{{discardedContexts}}},
             "handback": {"case": "none", "reason": ""}}
            """;
        var parsed = DerivationTestParsers.Real().Parse(reply, "azdo-19106", "19106", segments, SpecSource.Derived);
        parsed.Error.Should().BeNull();
        return parsed.Derivation!.Set;
    }
}
