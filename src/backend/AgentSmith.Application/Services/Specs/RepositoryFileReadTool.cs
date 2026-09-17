using System.ComponentModel;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: one file of one repository, read through the sandbox reader under
/// /work and bounded in size. The containment is the whole reason this is not a plain
/// read: the same reader wrote the registry credentials under /root a few steps earlier.
/// </summary>
public sealed class RepositoryFileReadTool(
    DerivationLook look, ISandboxFileReaderFactory files, ILogger logger)
{
    public const string Name = "read_file";

    /// <summary>Enough of a manifest, a lock file or a config to read what it declares;
    /// a derivation is not a code review.</summary>
    public const int MaxChars = 16_000;

    private const int ReadExit = 0;
    private const int MissingExit = 1;

    /// <summary>2026-09-15-ffa7: per look, because the id it spells is the holder's.</summary>
    public string Description =>
        "Reads one file of one repository, relative to the repository root, and returns its "
        + "content — a manifest, a lock file, a config. Read-only. The result starts with an "
        + $"evidence id such as [{look.Terms.EvidencePrefix}2]; a fact that rests on this file cites that id.";

    public async Task<string> ReadFile(
        [Description("The repository the file is in. Use one of the names listed as in scope.")]
        string repository,
        [Description("The path relative to the repository root, e.g. 'src/Api/Api.csproj'.")]
        string path,
        CancellationToken ct = default)
    {
        if (!ContainedPath.TryRelative(path, out var under) || under == ".")
            return ContainedPath.Refusal;
        if (!look.TryOpen(repository, out var sandbox, out var refusal)) return refusal;

        // 2026-09-17-042ed: on a read-only source scope the reader's null means any of "missing",
        // "refused" and "could not clone". The scope is opened first and the step result read
        // itself, so an absence the reviewer may state is told apart from a read that failed.
        var opened = sandbox is ISourceScopeSandbox scope ? new SourceScopeLook(scope) : null;
        var read = opened is null
            ? new SourceScopeRead(
                await files.Create(sandbox).TryReadAsync(ContainedPath.Absolute(under), ct), Ran: true)
            : await opened.TryOpenAsync(ct) is { } failure
                ? new SourceScopeRead(Content: null, Ran: false, failure)
                : await opened.ReadAsync(under, ct);
        var id = look.Evidence.Remember(
            repository, $"read {under}",
            read.Ran ? read.Content is null ? MissingExit : ReadExit : SourceScopeLook.NotRunExit,
            read.Ran);
        logger.LogInformation(
            "The {Actor} read {Repo}/{Path} — {Outcome} as {Id}",
            look.Terms.Actor, repository, under,
            read.Ran ? read.Content is null ? "absent" : $"{read.Content.Length} chars" : "could not run", id);
        if (!read.Ran) return $"[{id}] {repository}/{under} could not be read ({read.Error}), so this proves nothing.";
        return read.Content is null
            ? $"[{id}] {repository}/{under} does not exist."
            : $"[{id}] {repository}/{under}:\n" + BoundedResultTool.Bound(read.Content, MaxChars);
    }
}
