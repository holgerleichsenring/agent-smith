namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// Configuration for SlackAdapter, bound from environment variables or appsettings.
/// </summary>
public sealed class SlackAdapterOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
}
