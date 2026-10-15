using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>A fold that is due: the indexes of the first and last turn it folds, those turns oldest first, and the
/// text of the digest it carries forward, null for a conversation's first fold.</summary>
public sealed record TranscriptFold(int From, int Through, IReadOnlyList<SpecDialogTurn> Turns, string? PreviousDigest);
