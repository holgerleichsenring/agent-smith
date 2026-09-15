namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-15-d66f: what became of ONE artefact a delta declared, for one repository.
/// <para>
/// Per path, because the round-level answer cannot carry it: a repository that already holds a
/// ratified principles.md would otherwise report "preserved" for every artefact beside it — in
/// exactly the repositories that carry the drift these files exist to catch.
/// </para>
/// </summary>
/// <param name="Path">The repository-root-relative path the delta declared.</param>
/// <param name="Status">What happened to it.</param>
/// <param name="Reason">Why, when the status alone does not say — always set for
/// <see cref="ArtefactStatus.Refused"/>.</param>
public sealed record ArtefactWrite(string Path, ArtefactStatus Status, string? Reason = null);

/// <summary>What became of one declared artefact.</summary>
public enum ArtefactStatus
{
    /// <summary>The repository did not carry it and it was written.</summary>
    Written,

    /// <summary>
    /// The repository already carried it and it was left alone. A repository fans out one round
    /// per component against ONE checkout, so this is also what the second component of a
    /// repository sees for a root-relative artefact the first component just wrote —
    /// <see cref="AlreadyWrittenThisRound"/> keeps the two apart, because telling an operator
    /// their file was "ratified" when this run created it is a claim nobody made.
    /// </summary>
    PreservedExisting,

    /// <summary>An earlier round of THIS run wrote it; nothing was ratified.</summary>
    AlreadyWrittenThisRound,

    /// <summary>The path was not one a repository may be written at. Fails the round.</summary>
    Refused,
}
