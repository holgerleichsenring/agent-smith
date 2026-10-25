using System.Text;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-25-54beb: the templates the diff reviewer reads — borrowed from the run's
/// <see cref="RunTemplateScopes"/>, so it reads the checkout the master was given, and the same
/// advice the master was shown (<see cref="MasterTemplateAdvice"/>). A template that cannot be
/// opened is left out and named, never a failed review: a review never fails a spec.
/// </summary>
public sealed class DiffReviewTemplates(MasterTemplateAdvice advice, ILogger<DiffReviewTemplates> logger)
{
    /// <summary>The opened scopes (null when the run lends none) and the advice text.</summary>
    public async Task<(IReadOnlyDictionary<string, ISourceScopeSandbox>? Scopes, string Advice)> BorrowAsync(
        PipelineContext pipeline, IReadOnlyCollection<string>? contexts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (RunTemplateScopes.Of(pipeline)?.Borrow(pipeline, contexts) is not { Count: > 0 } lent)
            return (null, string.Empty);
        var declared = TemplateDeclarations.For(pipeline, contexts);
        var opened = new Dictionary<string, ISourceScopeSandbox>(TemplateScopeName.Comparer);
        var text = new StringBuilder();
        foreach (var (name, scope) in lent)
        {
            if (!declared.TryGetValue(name, out var template)) continue;
            if (!await OpenedAsync(name, scope, text, cancellationToken)) continue;
            opened[name] = scope;
            text.Append(await advice.ReadAsync(name, template, scope, cancellationToken));
        }
        return (opened.Count == 0 ? null : opened, text.ToString());
    }

    private async Task<bool> OpenedAsync(
        string name, ISourceScopeSandbox scope, StringBuilder text, CancellationToken cancellationToken)
    {
        try
        {
            await scope.MaterializeAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Template '{Name}' could not be opened for the diff review", name);
            text.Append($"\n\n### Template `{name}` could not be opened\n{ex.Message}\n");
            return false;
        }
    }
}
