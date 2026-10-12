using System.Text.Json.Nodes;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: a queued launch's request context — QueuedTicket.InitialContextJson and the
/// Context of Run.QueuedRequestJson. Two of its values are JSON texts riding as strings: the
/// approved set (<see cref="ContextKeys.ApprovedSpecSet"/>) and the resume payload
/// (<see cref="ContextKeys.ResumeCheckpoint"/>, ResumePayload: Commands plus ContextJson).
/// </summary>
public sealed class RequestContextJsonRewriter(
    SpecRenameJson json, SpecApprovalJsonRewriter approvals, CheckpointJsonRewriter checkpoints)
{
    /// <summary>An initial context dictionary; null when nothing moved.</summary>
    public string? RewriteInitialContext(string? contextJson) =>
        json.Rewrite(contextJson, node => node is JsonObject context && RewriteContext(context));

    /// <summary>A serialized PipelineRequest, whose Context is the same dictionary; null when nothing moved.</summary>
    public string? RewriteRequest(string? requestJson) =>
        json.Rewrite(requestJson, node => node is JsonObject request
            && request["Context"] is JsonObject context && RewriteContext(context));

    private bool RewriteContext(JsonObject context) =>
        json.RewriteNested(context, ContextKeys.ApprovedSpecSet, approvals.Rewrite)
        | json.RewriteNested(context, ContextKeys.ResumeCheckpoint, RewriteResumePayload);

    private string? RewriteResumePayload(string payload) =>
        json.Rewrite(payload, node => node is JsonObject resume
            && ((resume["Commands"] is JsonArray commands && checkpoints.RewriteCommands(commands))
                | json.RewriteNested(resume, "ContextJson", checkpoints.RewriteContext)));
}
