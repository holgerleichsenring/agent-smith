using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using static AgentSmith.Tests.Specs.SampleMessagingManual;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-17-293ba: a reply states the segments every spec needs once, under "shared", and each
/// spec carries those plus its own unit's — distributed by code, recorded, and absent from a reply
/// that predates the key.
/// </summary>
public sealed class SpecDerivationAudienceTests
{
    private const string Key = "azdo-1";
    private const string Series = "2026-10-17-0a0a";

    private static readonly IReadOnlyList<TicketSegment> Segments = TicketSegmenter.Segment(Ticket);
    private readonly SpecDerivationParser _parser = DerivationTestParsers.Real();

    [Fact]
    public void Shared_EachSpec_CarriesTheSharedSegmentsAndItsOwnVariantOnly()
    {
        var set = Parse(SharedCut);

        Segments.Should().HaveCount(8);
        set.Specs[0].CarriedSegments.Should().Equal(1, 2, 3, 4, 5);
        set.Specs[1].CarriedSegments.Should().Equal(1, 2, 3, 6, 7);
        set.Specs[0].Markdown.Should().Contain(SharedTemplate).And.Contain(PublisherTemplate)
            .And.NotContain(ConsumerTemplate, "a pure publisher must not be handed listener queues");
        set.Specs[1].Markdown.Should().Contain(SharedTemplate).And.Contain(ConsumerTemplate)
            .And.NotContain(PublisherTemplate);
        set.Accounting.IsComplete.Should().BeTrue();
        set.Accounting.Shared.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Shared_AReplyWithoutTheKey_ParsesAsBefore()
    {
        var expanded = SharedCut.Replace("\"carries\": [4, 5]", "\"carries\": [1, 2, 3, 4, 5]")
            .Replace("\"carries\": [6, 7]", "\"carries\": [1, 2, 3, 6, 7]")
            .Replace("\"shared\": [1, 2, 3],", string.Empty);

        var old = Parse(expanded);
        var shared = Parse(SharedCut);

        old.Specs.Select(s => s.Markdown).Should().Equal(shared.Specs.Select(s => s.Markdown));
        old.Specs.Select(s => s.Draft.Yaml).Should().Equal(shared.Specs.Select(s => s.Draft.Yaml));
        old.Accounting.Carried.Should().BeEquivalentTo(shared.Accounting.Carried);
        old.Accounting.Shared.Should().BeEmpty();
    }

    [Fact]
    public void Shared_TheExecutedHead_KeepsWhatItCarried()
    {
        var head = Parse(SharedCut.Replace("\"shared\": [1, 2, 3],", string.Empty)
            .Replace("\"discarded\": [", "\"discarded\": [{\"segment\": 1, \"reason\": \"t\"}, {\"segment\": 2, \"reason\": \"t\"}, {\"segment\": 3, \"reason\": \"t\"}, "))
            .Specs[0];

        var recut = Parse(SharedCut, [head]);

        recut.Specs[0].Should().BeSameAs(head);
        recut.Specs[0].CarriedSegments.Should().Equal(4, 5);
        recut.Specs[1].CarriedSegments.Should().Equal(1, 2, 3, 6, 7);
    }

    [Fact]
    public void Shared_ASharedSegmentNoNewSpecCarries_IsUnaccounted()
    {
        var head = Parse(SharedCut.Replace("\"shared\": [1, 2, 3],", string.Empty)
            .Replace("\"carries\": [4, 5]", "\"carries\": [4, 5, 6, 7]")
            .Replace("\"discarded\": [", "\"discarded\": [{\"segment\": 1, \"reason\": \"t\"}, {\"segment\": 2, \"reason\": \"t\"}, {\"segment\": 3, \"reason\": \"t\"}, "))
            .Specs[0];
        var onlyTheHead = SharedCut.Replace(
            SharedCut[SharedCut.IndexOf(",\n   {\"slug\": \"consumer", StringComparison.Ordinal)..SharedCut.IndexOf("],\n \"shared\"", StringComparison.Ordinal)],
            string.Empty);

        var recut = Parse(onlyTheHead, [head]);

        recut.Specs.Should().ContainSingle();
        recut.Accounting.Unaccounted.Should().Equal(1, 2, 3);
        recut.Accounting.IsComplete.Should().BeFalse("declaring a segment shared is not carrying it");
    }

    [Fact]
    public void Shared_TheManifestAndThePullRequest_NameIt()
    {
        var set = Parse(SharedCut);
        var manifest = new SeriesManifest();

        var yaml = manifest.Serialize(set);
        var reread = manifest.AccountingOf(manifest.Parse(yaml)!);

        yaml.Should().Contain("shared:");
        reread.Shared.Should().Equal(1, 2, 3);
        SpecPrBody.RenderDiscarded(set).Should().Contain("## Carried by every spec")
            .And.Contain("segment(s) 1, 2, 3");
        SpecPrBody.RenderDiscarded(Parse(SharedCut.Replace("\"shared\": [1, 2, 3],", string.Empty)
                .Replace("\"carries\": [4, 5]", "\"carries\": [1, 2, 3, 4, 5]")))
            .Should().NotContain("Carried by every spec");
    }

    private SpecSet Parse(string reply, IReadOnlyList<DerivedSpec>? head = null)
    {
        var parsed = _parser.Parse(
            reply, Key, Series, "1", Segments, head is null ? SpecSource.Derived : SpecSource.BranchArtifact, head);
        parsed.Error.Should().BeNull();
        return parsed.Derivation!.Set;
    }
}
