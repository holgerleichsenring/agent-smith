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

    [Description("Reads one file of one repository, relative to the repository root, and "
                 + "returns its content — a manifest, a lock file, a config. Read-only. The "
                 + "result starts with an evidence id such as [L2]; a fact that rests on "
                 + "this file cites that id.")]
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

        var content = await files.Create(sandbox).TryReadAsync(ContainedPath.Absolute(under), ct);
        var id = look.Evidence.Remember(
            repository, $"read {under}", content is null ? MissingExit : ReadExit, ran: true);
        logger.LogInformation(
            "The derivation read {Repo}/{Path} — {Outcome} as {Id}",
            repository, under, content is null ? "absent" : $"{content.Length} chars", id);
        return content is null
            ? $"[{id}] {repository}/{under} does not exist."
            : $"[{id}] {repository}/{under}:\n" + BoundedResultTool.Bound(content, MaxChars);
    }
}
