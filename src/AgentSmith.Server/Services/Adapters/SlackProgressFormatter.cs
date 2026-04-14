namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Formats progress bar text for Slack messages.
/// </summary>
internal static class SlackProgressFormatter
{
    internal static string BuildProgressBar(int step, int total)
    {
        const int barLength = 10;
        var filled = (int)Math.Round((double)step / total * barLength);
        var empty = barLength - filled;
        return $"`[{new string('\u2588', filled)}{new string('\u2591', empty)}]` {step}/{total}";
    }

    internal static string FormatProgress(int step, int total, string commandName)
    {
        var bar = BuildProgressBar(step, total);
        return $"*[{step}/{total}]* {commandName}\n{bar}";
    }
}
