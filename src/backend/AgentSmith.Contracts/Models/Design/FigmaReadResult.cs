using System.Text.Json;

namespace AgentSmith.Contracts.Models.Design;

/// <summary>
/// 2026-10-01-7f7ab: one Figma read — the parsed JSON body on success, the failure otherwise.
/// The body is a detached element, valid after the response that carried it is disposed.
/// </summary>
public sealed record FigmaReadResult(JsonElement? Body, FigmaReadFailure? Failure)
{
    public static FigmaReadResult Of(JsonElement body) => new(body, null);

    public static FigmaReadResult Failed(FigmaReadFailure failure) => new(null, failure);
}
