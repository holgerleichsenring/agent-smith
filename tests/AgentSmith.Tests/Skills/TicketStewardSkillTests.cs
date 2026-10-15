using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// The ticket steward's master as the PINNED catalog ships it, held against the surface a steward turn is composed
/// with. A tool the text names but the surface lacks is a ghost the model reaches for and never finds; this keeps the
/// skill text and the backend in step.
/// </summary>
public sealed partial class TicketStewardSkillTests
{
    /// <summary>First agent-smith-skills release carrying ticket-steward-master (1.0.0).</summary>
    private static readonly Version StewardRelease = new(5, 17, 0);

    private static readonly string[] StewardActs =
        ["start_next_attempt", "read_series", "edit_series", "verify_series", "ticket_runs"];

    [GeneratedRegex(@"`([a-z][a-z0-9]*(?:_[a-z0-9]+)*)[`(]")]
    private static partial Regex BacktickedIdentifier();

    private static string Master() => PackagedMaster.Read(PipelinePresets.TicketStewardMaster);

    [Fact]
    public void TicketStewardSkill_IsInThePinnedCatalog()
    {
        PackagedMaster.Pin.Should().BeGreaterThanOrEqualTo(StewardRelease,
            "the embedded catalog must carry the master a worked ticket's conversation asks for");

        var master = Master();

        master.Should().Contain($"name: {PipelinePresets.TicketStewardMaster}").And.Contain("role: master");
    }

    [Fact]
    public void TicketStewardSkill_WaitingStart_SaysWhatItWaitsForAndThatNothingStarted() =>
        Master().Should().Contain(
            "When `start_next_attempt` answers that the ticket waits for other tickets, tell the person which "
            + "tickets it waits for and that nothing was started: once those are merged or marked done by hand, "
            + "they ask for the attempt again.",
            $"the pin is {PackagedMaster.Pin} and the waiting-start sentence ships from v5.18.0");

    [Fact]
    public async Task TicketStewardSkill_NamesOnlyToolsTheStewardOffers()
    {
        var steward = await BoundDialogTurn.MasterSurface(PipelinePresets.TicketStewardMaster);
        var designPartner = await BoundDialogTurn.MasterSurface("design-partner-master");
        var known = designPartner.Concat(steward).ToHashSet(StringComparer.Ordinal);

        var named = BacktickedIdentifier().Matches(Master()).Select(m => m.Groups[1].Value)
            .Where(known.Contains).Distinct().ToList();

        named.Should().Contain(StewardActs, "the steward answers and acts through these tools");
        named.Should().OnlyContain(tool => steward.Contains(tool),
            $"every tool the master names must be on the steward surface ({string.Join(", ", steward)})");
    }
}
