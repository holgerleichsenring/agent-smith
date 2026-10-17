using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// 2026-10-17-a196a: a file tool that does not write into a repository the current spec does not name. The
/// refusal is the tool's result, in the filesystem host's own failure shape ("Error: …"), the inner tool is
/// never invoked, and the refusal is recorded for the master step's result. Every other call passes through.
/// </summary>
public sealed class RepositoryScopedWriteTool(
    AIFunction inner, UnnamedRepositoryWrite check, Func<ToolResultSanityScope> scope, PipelineContext pipeline)
    : DelegatingAIFunction(inner)
{
    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var refusal = check.Refusal(new ToolResultSanityCall(Name, arguments, string.Empty), scope());
        if (refusal is null) return base.InvokeCoreAsync(arguments, cancellationToken);
        RefusedWriteLog.Of(pipeline).Record(refusal);
        return ValueTask.FromResult<object?>("Error: " + refusal);
    }
}
