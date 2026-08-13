using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0393a: the derivation's two halves proven apart. The JUDGEMENT (where the
/// boundaries fall, what is load-bearing) is the model's and is scripted here; the
/// EXTRACTION (cutting the anchored spans, rendering the yaml, the accounting) is code
/// and is what these assert — including that a code template survives byte-identical,
/// which is the failure mode the whole line of work started from.
/// </summary>
public sealed class SpecDerivationTests
{
    private const string MigrationTicket = """
        Migrate every service off the legacy client.

        All new call sites MUST be named `<Domain>ServiceClient` — never `<Domain>Client`.

        ```csharp
        public sealed class OrderServiceClient(HttpClient http) : IOrderServiceClient
        {
            public Task<Order> GetAsync(OrderId id, CancellationToken ct) => http.GetAsync(id, ct);
        }
        ```

        The `LegacyHttpHelper` API is forbidden in new code.

        Thanks in advance, and ping me if anything is unclear.
        """;

    private readonly SpecDerivationParser _parser = new(
        new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader(),
        new DerivedPhaseYamlRenderer(), new SpecDerivationEnvelope());

    [Fact]
    public void DeriveSpec_MigrationTicketFixture_EmitsAnOrderedPhaseSet()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            TwoPhaseReply(segments), "azdo-19106", "19106", segments, SpecSource.Derived);

        parsed.Error.Should().BeNull();
        var set = parsed.Derivation!.Set;
        set.Phases.Should().HaveCount(2, "an 800-line manual is a sequence, not one phase");
        set.Phases[0].PhaseId.Should().Be("p19106a");
        set.Phases[1].PhaseId.Should().Be("p19106b");
        set.Phases[1].Draft.Requires.Should().Contain(
            "p19106a", "the sequence IS the requires-chain");
        set.Phases.Should().OnlyContain(
            p => p.Draft.Done.Count > 0, "a phase that cannot end is not a phase");
    }

    [Theory]
    [InlineData("ships_code")]
    [InlineData("shipsCode")]
    [InlineData("ShipsCode")]
    [InlineData("ships-code")]
    public void DeriveSpec_ShipsCodeFalseInReply_LandsOnTheDraft(string key)
    {
        // p0400b: the JSON lookup folded underscores on the MODEL's side only, so a
        // reader asking for the field as prompt and schema spell it ("ships_code")
        // matched nothing and silently fell back to true. This pins the DERIVATION-JSON
        // path; the YAML reader has its own test and could not catch this. The spellings
        // are the ones models emit interchangeably — every one is the same declaration.
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var all = string.Join(",", segments.Select(x => x.Id));
        var reply = $$$"""
            {"phases": [
               {"slug": "setup-and-inventory",
                "goal": "The inventory is recorded before any code is touched",
                "steps": [{"id": "inventory", "action": "Run the inventory greps"}],
                "done": ["The inventory is captured per repository."],
                "carries": [{{{all}}}],
                "{{{key}}}": false}],
             "discarded": [], "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;

        var parsed = _parser.Parse(reply, "azdo-19106", "19106", segments, SpecSource.Derived);

        parsed.Error.Should().BeNull();
        parsed.Derivation!.Set.Phases[0].Draft.Yaml.Should().Contain("ships_code: false",
            "the rendered spec carries the declaration the model made, not the default");
        parsed.Derivation!.Set.Phases[0].Draft.ShipsCode.Should().BeFalse(
            "the model's ratified declaration must survive parsing");
    }

    [Fact]
    public void DeriveSpec_PhaseWithoutShipsCode_IsRejectedRatherThanDefaulted()
    {
        // p0400c: live run aa2a rendered ships_code: true on all five phases —
        // including a pure inventory and a final report — because an absent field and
        // a declared true are the same value once a fallback has run. The parser now
        // refuses the cut; the deriver hands the rejection back and the model answers.
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var all = string.Join(",", segments.Select(x => x.Id));
        var reply = $$$"""
            {"phases": [
               {"slug": "setup-and-inventory",
                "goal": "The inventory is recorded before any code is touched",
                "steps": [{"id": "inventory", "action": "Run the inventory greps"}],
                "done": ["The inventory is captured per repository."],
                "carries": [{{{all}}}]}],
             "discarded": [], "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;

        var parsed = _parser.Parse(reply, "azdo-19106", "19106", segments, SpecSource.Derived);

        parsed.Derivation.Should().BeNull("a phase that never declared its deliverable is not ratifiable");
        parsed.Error.Should().Contain("ships_code",
            "the rejection has to name the missing declaration, because it is handed "
            + "straight back to the model as the correction prompt");
    }

    [Fact]
    public void DeriveSpec_CodeTemplates_LandInTheMarkdownByteIdenticalToTheTicket()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var template = segments.Single(s => s.Text.Contains("OrderServiceClient", StringComparison.Ordinal));

        var parsed = _parser.Parse(
            TwoPhaseReply(segments), "azdo-19106", "19106", segments, SpecSource.Derived);

        var markdown = parsed.Derivation!.Set.Phases[0].Markdown;
        markdown.Should().Contain(template.Text,
            "the model returns anchors and CODE cuts the span — a retyped template is a "
            + "plausible copy, and a plausible copy of a naming contract is a broken one");
        markdown.Should().Contain("`<Domain>ServiceClient`",
            "the naming rule survives with its backticks and its angle brackets");
    }

    [Fact]
    public void Accounting_EverySegment_IsCarriedBySomePhaseOrDiscardedWithAReason()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            TwoPhaseReply(segments), "azdo-19106", "19106", segments, SpecSource.Derived);

        var accounting = parsed.Derivation!.Set.Accounting;
        var signOff = segments[^1].Id;
        accounting.IsComplete.Should().BeTrue();
        accounting.Discarded.Should().ContainSingle(
            d => d.SegmentId == signOff && d.Reason.Length > 0,
            "the sign-off is discarded WITH a reason, not silently dropped");
        foreach (var segment in segments.SkipLast(1))
            accounting.Carried.Should().Contain(c => c.SegmentId == segment.Id);
    }

    [Fact]
    public void Accounting_SegmentNeitherCarriedNorDiscarded_IsReportedAsUnaccounted()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        // The judgement forgot the last two segments entirely — no phase, no reason.
        var parsed = _parser.Parse(
            """
              {"phases": [{"slug": "rename", "goal": "Rename the clients",
                           "done": ["Every call site uses the new name."],
                           "carries": [1, 2], "ships_code": true}],
               "discarded": []}
              """,
            "azdo-19106", "19106", segments, SpecSource.Derived);

        parsed.Derivation!.Set.Accounting.IsComplete.Should().BeFalse();
        parsed.Derivation.Set.Accounting.Unaccounted.Should().Contain(segments[^1].Id);
    }

    [Fact]
    public void DeriveSpec_NotImplementable_ProducesAHandbackAndNoPhases()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            """
            {"phases": [],
             "handback": {"case": "not_implementable",
                          "reason": "The legacy client is the only transport this platform has."}}
            """,
            "azdo-19106", "19106", segments, SpecSource.Derived);

        var set = parsed.Derivation!.Set;
        set.IsHandedBack.Should().BeTrue();
        set.Handback!.Case.Should().Be(SpecHandbackCase.NotImplementable);
        set.Handback.IsVerdict.Should().BeTrue("a verdict does not auto-retry on a comment");
        set.Phases.Should().BeEmpty("a hand-back replaces the spec, it does not accompany it");
    }

    [Fact]
    public void DeriveSpec_RequirementContradictsRepository_ProducesTheQuestionClassHandback()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            """
            {"phases": [],
             "handback": {"case": "requirements_contradict_repository",
                          "reason": "There is no LegacyHttpHelper in any repository in scope."}}
            """,
            "azdo-19106", "19106", segments, SpecSource.Derived);

        parsed.Derivation!.Set.Handback!.Case
            .Should().Be(SpecHandbackCase.RequirementsContradictRepository);
        parsed.Derivation.Set.Handback!.IsVerdict.Should().BeFalse(
            "a contradiction is a question — an answer re-triggers the run");
    }

    [Fact]
    public void DeriveSpec_PhaseWithoutDoneCriteria_IsRejectedBackToTheModel()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            """{"phases": [{"slug": "rename", "goal": "Rename the clients", "carries": [1]}]}""",
            "azdo-19106", "19106", segments, SpecSource.Derived);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("done-criteria");
    }

    [Fact]
    public void Recut_UnexecutedTail_IsRepartitionedWhileTheExecutedHeadKeepsItsIds()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var original = _parser.Parse(
            TwoPhaseReply(segments), "azdo-19106", "19106", segments, SpecSource.Derived)
            .Derivation!.Set;
        var executedHead = new[] { original.Phases[0] };

        // The correction says "the cut is wrong" and re-writes BOTH entries. The first is
        // executed, so what the model said about it is discarded.
        var recut = _parser.Parse(
            """
            {"phases": [
               {"slug": "something-else-entirely", "goal": "A different first phase",
                "done": ["Nobody asked for this."], "carries": [1], "ships_code": true},
               {"slug": "split-a", "goal": "Forbid the legacy helper",
                "done": ["LegacyHttpHelper appears in no new code."], "carries": [4], "ships_code": true},
               {"slug": "split-b", "goal": "Delete the legacy helper",
                "done": ["LegacyHttpHelper is gone."], "carries": [5], "ships_code": true}],
             "discarded": [{"segment": 2, "reason": "prose"},
                           {"segment": 3, "reason": "prose"},
                           {"segment": 6, "reason": "sign-off"}]}
            """,
            "azdo-19106", "19106", segments, SpecSource.Derived, executedHead);

        var set = recut.Derivation!.Set;
        set.Phases[0].Should().BeSameAs(original.Phases[0],
            "an executed phase is APPEND-ONLY — a correction to it is a new phase, never an edit");
        set.Phases.Should().HaveCount(3, "the unexecuted tail was re-partitioned into two");
        set.Phases[1].PhaseId.Should().Be("p19106b");
        set.Phases[2].PhaseId.Should().Be("p19106c");
        set.Executed.Should().ContainSingle().Which.Should().Be("p19106a");
    }

    // The judgement half, scripted: two phases, everything carried except the sign-off.
    private static string TwoPhaseReply(IReadOnlyList<TicketSegment> segments)
    {
        var last = segments[^1].Id;
        var head = string.Join(",", segments.SkipLast(1).Take(3).Select(s => s.Id));
        var tail = string.Join(",", segments.SkipLast(1).Skip(3).Select(s => s.Id));
        return $$$"""
            {"phases": [
               {"slug": "rename-the-clients",
                "goal": "Rename every call site onto the ServiceClient convention",
                "steps": [{"id": "rename", "action": "Rename the call sites"}],
                "done": ["Every call site is named <Domain>ServiceClient."],
                "carries": [{{{head}}}], "ships_code": true},
               {"slug": "forbid-the-legacy-helper",
                "goal": "Remove the forbidden helper from new code",
                "steps": [{"id": "forbid", "action": "Drop the helper"}],
                "done": ["LegacyHttpHelper appears in no new code."],
                "carries": [{{{(tail.Length > 0 ? tail : head)}}}], "ships_code": true}],
             "discarded": [{"segment": {{{last}}}, "reason": "a sign-off, not part of the work"}],
             "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
    }
}
