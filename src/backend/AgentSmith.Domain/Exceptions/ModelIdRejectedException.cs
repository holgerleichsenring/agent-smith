namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// 2026-10-12-6bfae: the provider answered a model call with "not found" — the configured
/// model id (or deployment, or endpoint path) does not exist there. A configuration fault:
/// every later call with the same role fails the same way, so nothing falls back on it.
/// Derives <see cref="AgentSmithException"/> directly so no <see cref="ConfigurationException"/>
/// handler, which guards config loading, takes a model-call failure.
/// </summary>
public sealed class ModelIdRejectedException : AgentSmithException
{
    public string Role { get; }
    public string ModelId { get; }

    public ModelIdRejectedException(string role, string modelId, string message, Exception innerException)
        : base(message, innerException)
    {
        Role = role;
        ModelId = modelId;
    }
}
