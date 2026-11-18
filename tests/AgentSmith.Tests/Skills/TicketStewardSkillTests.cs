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

    // 2026-11-18-550ac: steward 1.2.3 drops the waiting-start sentence; the refusal itself names the tickets it waits
    // for (RequiresUnmetGate, OperatorChannelTests) and the steward passes it on as it is.
    [Fact]
    public void TicketStewardSkill_StartRefusal_IsPassedOnAsTheToolGaveIt() =>
        PackagedMaster.Folded(PipelinePresets.TicketStewardMaster).Should().Contain(
            "`start_next_attempt` answers that the attempt was requested or why it was refused; pass that on as it is.",
            $"the pin is {PackagedMaster.Pin}: a refusal reaches the person in the tool's words");

    // 2026-11-18-550ac: steward 1.2.3 reads what its answer depends on; the narrow-read and re-analyse rules are gone.
    [Fact]
    public void TicketStewardSkill_ReadsWhatItsAnswerDependsOn() =>
        PackagedMaster.Folded(PipelinePresets.TicketStewardMaster).Should().Contain("Read what your answer depends on")
            .And.Contain("separate what you checked from what you infer")
            .And.NotContain("Read narrowly").And.NotContain("re-analyse");

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
