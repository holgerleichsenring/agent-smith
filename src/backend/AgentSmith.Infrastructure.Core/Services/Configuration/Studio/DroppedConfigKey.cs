namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>A key an imported file sets that the store does not keep, and why.</summary>
public sealed record DroppedConfigKey(string Path, string Reason);
