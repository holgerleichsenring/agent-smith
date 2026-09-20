using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-6f8d: ONE declaration's proof, read out of the scope that declaration owns —
/// the <c>verify:</c> block of the context.yaml it names — and the sentence that says what
/// was found.
/// <para>
/// Split from <see cref="TemplateProofReport"/>, which sat four lines under the file-length
/// ceiling when its recording loop gained the ownership check. The read and the loop are two
/// responsibilities either way: what one declaration states, and which declarations are
/// entitled to be read at all.
/// </para>
/// </summary>
public sealed class TemplateProofRead(
    IContextYamlSerializer contextYaml,
    ISandboxFileReaderFactory files,
    ILogger<TemplateProofRead> logger)
{
    /// <summary>A read that reached a verdict about the declaration.</summary>
    public const int Read = 0;

    /// <summary>...and one that did not, so it proves nothing either way.</summary>
    public const int Unread = 1;

    // The sentence is the report: the evidence line carries the id and the repository, so
    // WHAT was found has to live in the clause, or the count is a list of reads with no
    // outcome attached to any of them.
    public async Task<(string What, int Exit, string? Path)> OfAsync(
        ProjectTemplate declared, ISourceScopeSandbox scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(scope);
        var path = $"{ProjectMetaPaths.Contexts}/{declared.TemplateContext}"
            + $"/{ProjectMetaPaths.ContextYamlFile}";
        // The revision it LANDED on, never the one that was asked for: a template pinned to a
        // branch declares whatever that branch held at this moment, and the count is of a sha.
        var read = $"read {path} at {scope.ResolvedSha ?? "the revision it landed on"}";
        string? content;
        try
        {
            content = await files.Create(scope)
                .TryReadAsync(ContainedPath.Absolute(path), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The template at {Path} could not be read", path);
            return ($"{read}: the template could not be read — {ex.Message}", Unread, path);
        }
        var (what, exit) = content is null
            ? ($"{read}: the template carries no context under that name", Unread)
            : Declared(read, content);
        return (what, exit, path);
    }

    private (string What, int Exit) Declared(string read, string content)
    {
        ContextYamlParseResult parsed;
        try
        {
            parsed = contextYaml.Parse(content);
        }
        catch (InvalidOperationException ex)
        {
            // The parse throws on a context.yaml missing meta.workdir. That is a broken
            // template, not a template without proof, and the two must not count as one.
            return ($"{read}: the template's context.yaml is not readable — {ex.Message}", Unread);
        }
        if (parsed.ErrorReason is { } bad)
            return ($"{read}: the template's context.yaml is not readable — {bad}", Unread);
        if (parsed.Summary is null)
            return ($"{read}: the template's context.yaml declares no context this reads", Unread);
        // A verify block declaring no VALID stage parses to null, and that null is the whole
        // finding: the template states nothing about how it is proven.
        var stages = parsed.Summary.Verify;
        return stages is null || stages.Count == 0
            ? ($"{read}: the template declares NO verify stage — it proves nothing about itself", Read)
            : ($"{read}: the template declares {stages.Count} verify stage(s) — "
                + string.Join(", ", stages.Select(stage => stage.Label)), Read);
    }
}
