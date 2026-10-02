using System.Collections.Concurrent;
using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Design;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Design;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-7f7ab: design_read — a Figma link in, a trimmed node summary out, read through the
/// server-side client that holds the token. The model names a link and, with several sources, a
/// source; it never sees, sends or receives the token. Variables are read once per file and
/// degrade to a stated absence: the node values stand on their own. Only an answered variables
/// read is kept, so a rate-limited one is asked again on the next call.
/// <para>2026-10-01-7f7ae: an answered read is recorded on the run (when the pipeline has one);
/// a cited version is compared with the one read, and read on request.</para>
/// <para>2026-10-01-7f7ac: the read node is also exported as PNG and deposited for the model to
/// see; the summary says whether a render follows or why not.</para>
/// </summary>
public sealed class DesignReadToolHost(
    IFigmaClient figma, IReadOnlyList<DesignSource> sources, DesignReadRecorder? recorder = null,
    DesignNodeRender? render = null) : IToolHost
{
    private const int NodeBudget = 16_000;
    private const int VariableBudget = 6_000;
    private const int DefaultDepth = 3;
    private const int MaxDepth = 6;
    private readonly ConcurrentDictionary<string, FigmaReadResult> _variables = new(StringComparer.Ordinal);

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode) =>
        [AIFunctionFactory.Create(DesignRead, name: "design_read")];

    [Description("Reads a Figma frame or component from its link and returns a summary for building it: file version, per node its type, name, size, auto-layout, colours as hex, corner radius, text with font, the component an instance is of, style and variable names, then the file's variables per mode — and a PNG render of the node, shown after the result where images can be shown. The link must carry node-id.")]
    public async Task<string> DesignRead(
        [Description("The Figma link, e.g. https://www.figma.com/design/<key>/<title>?node-id=1-2")] string url,
        [Description("The design source to read through; needed only when the project has several.")] string? source = null,
        [Description("How many levels below the node to read, 1-6 (default 3).")] int? depth = null,
        [Description("The file version the ticket cites; the answer says whether the design moved since.")] string? expected_version = null,
        [Description("True reads expected_version itself instead of the current version.")] bool? read_version = null,
        CancellationToken ct = default)
    {
        if (!FigmaLink.TryParse(url, out var link))
            return "Error: not a readable Figma link. Expected https://www.figma.com/design|file|proto/<key>/...?node-id=<id>.";
        if (link.NodeId is null)
            return "Error: the link names no node. Copy the link of the frame to read (it carries node-id) and call again.";
        if (Pick(source) is not { } chosen)
            return $"Error: name the design source to read through: one of {string.Join(", ", sources.Select(s => s.Name))}.";
        var cited = read_version == true ? expected_version?.Trim() : null;
        if (read_version == true && string.IsNullOrEmpty(cited))
            return "Error: read_version needs expected_version — the version to read.";
        var nodes = await figma.GetNodesAsync(
            chosen.SecretName, link.ApiFileKey, link.NodeId, Math.Clamp(depth ?? DefaultDepth, 1, MaxDepth), cited, ct);
        if (nodes.Body is not { } body)
            return Failed(nodes.Failure!);
        if (recorder is not null) await recorder.RecordAsync(chosen.Name, link, body, ct);
        var image = render is null ? string.Empty : await render.RenderAsync(chosen, link, body, ct) + "\n";
        var variables = await VariablesAsync(chosen, link.ApiFileKey, ct);
        var names = variables.Body is { } vars ? FigmaVariableSummary.Names(vars) : new Dictionary<string, string>();
        return $"{DesignVersionNote.Render(expected_version, cited is not null, body)}source: {chosen.Name}\n{image}{FigmaNodeSummary.Render(body, names, NodeBudget)}\n\n{VariablesText(variables)}";
    }

    private DesignSource? Pick(string? name) => string.IsNullOrWhiteSpace(name)
        ? sources.Count == 1 ? sources[0] : null
        : sources.FirstOrDefault(s => ConfigNames.Comparer.Equals(s.Name, name.Trim()));

    private async Task<FigmaReadResult> VariablesAsync(DesignSource source, string fileKey, CancellationToken ct)
    {
        var key = $"{source.Name}\n{fileKey}";
        if (_variables.TryGetValue(key, out var kept)) return kept;
        var read = await figma.GetLocalVariablesAsync(source.SecretName, fileKey, ct);
        if (read.Body is not null) _variables[key] = read;
        return read;
    }

    private static string Failed(FigmaReadFailure failure) =>
        $"design_read failed: {failure.KindWord} — {failure.Detail}"
        + (failure.RetryAfter is { } wait ? $"; retry after {Math.Ceiling(wait.TotalSeconds)}s" : "");

    private static string VariablesText(FigmaReadResult variables) => variables.Body is { } body
        ? FigmaVariableSummary.Render(body, VariableBudget)
        : $"variables: unavailable ({variables.Failure!.KindWord} — {variables.Failure.Detail})"
          + (variables.Failure.Kind is FigmaReadFailureKind.Forbidden or FigmaReadFailureKind.NotFound
              ? "; the Variables API needs a Figma Enterprise plan and a token with file_variables:read" : "")
          + ". The values above are those set on the nodes; no variable is named that was not read.";
}
