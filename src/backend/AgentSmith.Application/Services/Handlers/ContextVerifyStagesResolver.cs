using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-31-26d4: the declared verify stages that apply to one sandbox, in context
/// order.
/// <para>
/// Reads the per-sandbox CONTEXT LIST (<see cref="ContextKeys.SandboxContexts"/>), not
/// the grouped representative at <see cref="ContextKeys.SandboxDiscoveries"/>: two
/// contexts sharing an image collapse into one sandbox, and reading the representative
/// would make which context's declaration is honoured depend on discovery order.
/// </para>
/// </summary>
public sealed class ContextVerifyStagesResolver
{
    public IReadOnlyList<ContextVerifyStages> For(PipelineContext pipeline, string sandboxKey)
    {
        var contexts = SandboxContextList.In(pipeline, sandboxKey);
        var declared = new List<ContextVerifyStages>();
        foreach (var context in contexts)
        {
            if (context.Verify is not { Count: > 0 } stages) continue;
            declared.Add(new ContextVerifyStages(
                context.ContextName, stages, context.VerifyDerivedFrom));
        }
        return declared;
    }
}
