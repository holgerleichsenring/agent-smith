using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-13-8762j: a contract block as read and checked — the contract, or why it was refused.</summary>
/// <param name="Contract">Null when the source declared none, or when <paramref name="Problem"/> is set.</param>
/// <param name="Problem">Every defect and collision in one message; null when the block is clean or absent.</param>
public sealed record SeriesContractRead(SeriesContract? Contract, string? Problem)
{
    public static SeriesContractRead None { get; } = new(null, null);
}
