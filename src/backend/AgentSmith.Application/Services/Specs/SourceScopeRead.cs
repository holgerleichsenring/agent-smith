namespace AgentSmith.Application.Services.Specs;

/// <summary>What a read of a source scope came to: content, an absence, or a read that could
/// not run and proves neither.</summary>
internal sealed record SourceScopeRead(string? Content, bool Ran, string? Error = null);
