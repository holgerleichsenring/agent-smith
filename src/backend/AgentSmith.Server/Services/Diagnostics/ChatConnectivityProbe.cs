using System.Diagnostics;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Auth probes for the chat adapters. Slack authenticates with a bot token
/// (auth.test); Teams authenticates by acquiring a Bot Framework token from its
/// app credentials. A platform counts as configured only when its credentials are set.
/// </summary>
internal sealed class ChatConnectivityProbe(
    SlackApiClient slackApiClient,
    SlackAdapterOptions slackOptions,
    BotFrameworkTokenProvider teamsTokenProvider,
    TeamsAdapterOptions teamsOptions,
    ILogger<ChatConnectivityProbe> logger) : IChatConnectivityProbe
{
    public bool IsSlackConfigured => !string.IsNullOrEmpty(slackOptions.BotToken);

    public bool IsTeamsConfigured =>
        !string.IsNullOrEmpty(teamsOptions.AppId) && !string.IsNullOrEmpty(teamsOptions.AppPassword);

    public async Task<ConnectionProbeResult> ProbeSlackAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var node = await slackApiClient.PostAsync("auth.test", new { }, cancellationToken);
            var ok = node?["ok"]?.GetValue<bool>() ?? false;
            return ok
                ? ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds)
                : ConnectionProbeResult.Unreachable(
                    stopwatch.ElapsedMilliseconds, node?["error"]?.GetValue<string>() ?? "auth.test failed");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Slack probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<ConnectionProbeResult> ProbeTeamsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await teamsTokenProvider.GetTokenAsync(cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Teams probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }
}
