using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-25-8c97: whether the caller's half of the product came from the same build this
/// one did. It answers in findings rather than in a verdict, because the findings channel is
/// already what an installation reports its own state through — and it answers ADVISORY,
/// because two halves of different builds coexisting is what a rolling update looks like
/// from the inside, not a fault.
/// </summary>
public interface IBuildMismatchDetector
{
    /// <summary>
    /// The findings a caller identifying itself as <paramref name="callerRevision"/> should
    /// see — one when the builds differ and the difference has outlived a rolling update,
    /// none otherwise. Never throws and never refuses: the caller asked what is wrong with
    /// the installation, not for permission to talk.
    /// </summary>
    IReadOnlyList<StartupFinding> Compare(string? callerRevision);
}
