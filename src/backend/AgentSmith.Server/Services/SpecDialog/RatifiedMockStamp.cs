using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Before an approved proposal is filed, each draft's top-level <c>ratified_mock</c> is set to the version a
/// person ratified in the conversation, or REMOVED when nobody did — a value the model wrote never survives.
/// <para>
/// THE TRANSCRIPT IS READ THROUGH A FRESH SCOPE. The turn's scope tracks the session since it appended the
/// assistant turn, and a ratification made in another request while the approval waited is newer than that
/// tracked copy.
/// </para>
/// </summary>
public sealed class RatifiedMockStamp(IServiceScopeFactory scopes)
{
    private const string Key = "ratified_mock";

    public async Task<OutcomeProposal> StampAsync(ConversationState state, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (proposal is not (SpecOutcome or EpicOutcome)) return proposal;
        using var scope = scopes.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>()
            .GetBySessionIdAsync(state.JobId, ct);
        var ratified = session is null
            ? new Dictionary<string, int>()
            : RatifiedMocks.Of(SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson));
        return Stamp(proposal, ratified);
    }

    internal static OutcomeProposal Stamp(OutcomeProposal proposal, IReadOnlyDictionary<string, int> ratified) =>
        proposal switch
        {
            SpecOutcome spec => spec with { Draft = Stamp(spec.Draft, ratified) },
            EpicOutcome epic => epic with
            {
                Parent = Stamp(epic.Parent, ratified),
                Children = [.. epic.Children.Select(child => Stamp(child, ratified))],
            },
            _ => proposal,
        };

    internal static SpecDraft Stamp(SpecDraft draft, IReadOnlyDictionary<string, int> ratified)
    {
        int? version = ratified.TryGetValue(draft.SpecId, out var n) ? n : null;
        var body = string.Join('\n', Without(draft.Yaml.Split('\n'))).TrimEnd();
        var yaml = version is { } v ? $"{body}\n{Key}: {v}" : body;
        return draft with { Yaml = yaml, RatifiedMock = version };
    }

    // The stated key is its line and every line under it that is indented or blank.
    private static IEnumerable<string> Without(IEnumerable<string> lines)
    {
        var inKey = false;
        foreach (var line in lines)
        {
            if (line.StartsWith(Key + ":", StringComparison.Ordinal)) { inKey = true; continue; }
            if (inKey && (line.Length == 0 || char.IsWhiteSpace(line[0]))) continue;
            inKey = false;
            yield return line;
        }
    }
}
