namespace AgentSmith.Contracts.Services;

/// <summary>
/// Marker for tool hosts whose result bodies contain credentials or other
/// secrets that must not persist beyond one round-trip with the LLM
/// provider (p0191). Adding this marker REQUIRES adding the tool name
/// to <c>SensitiveToolHistoryScrubChatClient.SensitiveToolNames</c> so the
/// history-scrub layer recognises it. The two sites are co-edited or the
/// guarantee leaks.
/// </summary>
public interface ISensitiveToolHost
{
}
