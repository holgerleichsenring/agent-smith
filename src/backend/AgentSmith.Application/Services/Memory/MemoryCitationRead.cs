using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Memory;

/// <summary>2026-10-25-9324b: the memories a master's entries named, and why any entry named none.</summary>
public sealed record MemoryCitationRead(IReadOnlyList<MemoryCitation> Citations, IReadOnlyList<string> Problems);
