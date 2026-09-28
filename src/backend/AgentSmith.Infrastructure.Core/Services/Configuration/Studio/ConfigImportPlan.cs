namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>What an import writes, and every key of the file it does not keep.</summary>
public sealed record ConfigImportPlan(
    IReadOnlyList<DecomposedConfigDoc> Docs, IReadOnlyList<DroppedConfigKey> Dropped);
