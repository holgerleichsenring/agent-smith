namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// p0504: one verification command a domain profile brings, in the order the
/// profile lists it. <paramref name="Stage"/> is the human label the verify
/// outcome is reported under; nothing switches on its value.
/// <para>
/// p0513: <paramref name="WhenPresent"/> is what the command NEEDS to be there — a
/// path, relative to the context's workdir, that must exist in the checkout. One
/// domain word covers repositories of different shapes, and a command measured
/// green on one shape says nothing about a repository that carries none of its
/// files. Absent path, skipped command: the verify gate stops at the first
/// non-zero exit, so a command failing on files it was never measured against
/// would HIDE the gates behind it. A command with no condition always runs.
/// </para>
/// </summary>
public sealed record DomainProfileCommand(string Stage, string Command, string? WhenPresent = null);
