using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-17-dd05b: the parent a series has once it is on a branch — its goal alone, since the branch keeps no parent
/// document. A JSON string is a YAML scalar, so any goal reads back when the renderer reads the parent's yaml.
/// </summary>
public static class SeriesParentDraft
{
    public static SpecDraft Of(string series, string goal) =>
        new(series, goal, $"goal: {JsonSerializer.Serialize(goal)}", []);
}
