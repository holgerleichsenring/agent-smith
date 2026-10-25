using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-25-54bea: what one opened template ADVISES the coding master — the principles of the
/// context it declares, its Limits fence, and the sentence that says how it proves itself.
/// <para>
/// Advice and never a gate: the models decide, so nothing here is held against the diff. The
/// principles of the declared context win over the template's root file, the way a context's
/// own principles win in the target.
/// </para>
/// </summary>
public sealed class MasterTemplateAdvice(
    ISandboxFileReaderFactory files, TemplateProofRead proof, StructureLimits limits,
    ILogger<MasterTemplateAdvice> logger)
{
    /// <summary>The labelled section for one template, read out of its materialised scope.</summary>
    public async Task<string> ReadAsync(
        string address, ProjectTemplate declared, ISourceScopeSandbox scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(scope);
        var (path, principles) = await PrinciplesAsync(declared, scope, cancellationToken);
        var (proven, _, _) = await proof.OfAsync(declared, scope, cancellationToken);
        return $"\n\n### What template `{address}` advises\n"
            + Body(declared, path, principles)
            + Shape(limits.Read(principles))
            + $"How the template proves itself: {proven}.\n";
    }

    private async Task<(string? Path, string? Content)> PrinciplesAsync(
        ProjectTemplate declared, ISourceScopeSandbox scope, CancellationToken cancellationToken)
    {
        var reader = files.Create(scope);
        foreach (var path in Candidates(declared))
        {
            try
            {
                var content = await reader.TryReadAsync(ContainedPath.Absolute(path), cancellationToken);
                if (content is not null) return (path, content);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "The template's principles at {Path} could not be read", path);
            }
        }
        return (null, null);
    }

    private static string[] Candidates(ProjectTemplate declared) =>
    [
        $"{ProjectMetaPaths.Contexts}/{declared.TemplateContext}/{ProjectMetaPaths.PrinciplesFile}",
        ProjectMetaPaths.Principles,
    ];

    private static string Body(ProjectTemplate declared, string? path, string? principles) =>
        principles is null
            ? $"The template carries no principles file — neither `{Candidates(declared)[0]}` nor "
                + $"`{ProjectMetaPaths.Principles}` — so it advises by its code alone.\n"
            : $"The template's rules, from `{path}`. Follow them for NEW units — a new file, type or "
                + "component. Existing code you extend and the spec win where they disagree; this is "
                + "advice, never a gate.\n\n" + principles.Trim() + "\n\n";

    private static string Shape(StructureLimitSet set) =>
        set.IsEmpty
            ? string.Empty
            : "The template's shape (its Limits fence): "
                + string.Join(", ", set.ByMetric.Select(m => $"{m.Key} {m.Value}"))
                + (set.Tolerance > 0 ? $", tolerance {set.Tolerance}%" : string.Empty) + ".\n";
}
