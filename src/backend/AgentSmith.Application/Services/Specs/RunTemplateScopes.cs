using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-25-54beb: the ONE owner of the template scopes a pipeline run opens. Before it, every
/// holder that wanted a template called <see cref="ProjectTemplateScopes.For"/> and cloned every
/// template again, and every look disposed what it was handed — so the diff reviewer was given
/// none. Now an address is opened once per run, the master and the diff reviewer's look BORROW
/// it, and only the pipeline executor disposes it, when the run ends.
/// <para>
/// The lock is the lazy gate: spec reviewers run in parallel, and two borrowers asking for one
/// address must get one scope. The scope materialises behind its own gate on first use, so one
/// scope is one clone however many borrowers read through it.
/// </para>
/// </summary>
public sealed class RunTemplateScopes(ProjectTemplateScopes templates) : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ISourceScopeSandbox> _open = new(TemplateScopeName.Comparer);
    private bool _disposed;

    /// <summary>Installs this owner as the run's, for the borrowers to find.</summary>
    public RunTemplateScopes Own(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Set(ContextKeys.RunTemplateScopes, this);
        return this;
    }

    /// <summary>The run's owner, or null when the pipeline was not started by the executor.</summary>
    public static RunTemplateScopes? Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<RunTemplateScopes>(ContextKeys.RunTemplateScopes, out var owner) ? owner : null;
    }

    /// <summary>
    /// The run's scopes for the selection <paramref name="contexts"/> narrows to, each address
    /// opened at most once. A borrower must not dispose them.
    /// </summary>
    public IReadOnlyDictionary<string, ISourceScopeSandbox> Borrow(
        PipelineContext pipeline, IReadOnlyCollection<string>? contexts = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var declared = TemplateDeclarations.For(pipeline, contexts);
        var lent = new Dictionary<string, ISourceScopeSandbox>(TemplateScopeName.Comparer);
        if (declared.Count == 0) return lent;
        var project = pipeline.Get<ResolvedProject>(ContextKeys.ProjectConfig);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var (name, template) in declared)
            {
                if (!_open.TryGetValue(name, out var scope))
                    _open[name] = scope = templates.Open(project, name, template);
                lent[name] = scope;
            }
        }
        return lent;
    }

    public async ValueTask DisposeAsync()
    {
        List<ISourceScopeSandbox> owned;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            owned = [.. _open.Values];
            _open.Clear();
        }
        foreach (var scope in owned) await scope.DisposeAsync();
    }
}
