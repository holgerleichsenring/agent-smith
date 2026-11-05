using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-04-96a0b: names the uploads stored while a design turn ran. A turn lists the conversation's sets once, when
/// it opens its scopes, so a set stored after that is one its answer never read. The sets the turn held are stamped on
/// its outcome by set id; every set of the conversation missing there is named, by the address the next turn reads it
/// by, in a notice kept and sent before the reply.
/// </summary>
public sealed class SpecDialogUploadArrivals(ReferenceSetRepository sets)
{
    /// <summary>The notice text, one line per set the turn did not hold, or null when it held them all or failed.</summary>
    public async Task<string?> NoticeAsync(ConversationState state, SpecDialogTurnResult result, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind == SpecDialogTurnKind.Failure) return null;
        var uploaded = await sets.ListAsync(state.JobId, ct);
        var held = result.Outcome.Uploads.Select(upload => upload.SetId).ToHashSet(StringComparer.Ordinal);
        var lines = ReferenceScopeName.For(uploaded.Select(set => set.Name)).Zip(uploaded)
            .Where(pair => !held.Contains(pair.Second.SetId))
            .Select(pair => Line(pair.First, pair.Second.Files, result.Outcome is not AnswerOutcome))
            .ToList();
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static string Line(string address, int files, bool isProposal) =>
        $"{address} ({files} {(files == 1 ? "file" : "files")}) was uploaded while this answer was being written, "
        + "so the answer did not read it. The next answer can read it."
        + (isProposal ? " Approving this proposal does not read it." : string.Empty);
}
