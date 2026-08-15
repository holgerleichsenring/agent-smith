namespace AgentSmith.Domain.Models;

/// <summary>
/// p0420: what one criterion of the ratified spec is accounted for by.
/// <para>
/// <see cref="Citation"/> is a path the diff is claimed to satisfy it at. It is checked
/// against the diff's own file list, so a criterion cannot be satisfied by a file the
/// phase never touched. That check is about INVENTION, not semantics: a real path may
/// still fail to mean what the account claims, which is the reviewer's twenty seconds.
/// </para>
/// </summary>
/// <param name="Mechanical">
/// True when the answer came from a command, not from a model — a green build is
/// evidence of a different kind and says so in the account.
/// </param>
public sealed record CriterionAccount(
    string Criterion,
    bool Satisfied,
    string? Citation = null,
    string? Note = null,
    bool Mechanical = false);
