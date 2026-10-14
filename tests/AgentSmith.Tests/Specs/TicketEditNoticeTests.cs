using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-5cd2: what the model and the author are told about an edited ticket —
/// the prompt names the revision the change postdates and keeps the executed specs,
/// the comment names the re-cut and the kept specs — and the index carries the
/// fingerprint the comparison reads.
/// </summary>
public sealed class TicketEditNoticeTests
{
    [Fact]
    public void PreviousCut_ATicketEdit_NamesTheRevisionTheChangePostdates()
    {
        var previous = Set(SpecRevisionCause.Initial, SpecRevisionCause.Retrigger);

        var section = PreviousCutPromptSection.Render(previous, SpecRevisionCause.TicketEdit, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults);

        section.Should().Contain("The ticket text changed since revision 2 was cut.");
        section.Should().Contain("cut the specs that have not started from it");
        section.Should().Contain("p1a (EXECUTED — keep exactly as it is)");
        section.Should().Contain("p1b (not started)");
    }

    [Fact]
    public void PreviousCut_AnyOtherCause_SaysNothingAboutAnEdit()
    {
        var section = PreviousCutPromptSection.Render(Set(SpecRevisionCause.Initial), SpecRevisionCause.Retrigger, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults);

        section.Should().NotContain("The ticket text changed");
    }

    [Fact]
    public void Comment_ATicketEditRevision_NamesTheRecutAndTheKeptSpecs()
    {
        var body = SpecSetComment.Render(Set(SpecRevisionCause.Initial, SpecRevisionCause.TicketEdit), null, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults);

        body.Should().Contain(
            "The ticket text changed since revision 1 was cut: p1a already ran and stayed as it was; "
            + "the rest was cut again from the current text.");
    }

    [Fact]
    public void Comment_ATicketEditBeforeAnySpecRan_SaysTheWholeSetWasCutAgain()
    {
        var set = Set(SpecRevisionCause.Initial, SpecRevisionCause.TicketEdit) with { Executed = [] };

        SpecSetComment.Render(set, null, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().Contain("no spec had run yet, so the whole set was cut again");
    }

    [Fact]
    public void Comment_AnyOtherRevision_SaysNothingAboutAnEdit()
    {
        SpecSetComment.Render(Set(SpecRevisionCause.Initial), null, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().NotContain("The ticket text changed");
    }

    [Fact]
    public void SeriesManifest_RoundTripsTheTicketFingerprint()
    {
        var index = new SeriesManifest();
        var stamped = Set(SpecRevisionCause.Initial) with { TicketFingerprint = "abc123" };
        var unstamped = Set(SpecRevisionCause.Initial);

        index.FingerprintOf(index.Parse(index.Serialize(stamped))!).Should().Be("abc123");
        index.Serialize(unstamped).Should().NotContain("ticket_fingerprint",
            "a set without a fingerprint serializes exactly as before this spec");
        index.FingerprintOf(index.Parse(index.Serialize(unstamped))!).Should().BeNull();
    }

    private static SpecSet Set(params string[] causes) => new(
        "azdo-1",
        [Spec("p1a", "Introduce the guard"), Spec("p1b", "Move the callers")],
        SpecAccounting.Empty,
        [.. causes.Select((cause, i) => new SpecRevision(i + 1, cause, DateTimeOffset.UtcNow))],
        SpecSource.BranchArtifact,
        ExecutedSpecIds: ["p1a"]);

    private static DerivedSpec Spec(string id, string goal) => new(
        new SpecDraft(id, goal, $"spec: {id}\ngoal: \"{goal}\"", []) { Done = [$"{goal} is done."] },
        id, string.Empty, []);
}
