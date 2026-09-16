using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-13-9f84: reads what a template declares as its OWN proof — the <c>verify:</c>
/// block of its context.yaml — while the scope the derivation opened is still standing, and
/// puts the answer on the record as a framework evidence line.
/// <para>
/// A template is held up as exemplary and NOTHING checks that. Nothing can be refused for
/// failing to prove itself before the estate is countable: how many declared templates
/// declare any proof at all. None of the boilerplates carries a verify block today, so a
/// gate would refuse every one of them on day one and its cheapest exit would be one
/// trivial stage. So this reports, and a successor refuses once the count says a template
/// can pass.
/// </para>
/// <para>
/// NOTHING IS MATERIALISED HERE. 2026-09-13-84c0 decided the derivation's template scopes
/// stay lazy, because an eager clone spends a pod on every derivation of every project that
/// declares a template — including the many where nobody looks. A scope that never opened is
/// therefore not read and not reported: a line about a file nobody fetched would be a
/// measurement nobody took.
/// </para>
/// </summary>
public sealed class TemplateProofReport(
    IContextYamlSerializer contextYaml,
    ISandboxFileReaderFactory files,
    ILogger<TemplateProofReport> logger)
{
    // A read that reached a verdict about the declaration, and one that did not.
    private const int Read = 0;
    private const int Unread = 1;

    /// <summary>
    /// One line per template the derivation actually opened, minted once per derivation.
    /// A run with no template, or one whose templates nobody opened, is silent — and never
    /// fails: what a template declares is REPORTED here, never refused.
    /// </summary>
    public async Task RecordAsync(
        DerivationLook? look, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (look is null || look.Templates.Count == 0) return;
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
            || project is null) return;
        foreach (var declared in project.Templates)
        {
            // 2026-09-16-4df5: through the one builder. Composed here by hand, two
            // declarations of one context name resolved to one scope and one evidence key, so
            // the second was never proven while the report read as covered.
            var name = TemplateScopeName.For(declared);
            if (!look.Templates.TryGetValue(name, out var scope) || !scope.IsMaterialized) continue;
            var (what, exit) = await ReadAsync(declared, scope, cancellationToken);
            var id = look.Evidence.RememberOnce(name, what, exit, ran: exit == Read);
            if (id is not null) logger.LogInformation("[{Id}] {Name}: {What}", id, name, what);
        }
    }

    // The sentence is the report: the evidence line carries the id and the repository, so
    // WHAT was found has to live in the clause, or the count is a list of reads with no
    // outcome attached to any of them.
    private async Task<(string What, int Exit)> ReadAsync(
        ProjectTemplate declared, ISourceScopeSandbox scope, CancellationToken cancellationToken)
    {
        var path = $"{ProjectMetaPaths.Contexts}/{declared.TemplateContext}"
            + $"/{ProjectMetaPaths.ContextYamlFile}";
        // The revision it LANDED on, never the one that was asked for: a template pinned to a
        // branch declares whatever that branch held at this moment, and the count is of a sha.
        var read = $"read {path} at {scope.ResolvedSha ?? "the revision it landed on"}";
        string? content;
        try
        {
            content = await files.Create(scope).TryReadAsync(ContainedPath.Absolute(path), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The template at {Path} could not be read", path);
            return ($"{read}: the template could not be read — {ex.Message}", Unread);
        }
        return content is null
            ? ($"{read}: the template carries no context under that name", Unread)
            : Declared(read, content);
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
