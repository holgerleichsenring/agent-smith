using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Which mock version a person ratified, per spec id, as the conversation's transcript says it: the
/// newest ratification turn of a spec wins, so a later ratification supersedes an earlier one.
/// </summary>
public static class RatifiedMocks
{
    public static IReadOnlyDictionary<string, int> Of(IEnumerable<TranscriptTurn> transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        var ratified = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var turn in transcript)
            if (turn is { Kind: SpecDialogTurnKind.MockRatified, Mock: { } mock })
                ratified[mock.SpecId] = mock.Version;
        return ratified;
    }
}
