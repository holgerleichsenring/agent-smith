using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: what a context.yaml says about ownership for a bootstrap round — the sources
/// of the one the round just wrote, and the workdir of the one it started from. A file that does
/// not parse, or cannot be read, says nothing: no sources, no earlier workdir.
/// </summary>
public sealed class StructureContextSources(
    IContextYamlParser parser, ISandboxFileReaderFactory files, ILogger<StructureContextSources> logger)
{
    /// <summary>The sources the context's context.yaml in the sandbox declares, empty when none.</summary>
    public async Task<IReadOnlyList<string>> WrittenAsync(ISandbox sandbox, string contextName, CancellationToken ct)
    {
        var path = $"{ProjectMetaPaths.MetaDirFor(contextName)}/{ProjectMetaPaths.ContextYamlFile}";
        try
        {
            return Summary(await files.Create(sandbox).TryReadAsync(path, ct))?.Sources ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // As the baseline itself: what stops the read is said, never thrown — the round goes on.
            logger.LogWarning(ex, "{Context}: sources not read from {Path}; the workdir alone is owned", contextName, path);
            return [];
        }
    }

    /// <summary>The workdir a context.yaml declares, or null when there is none or it does not parse.</summary>
    public string? Workdir(string? contextYaml) => Summary(contextYaml)?.Workdir;

    private ContextYamlSummary? Summary(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        try
        {
            return parser.Parse(yaml).Summary;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogDebug(ex, "context.yaml carries no workdir; it names no ownership");
            return null;
        }
    }
}
