using AgentSmith.Dispatcher.Models;

namespace AgentSmith.Dispatcher.Adapters;

/// <summary>
/// Builds Slack Block Kit blocks for structured error messages.
/// Buttons are conditionally included based on available context.
/// </summary>
internal static class SlackErrorBlockBuilder
{
    private static readonly string OwnerSlackUserId =
        Environment.GetEnvironmentVariable("OWNER_SLACK_USER_ID") ?? string.Empty;

    private static readonly string LogBaseUrl =
        Environment.GetEnvironmentVariable("LOG_BASE_URL") ?? string.Empty;

    public static (string fallbackText, object[] blocks) Build(ErrorContext ctx)
    {
        var fallback = BuildFallbackText(ctx);
        var blocks = new List<object> { BuildHeaderSection(ctx) };

        var buttons = BuildActionButtons(ctx);
        if (buttons.Length > 0)
            blocks.Add(BuildActionsBlock(ctx.JobId, buttons));

        return (fallback, blocks.ToArray());
    }

    private static string BuildFallbackText(ErrorContext ctx)
    {
        return ctx.FailedStep > 0
            ? $":x: Agent Smith failed — ticket #{ctx.TicketId} in {ctx.Project} " +
              $"at step {ctx.FailedStep}/{ctx.TotalSteps}"
            : $":x: Agent Smith failed — ticket #{ctx.TicketId} in {ctx.Project}";
    }

    private static object BuildHeaderSection(ErrorContext ctx)
    {
        var lines = new List<string>
        {
            $":x: *Agent Smith failed* — ticket *#{ctx.TicketId}* in *{ctx.Project}*"
        };

        if (ctx.FailedStep > 0)
            lines.Add($"*Step:* {ctx.FailedStep}/{ctx.TotalSteps} — {ctx.StepName}");

        lines.Add($"*Reason:* {ctx.FriendlyError}");

        return new
        {
            type = "section",
            text = new { type = "mrkdwn", text = string.Join("\n", lines) }
        };
    }

    private static object[] BuildActionButtons(ErrorContext ctx)
    {
        var buttons = new List<object>
        {
            BuildRetryButton(ctx)
        };

        var logButton = BuildLogButton(ctx);
        if (logButton is not null)
            buttons.Add(logButton);

        var contactButton = BuildContactButton();
        if (contactButton is not null)
            buttons.Add(contactButton);

        return buttons.ToArray();
    }

    private static object BuildRetryButton(ErrorContext ctx)
    {
        return new
        {
            type = "button",
            text = new { type = "plain_text", text = "Retry" },
            style = "primary",
            value = $"{ctx.TicketId}:{ctx.Project}",
            action_id = "error:retry"
        };
    }

    private static object? BuildLogButton(ErrorContext ctx)
    {
        if (string.IsNullOrEmpty(LogBaseUrl))
            return null;

        var logUrl = $"{LogBaseUrl.TrimEnd('/')}/agentsmith-{ctx.JobId}";
        return new
        {
            type = "button",
            text = new { type = "plain_text", text = "View Logs" },
            url = logUrl,
            action_id = "error:logs"
        };
    }

    private static object? BuildContactButton()
    {
        if (string.IsNullOrEmpty(OwnerSlackUserId))
            return null;

        return new
        {
            type = "button",
            text = new { type = "plain_text", text = "Contact Owner" },
            value = OwnerSlackUserId,
            action_id = "error:contact"
        };
    }

    private static object BuildActionsBlock(string jobId, object[] buttons)
    {
        return new
        {
            type = "actions",
            block_id = $"error_actions:{jobId}",
            elements = buttons
        };
    }
}
