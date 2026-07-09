using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: (de)serializes an OutcomeProposal to the JSON column on the
/// SpecDialogSession — an explicit kind discriminator plus per-kind payload,
/// so p0315c reads back exactly what was confirmed. Fails loudly on an
/// unknown kind in either direction.
/// </summary>
internal static class OutcomeProposalJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static string Write(OutcomeProposal proposal) =>
        JsonSerializer.Serialize(ToDto(proposal), JsonOptions);

    internal static OutcomeProposal Read(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Stored outcome JSON deserialized to null.");
        return FromDto(dto);
    }

    private static Dto ToDto(OutcomeProposal proposal) => proposal switch
    {
        BugOutcome bug => new Dto("bug", bug.Ticket, null, null, null),
        PhaseOutcome phase => new Dto("phase", null, phase.Draft, null, null),
        EpicOutcome epic => new Dto("epic", null, null, epic.Parent, epic.Children),
        _ => throw new InvalidOperationException(
            $"Outcome kind '{proposal.GetType().Name}' cannot be stored on a session."),
    };

    private static OutcomeProposal FromDto(Dto dto) => dto.Kind switch
    {
        "bug" => new BugOutcome(dto.Bug!),
        "phase" => new PhaseOutcome(dto.Phase!),
        "epic" => new EpicOutcome(dto.Parent!, dto.Children!),
        _ => throw new InvalidOperationException(
            $"Stored outcome JSON carries unknown kind '{dto.Kind}'."),
    };

    private sealed record Dto(
        string Kind,
        BugTicketDraft? Bug,
        PhaseDraft? Phase,
        PhaseDraft? Parent,
        IReadOnlyList<PhaseDraft>? Children);
}
