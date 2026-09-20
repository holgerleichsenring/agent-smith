using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-19-4c1f: the meta files of one kind that a remote grounding read brought back
/// from one repository, and the ones it was refused. The two lists together are what
/// separates the second absence from the third: a kind with no documents and no refusals
/// was looked for and is not there, while a refusal says nothing about whether it is.
/// </summary>
public sealed record RemoteFileSet(
    IReadOnlyList<ContextDocument> Documents, IReadOnlyList<string> Unreadable)
{
    public static readonly RemoteFileSet Empty = new([], []);

    /// <summary>The location was read and carries no file of this kind.</summary>
    public bool IsAbsent => Documents.Count == 0 && Unreadable.Count == 0;
}

/// <summary>
/// 2026-09-19-4c1f: what one scoped repository contributed to the design turn's grounding.
/// <see cref="Located"/> is false for a repository that names neither a url nor a path —
/// the first absence, and the one no read was ever attempted for.
/// <see cref="Unreachable"/> carries the reason when the repository itself could not be
/// listed, which is the third absence at whole-repository scale.
/// </summary>
public sealed record DialogGrounding(
    string Repo,
    bool Located,
    string? Unreachable,
    RemoteFileSet Contexts,
    RemoteFileSet Principles)
{
    public static DialogGrounding NotLocated(string repo) =>
        new(repo, false, null, RemoteFileSet.Empty, RemoteFileSet.Empty);

    public static DialogGrounding Unreached(string repo, string reason) =>
        new(repo, true, reason, RemoteFileSet.Empty, RemoteFileSet.Empty);
}
