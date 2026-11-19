using AgentSmith.Tests.Specs.Review;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>2026-11-17-ec48a: a revised text is read as filing reads a spec, and stays this spec.</summary>
public sealed class SpecRevisionTextTests
{
    private static readonly AgentSmith.Contracts.Models.SpecDraft Current =
        CutRevisionFixtures.Draft() with { Repos = ["shop"] };

    [Fact]
    public void Read_ValidChangedText_IsTheDraftWithTheCurrentRepositories()
    {
        var (draft, refusal) = SpecRevisionRig.Text().Read(Current, SpecRevisionRig.RevisedYaml);

        refusal.Should().BeNull();
        draft!.Done.Should().Contain(SpecRevisionRig.Revised);
        draft.Repos.Should().Equal("shop");
    }

    [Fact]
    public void Read_FencedText_IsRead()
    {
        var (draft, _) = SpecRevisionRig.Text().Read(Current, CutRevisionFixtures.Reply(SpecRevisionRig.RevisedYaml));

        draft.Should().NotBeNull();
    }

    [Theory]
    [InlineData("goal: [unclosed", "invalid")]
    [InlineData("spec: 2026-10-14-5678a\n", "invalid")]
    public void Read_SchemaInvalid_IsRefused(string yaml, string expected)
    {
        var (draft, refusal) = SpecRevisionRig.Text().Read(Current, yaml);

        draft.Should().BeNull();
        refusal.Should().Contain(expected);
    }

    [Theory]
    [InlineData("spec: 2026-10-14-5678a", "spec: 2026-10-14-9999a")]
    [InlineData("requires: [2026-10-14-1234a]", "requires: []")]
    [InlineData("applies_to: \"backend (export)\"", "applies_to: \"frontend\"")]
    public void Read_IdentityMoved_IsRefused(string from, string to)
    {
        var (draft, refusal) = SpecRevisionRig.Text().Read(Current, CutRevisionFixtures.With(from, to, SpecRevisionRig.RevisedYaml));

        draft.Should().BeNull();
        refusal.Should().Contain("keeps spec");
    }

    [Fact]
    public void Read_SameSpecReformatted_IsRefusedAsUnchanged()
    {
        var (draft, refusal) = SpecRevisionRig.Text().Read(Current,
            CutRevisionFixtures.With("# yaml-language-server: $schema=../../spec.schema.json\n", string.Empty));

        draft.Should().BeNull();
        refusal.Should().Contain("nothing to revise");
    }

    // 2026-11-19-6b0ba: a stand-in states no applies_to; the spec its master writes may add one, and only to a stand-in.
    [Fact]
    public void Read_AStandInWithoutAppliesTo_MayGainOne()
    {
        var bare = CutRevisionFixtures.Draft(CutRevisionFixtures.With("applies_to: \"backend (export)\"\n", "", CutRevisionFixtures.Yaml));
        var revised = CutRevisionFixtures.With(SpecRevisionRig.Criterion, SpecRevisionRig.Revised);

        SpecRevisionRig.Text().Read(bare, revised, isStandIn: true).Draft.Should().NotBeNull();
        SpecRevisionRig.Text().Read(bare, revised).Refusal.Should().Contain("keeps spec");
    }
}
