namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-8762g: where an attempt stops building. SpecSequence splices only the specs before
/// <paramref name="BeforeSpecId"/> and defers the rest to the next attempt with <paramref name="Reason"/>;
/// with <paramref name="Park"/> the stop spec is handed back instead and the run parks after the last block.
/// </summary>
public sealed record SpecSequenceStop(string BeforeSpecId, string Reason, bool Park);
