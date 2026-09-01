using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-01-6c32: what a master's closing answer turned out to hold, decided by the
/// parser that reads it rather than by a stricter check in front of that parser.
/// <para>
/// Three outcomes, and the difference between them is the whole point. A valid array —
/// even an empty one — is a real triage: the master looked and kept nothing.
/// <see cref="Recovered"/> means the array was cut off mid-write and the observations it
/// still held were salvaged object by object; that ships, but never as a clean triage.
/// A non-null <see cref="Rejection"/> means the answer was not findings at all.
/// </para>
/// </summary>
public sealed record MasterAnswerReading(
    IReadOnlyList<SkillObservation> Observations,
    bool Recovered,
    string? Rejection)
{
    /// <summary>The answer could not be read as findings at all.</summary>
    public bool IsRejected => Rejection is not null;
}
