using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>What a resume did: moved the session here, found nothing to move, or refused.</summary>
public abstract record SpecDialogResumeResult;

/// <summary>The session now lives on the current thread and is open.</summary>
public sealed record SpecDialogResumed(ConversationState State) : SpecDialogResumeResult;

/// <summary>No session by that id, or one owned by somebody else — the same answer for both.</summary>
public sealed record SpecDialogResumeNotFound : SpecDialogResumeResult;

/// <summary>
/// The session is mid-turn and stays where it is. <paramref name="QuestionPending"/> says
/// whether the turn is waiting on an answer rather than still working.
/// </summary>
public sealed record SpecDialogResumeRefused(string SessionId, bool QuestionPending) : SpecDialogResumeResult;
