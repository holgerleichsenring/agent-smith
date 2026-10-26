using AgentSmith.Domain.Models;
using AgentSmith.Application.Services.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: reads the cut reviewer's JSON out of its answer. The scan lives in
/// <see cref="JsonAnswerReader"/>; this names the wire shape it parses into.
/// </summary>
internal static class SpecCutAnswerReader
{
    public static IReadOnlyList<CutFinding>? Read(string? text) =>
        JsonAnswerReader.ReadArray<CutFindingWire, CutFinding>(text, w => w.ToFinding());
}
