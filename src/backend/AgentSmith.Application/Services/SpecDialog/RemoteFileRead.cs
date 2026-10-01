using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-19-4c1f: one remote meta-file read for the design turn's grounding, lifted out of
/// <see cref="DialogGroundingReader"/> by 2026-10-01-283dg so a third kind (DESIGN.md) reads
/// through the same path. The provider contract answers an absent path with null and
/// propagates auth and transport errors, so a throw here is a file that could not be READ — a
/// different answer from "it is not there", and the report has to keep it different.
/// </summary>
public sealed class RemoteFileRead(ILogger<RemoteFileRead> logger)
{
    public async Task<string?> TryAsync(
        ISourceProvider provider, string repo, string path, List<string> unreadable, CancellationToken ct)
    {
        try
        {
            var content = await provider.TryReadFileAsync(path, ct);
            return string.IsNullOrEmpty(content) ? null : content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Grounding {Repo}: read of {Path} failed.", repo, path);
            unreadable.Add($"{path} ({ex.Message})");
            return null;
        }
    }

    /// <summary>A file that speaks for the whole repository: one document or none, and the
    /// refusal when the read was refused.</summary>
    public async Task<RemoteFileSet> SingleAsync(
        ISourceProvider provider, string repo, string path, CancellationToken ct)
    {
        var unreadable = new List<string>();
        var content = await TryAsync(provider, repo, path, unreadable, ct);
        return new RemoteFileSet(
            content is null ? [] : [new ContextDocument(repo, null, null, path, content)], unreadable);
    }
}
