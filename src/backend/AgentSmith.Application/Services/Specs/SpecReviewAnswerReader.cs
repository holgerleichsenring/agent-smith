using AgentSmith.Application.Services.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: reads the spec reviewer's JSON out of its answer. This reviewer is taught to cite ids written
/// <c>[P3]</c>, so a bracket in its prose is ordinary; 2026-10-26-cb25d: the scan is <see cref="JsonAnswerReader"/>'s,
/// this names the wire shape it parses into.
/// </summary>
internal static class SpecReviewAnswerReader
{
    public static IReadOnlyList<SpecFinding>? Read(string? text) =>
        JsonAnswerReader.ReadArray<SpecFindingWire, SpecFinding>(text, w => w.ToFinding());
}
