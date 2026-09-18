namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: the one decision entry a DERIVED phase does not state — the framework
/// writes it, unconditionally, on every phase the deriver renders.
/// <para>
/// It is boilerplate, and it points at the carried-segments markdown, which is a RUN ARTIFACT
/// (<c>SpecSetPublisher</c> hands it to <c>IRunArtifactStore</c>) and exists in no sandbox. Left
/// among a phase's premises it would be the ONE premise every derived phase carries, it would
/// keep "this phase states no premises" from ever being true on the derivation path, and a
/// checker asked whether it still holds would look for that file, fail to find it, and hand back
/// an honest phase. One producer names it and one reader excludes it, so the two cannot drift.
/// </para>
/// </summary>
public static class DerivedPhaseDecision
{
    /// <summary>How the framework's own decision entry opens.</summary>
    public const string Opening = "Derived from ticket ";

    /// <summary>The clause that says whose sentence it is.</summary>
    public const string Attribution = "by agent-smith.";

    /// <summary>The entry as the deriver writes it, naming the companion IN the spec: a phase
    /// whose constraints live in a file nobody is told to open is a phase that drops them.</summary>
    public static string For(string ticketId, string markdownFileName) =>
        $"{Opening}{ticketId} {Attribution} The verbatim naming rules, forbidden APIs and code "
        + $"templates this phase must honour are carried byte-identical in {markdownFileName} — "
        + "read it, never a summary of it.";

    /// <summary>True when the framework, not the phase, wrote this decision.</summary>
    public static bool IsMachineWritten(string? decision) =>
        decision is not null
        && decision.StartsWith(Opening, StringComparison.Ordinal)
        && decision.Contains(Attribution, StringComparison.Ordinal);
}
