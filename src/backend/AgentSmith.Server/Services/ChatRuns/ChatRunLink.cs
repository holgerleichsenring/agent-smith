using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.ChatRuns;

/// <summary>
/// The dashboard link a chat reply names for a run, under the dashboard URL configured NOW:
/// the configuration is read on every call, so a <c>dialogue.dashboard_url</c> changed by live
/// reload reaches the next reply without a restart. No configured URL, no link.
/// </summary>
public sealed class ChatRunLink(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    RunAnswerLink answerLink)
{
    public string? For(string runId) =>
        answerLink.For(runId, configLoader.LoadConfig(serverContext.ConfigPath).Dialogue.DashboardUrl);
}
