using AgentSmith.Application.Services.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: reads the account out of whatever the model wrote around it.
/// <para>
/// Tolerant about framing — prose, a fence — and strict about substance: an answer that
/// carries no readable rows is NOT an empty account, it is a failed one, and the caller
/// must be able to tell those apart.
/// </para>
/// </summary>
public static class SpecAccountReader
{
    // 2026-08-25-9749: read as the wire shape, which carries both the three-state disposition and the two-state
    // bool it replaced, then mapped to the typed row. 2026-10-26-cb25d: wherever the array sits in the prose.
    public static IReadOnlyList<AccountRow>? Read(string? text) =>
        JsonAnswerReader.ReadArray<AccountRowJson, AccountRow>(text, row => row.ToRow());
}
