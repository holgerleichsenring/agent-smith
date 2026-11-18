namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: the Work it out conversation a series reports into.</summary>
public interface ISeriesConversation
{
    /// <summary>Keeps an agent message in the conversation and pushes it to the page; silent when it is closed.</summary>
    Task SayAsync(string conversation, string text, CancellationToken cancellationToken);
}
