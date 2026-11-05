using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>The thread the server seeded for this turn's master, or null: none was seeded, or it is the other
/// master's.</summary>
public static class DialogThreadSeed
{
    public static DialogThread? For(PipelineContext pipeline, bool stewarded)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<DialogThread>(ContextKeys.SpecDialogThread, out var thread)
            && thread is not null && thread.Stewarded == stewarded
                ? thread
                : null;
    }
}
