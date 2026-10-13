using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Services.SpecRename;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Persistence.SpecRename;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967ah: the readers of owned data know the spec names alone. A payload in the shape the
/// release before the rename wrote — built from the store rewrite's frozen table, which is the only
/// place those names are still spelled — is not read as the spec it described.
/// </summary>
public sealed class SpecNameReadersTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-12T09:00:00Z");
    private readonly EventEnvelopeSerializer _codec = new();

    [Fact]
    public void Reader_PhaseNamedPayload_IsNotAccepted()
    {
        var recorded = SpecShape.Of(JsonSerializer.Serialize(new SpecRecordedEvent("r1", "s1", "spec: s1", T)));
        var oldType = SpecRenameTable.EventTypeNames.Single(r => r.New == nameof(EventType.SpecRecorded)).Old;
        var draft = new SpecDraft("s1", "Read", "spec: s1", []);
        var planned = new List<PlannedStepView> { new(4, CommandNames.SelectSpec, "Start the spec", "s1") };

        _codec.DeserializeRaw(oldType, recorded).Should().BeNull("an old event type name names no event");
        _codec.DeserializeRaw(nameof(EventType.SpecRecorded), recorded).Should().BeOfType<SpecRecordedEvent>()
            .Which.SpecId.Should().BeNull("the old member name is not read");
        JsonSerializer.Deserialize<SpecDraft>(SpecShape.Of(JsonSerializer.Serialize(draft)))!.SpecId.Should().BeNull();
        RunStoryJson.TryDeserialize<List<PlannedStepView>>(SpecShape.Of(RunStoryJson.Serialize(planned)))!
            .Single().SpecId.Should().BeNull();
    }

    [Fact]
    public void Reader_PhaseNamedOutcomeKind_IsNotAccepted()
    {
        var written = OutcomeProposalJson.Write(new SpecOutcome(new SpecDraft("s1", "Read", "spec: s1", [])));
        var old = written.Replace($"\"kind\":\"{SpecRenameTable.SpecKind}\"", $"\"kind\":\"{SpecRenameTable.OldSpecKind}\"");

        var act = () => OutcomeProposalJson.Read(old);

        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown kind*");
    }
}
