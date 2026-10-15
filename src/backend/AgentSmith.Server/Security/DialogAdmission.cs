namespace AgentSmith.Server.Security;

/// <summary>2026-10-15-4a4db: whether a dialog message is taken at all, and whether its turn may start runs.</summary>
internal sealed record DialogAdmission(bool Admitted, bool MayStartRuns)
{
    internal static DialogAdmission Refused { get; } = new(false, false);
}
