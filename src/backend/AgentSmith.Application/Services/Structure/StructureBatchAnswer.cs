namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-ee83: what one batched read answered, by index into its chunk. A null content is a
/// file the container's ReadFile would refuse too (over 1 MB, not UTF-8); an index in neither map
/// was not answered and is read on its own.
/// </summary>
public sealed record StructureBatchAnswer(IReadOnlyDictionary<int, string?> Read, IReadOnlySet<int> Skipped);
