namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// p0323: the three cache token counts, split by the semantics the provider gives them.
/// <see cref="ExclusiveRead"/> is NOT part of the provider's input total (Anthropic);
/// <see cref="InclusiveRead"/> IS (OpenAI/Azure) and must be subtracted to get the
/// billable remainder; <see cref="Creation"/> is what was written to the cache.
/// </summary>
public readonly record struct CacheCounts(long ExclusiveRead, long InclusiveRead, long Creation);
