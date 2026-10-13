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

    private readonly SpecDerivationParser _parser = DerivationTestParsers.Real();

    [Fact]
    public void DeriveSpec_MigrationTicketFixture_EmitsAnOrderedSpecSet()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            TwoSpecReply(segments), "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Error.Should().BeNull();
        var set = parsed.Derivation!.Set;
        set.Specs.Should().HaveCount(2, "an 800-line manual is a sequence, not one spec");
        set.Specs[0].SpecId.Should().Be("2026-10-06-9106a");
        set.Specs[1].SpecId.Should().Be("2026-10-06-9106b");
        set.Specs[1].Draft.Requires.Should().Contain(
            "2026-10-06-9106a", "the sequence IS the requires-chain");
        set.Specs.Should().OnlyContain(
            p => p.Draft.Done.Count > 0, "a spec that cannot end is not a spec");
    }

    [Fact]
    public void DeriveSpec_SpecWithoutDoneCriteria_IsRejectedRatherThanDefaulted()
    {
        // p0421: the contract guards the CRITERIA now. A spec that cannot state what is
        // true when it is finished cannot be accounted for against the branch, and the
        // deriver's retry loop hands the rejection back to the model to answer.
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var reply = $$"""
            {"specs": [{"goal": "swap the client", "slug": "swap-the-client",
                         "steps": ["replace the calls"], "segments": [1]}]}
            """;

        var parsed = _parser.Parse(reply, "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Derivation.Should().BeNull("a spec that cannot state its completion is not ratifiable");
        parsed.Error.Should().Contain("done-criteria",
            "the rejection has to name the missing declaration, because it is handed "
            + "straight back to the model as the correction prompt");
    }

    [Fact]
    public void DeriveSpec_CodeTemplates_LandInTheMarkdownByteIdenticalToTheTicket()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var template = segments.Single(s => s.Text.Contains("OrderServiceClient", StringComparison.Ordinal));

        var parsed = _parser.Parse(
            TwoSpecReply(segments), "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        var markdown = parsed.Derivation!.Set.Specs[0].Markdown;
        markdown.Should().Contain(template.Text,
            "the model returns anchors and CODE cuts the span — a retyped template is a "
            + "plausible copy, and a plausible copy of a naming contract is a broken one");
        markdown.Should().Contain("`<Domain>ServiceClient`",
            "the naming rule survives with its backticks and its angle brackets");
    }

    [Fact]
    public void Accounting_EverySegment_IsCarriedBySomeSpecOrDiscardedWithAReason()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            TwoSpecReply(segments), "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

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

        // The judgement forgot the last two segments entirely — no spec, no reason.
        var parsed = _parser.Parse(
            """
              {"specs": [{"slug": "rename", "goal": "Rename the clients",
                           "done": ["Every call site uses the new name."],
                           "carries": [1, 2]}],
               "discarded": []}
              """,
            "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Derivation!.Set.Accounting.IsComplete.Should().BeFalse();
        parsed.Derivation.Set.Accounting.Unaccounted.Should().Contain(segments[^1].Id);
    }

    // 2026-09-08-1830: a spec names the contexts it changes and the cut names the ones it
    // leaves out; both are read out of the reply, rendered into the record, and read back
    // through the same reader — while the verbatim companion stays the ticket's own bytes.
    [Fact]
    public void DeriveSpec_SpecContextsAndDiscardedContexts_RoundTripThroughRecordAndIndex()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var reply = TwoSpecReply(segments)
            .Replace("\"slug\": \"rename-the-clients\",", "\"slug\": \"rename-the-clients\", \"contexts\": [\"backend\", \"frontend\"],")
            .Replace("\"discarded\": [", "\"discarded_contexts\": [{\"context\": \"worker\", \"reason\": \"no client there\"}], \"discarded\": [");

        var parsed = _parser.Parse(reply, "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Error.Should().BeNull();
        var set = parsed.Derivation!.Set;
        set.Specs[0].Draft.Contexts.Should().Equal("backend", "frontend");
        set.Specs[0].Draft.Yaml.Should().Contain("contexts:").And.Contain("- backend");
        set.Specs[1].Draft.Contexts.Should().BeEmpty("a spec that declares none has none");
        set.Specs[1].Draft.Yaml.Should().NotContain("contexts:");
        set.Accounting.DiscardedContexts.Should().ContainSingle()
            .Which.Should().Be(new DiscardedContext("worker", "no client there"));
        set.Specs[0].Markdown.Should().NotContain("backend", "the companion carries the ticket's bytes only");
        new SpecDraftReader().Read(set.Specs[0].Draft.Yaml).Contexts.Should().Equal("backend", "frontend");
    }

    [Fact]
    public void DeriveSpec_NotImplementable_ProducesAHandbackAndNoSpecs()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            """
            {"specs": [],
             "handback": {"case": "not_implementable",
                          "reason": "The legacy client is the only transport this platform has."}}
            """,
            "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        var set = parsed.Derivation!.Set;
        set.IsHandedBack.Should().BeTrue();
        set.Handback!.Case.Should().Be(SpecHandbackCase.NotImplementable);
        set.Handback.IsVerdict.Should().BeTrue("a verdict does not auto-retry on a comment");
        set.Specs.Should().BeEmpty("a hand-back replaces the spec, it does not accompany it");
    }

    [Fact]
    public void DeriveSpec_RequirementContradictsRepository_ProducesTheQuestionClassHandback()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            """
            {"specs": [],
             "handback": {"case": "requirements_contradict_repository",
                          "reason": "There is no LegacyHttpHelper in any repository in scope."}}
            """,
            "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Derivation!.Set.Handback!.Case
            .Should().Be(SpecHandbackCase.RequirementsContradictRepository);
        parsed.Derivation.Set.Handback!.IsVerdict.Should().BeFalse(
            "a contradiction is a question — an answer re-triggers the run");
    }

    // 2026-09-07-c9d4: a QUESTION carries its readings as a typed list and the taken one
    // as an index, because the framework names the taken reading twice — in the question
    // and in the notice that the run proceeded on it — and the second run holds only the
    // model's second phrasing.
    [Fact]
    public void DeriveSpec_QuestionWithTwoReadings_ProducesATypedQuestionHandback()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(QuestionReply(taken: 1), "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Error.Should().BeNull();
        var handback = parsed.Derivation!.Set.Handback!;
        handback.Case.Should().Be(SpecHandbackCase.Question);
        handback.IsVerdict.Should().BeFalse("a question parks where a person can answer");
        handback.Readings.Should().Equal(
            "a major only where nothing lower clears the advisory",
            "the newest major everywhere, breaking changes included");
        handback.TakenReading.Should().Be("the newest major everywhere, breaking changes included");
        parsed.Derivation.Set.Specs.Should().BeEmpty("a hand-back replaces the spec, it does not accompany it");
    }

    [Theory]
    [InlineData(1, 0, "at least 2 readings")]
    [InlineData(2, 2, "taken index 2 names none of the 2 readings")]
    public void DeriveSpec_QuestionNobodyCanAnswer_IsRejectedBackToTheModelByName(
        int readings, int taken, string expectedError)
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var listed = string.Join(", ", Enumerable.Range(0, readings).Select(i => $"\"reading {i}\""));
        var reply = $$$"""
            {"specs": [],
             "handback": {"case": "question", "reason": "the ticket reads two ways",
                          "readings": [{{{listed}}}], "taken": {{{taken}}}}}
            """;

        var parsed = _parser.Parse(reply, "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain(expectedError);
    }

    [Fact]
    public void DeriveSpec_EveryHandbackCase_ReplacesTheSpec()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);
        foreach (var name in new[] { "not_implementable", "requirements_contradict_repository", "refused", "question" })
        {
            var reply = name == "question"
                ? QuestionReply(taken: 0)
                : $$$"""{"specs": [], "handback": {"case": "{{{name}}}", "reason": "r"}}""";

            var parsed = _parser.Parse(reply, "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

            parsed.Derivation!.Set.IsHandedBack.Should().BeTrue(name);
            parsed.Derivation.Set.Specs.Should().BeEmpty(name);
        }
    }

    [Fact]
    public void DeriveSpec_ReplyNamingNoQuestion_CarriesNoHandback()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            TwoSpecReply(segments), "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Derivation!.Set.IsHandedBack.Should().BeFalse();
        parsed.Derivation.Set.Handback.Should().BeNull();
        parsed.Derivation.Set.Specs.Should().HaveCount(2);
    }

    private static string QuestionReply(int taken) => $$$"""
        {"specs": [],
         "handback": {"case": "question",
                      "reason": "'Adopt the newest versions, even breaking' reads two ways and the work differs.",
                      "readings": ["a major only where nothing lower clears the advisory",
                                   "the newest major everywhere, breaking changes included"],
                      "taken": {{{taken}}}}}
        """;

    [Fact]
    public void DeriveSpec_SpecWithoutDoneCriteria_IsRejectedBackToTheModel()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);

        var parsed = _parser.Parse(
            """{"specs": [{"slug": "rename", "goal": "Rename the clients", "carries": [1]}]}""",
            "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("done-criteria");
    }

    [Fact]
    public void Recut_UnexecutedTail_IsRepartitionedWhileTheExecutedHeadKeepsItsIds()
    {
        var segments = TicketSegmenter.Segment(MigrationTicket);
        var original = _parser.Parse(
            TwoSpecReply(segments), "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived)
            .Derivation!.Set;
        var executedHead = new[] { original.Specs[0] };

        // The correction says "the cut is wrong" and re-writes BOTH entries. The first is
        // executed, so what the model said about it is discarded.
        var recut = _parser.Parse(
            """
            {"specs": [
               {"slug": "something-else-entirely", "goal": "A different first spec",
                "done": ["Nobody asked for this."], "carries": [1]},
               {"slug": "split-a", "goal": "Forbid the legacy helper",
                "done": ["LegacyHttpHelper appears in no new code."], "carries": [4]},
               {"slug": "split-b", "goal": "Delete the legacy helper",
                "done": ["LegacyHttpHelper is gone."], "carries": [5]}],
             "discarded": [{"segment": 2, "reason": "prose"},
                           {"segment": 3, "reason": "prose"},
                           {"segment": 6, "reason": "sign-off"}]}
            """,
            "azdo-19106", "2026-10-06-9106", "19106", segments, SpecSource.Derived, executedHead);

        var set = recut.Derivation!.Set;
        set.Specs[0].Should().BeSameAs(original.Specs[0],
            "an executed spec is APPEND-ONLY — a correction to it is a new spec, never an edit");
        set.Specs.Should().HaveCount(3, "the unexecuted tail was re-partitioned into two");
        set.Specs[1].SpecId.Should().Be("2026-10-06-9106b");
        set.Specs[2].SpecId.Should().Be("2026-10-06-9106c");
        set.Executed.Should().ContainSingle().Which.Should().Be("2026-10-06-9106a");
    }

    // The judgement half, scripted: two specs, everything carried except the sign-off.
    private static string TwoSpecReply(IReadOnlyList<TicketSegment> segments)
    {
        var last = segments[^1].Id;
        var head = string.Join(",", segments.SkipLast(1).Take(3).Select(s => s.Id));
        var tail = string.Join(",", segments.SkipLast(1).Skip(3).Select(s => s.Id));
        return $$$"""
            {"specs": [
               {"slug": "rename-the-clients",
                "goal": "Rename every call site onto the ServiceClient convention",
                "steps": [{"id": "rename", "action": "Rename the call sites"}],
                "done": ["Every call site is named <Domain>ServiceClient."],
                "carries": [{{{head}}}]},
               {"slug": "forbid-the-legacy-helper",
                "goal": "Remove the forbidden helper from new code",
                "steps": [{"id": "forbid", "action": "Drop the helper"}],
                "done": ["LegacyHttpHelper appears in no new code."],
                "carries": [{{{(tail.Length > 0 ? tail : head)}}}]}],
             "discarded": [{"segment": {{{last}}}, "reason": "a sign-off, not part of the work"}],
             "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
    }
}
