using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Dialogue;

/// <summary>
/// p0461: the address of the place where a parked run is actually answered.
/// <para>
/// A ticket comment that asks a question used to name only itself ("reply to this
/// comment"), so a reader who wanted the affordance that resumes the run — the dashboard's
/// answer box — had to know it existed and go looking. The link closes that, and it comes
/// from configuration because only the deployment knows its own address.
/// </para>
/// <para>
/// An unconfigured base URL yields NO link. A printed guess would be a broken address in
/// someone else's work item, which is worse than the silence it replaced.
/// </para>
/// </summary>
public sealed class RunAnswerLink(AgentSmithConfig config)
{
    public string? For(PipelineContext? pipeline) =>
        pipeline is not null
        && pipeline.TryGet<string>(ContextKeys.RunId, out var runId)
        && !string.IsNullOrWhiteSpace(runId)
            ? For(runId!)
            : null;

    public string? For(string runId) => For(runId, config.Dialogue.DashboardUrl);

    /// <summary>The link under a base URL the caller read itself — a caller that must see a
    /// live-reloaded <c>dialogue.dashboard_url</c> cannot take the one this instance was built with.</summary>
    public string? For(string runId, string? dashboardUrl) =>
        string.IsNullOrWhiteSpace(dashboardUrl) || string.IsNullOrWhiteSpace(runId)
            ? null
            : $"{dashboardUrl.TrimEnd('/')}/jobs/{Uri.EscapeDataString(runId)}";
}
