using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: reads the cut reviewer's JSON out of its answer. The scan lives in
/// <see cref="JsonAnswerArrayReader"/>; this names the wire shape it parses into.
/// </summary>
internal static class SpecCutAnswerReader
{
    public static IReadOnlyList<CutFinding>? Read(string? text) =>
        JsonAnswerArrayReader.Read<CutFindingWire, CutFinding>(text, w => w.ToFinding());
}
