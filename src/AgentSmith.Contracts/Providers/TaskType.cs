namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Categorizes agent tasks for model routing.
/// Different task types can be assigned to different models (e.g. Haiku for Scout, Sonnet for Primary).
/// </summary>
public enum TaskType
{
    Scout,
    Primary,
    Planning,
    Reasoning,
    Summarization
}
