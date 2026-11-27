using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-27-3532c: the series master's readable sources for one beat, and the design turn's four reads over them —
/// read_file, grep_in_tree, find_files and directory_tree, each path routed by the address it starts with. The first
/// source is the default one; a second source of one address is dropped (logged), so the prompt lists exactly what the
/// reads route to. Disposing it disposes every scope it was given: an unread one costs nothing, an upload goes back to
/// its conversation's hold, and one that fails to dispose is logged and the rest still go.
/// </summary>
public sealed class SeriesSourceMap : IAsyncDisposable
{
    private readonly IReadOnlyList<SeriesSource> _given;
    private readonly ILogger? _logger;

    public SeriesSourceMap(IReadOnlyList<SeriesSource> sources, ILogger? logger = null)
    {
        _given = sources;
        _logger = logger;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<SeriesSource> kept = [];
        foreach (var source in sources)
            if (seen.Add(source.Address)) kept.Add(source);
            else logger?.LogWarning("A second read scope of address {Address} is dropped", source.Address);
        Sources = kept;
    }

    public static SeriesSourceMap None => new([]);

    public IReadOnlyList<SeriesSource> Sources { get; }

    public IEnumerable<AIFunction> ReadTools(ILogger? logger = null)
    {
        if (Sources.Count == 0) return [];
        var map = Sources.ToDictionary(s => s.Address, s => (ISandbox)s.Scope, StringComparer.Ordinal);
        var fs = new FilesystemToolHost(map, Sources[0].Address, logger: logger);
        return fs.GetTools(SkillExecutionPhase.BootstrapDiscover, investigatorMode: null)
            .Where(tool => AgenticToolSurface.SpecDialogReads.Contains(tool.Name)).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _given)
        {
            try { await source.Scope.DisposeAsync(); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "The read scope {Address} did not dispose", source.Address);
            }
        }
    }
}
