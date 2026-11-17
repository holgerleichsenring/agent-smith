namespace AgentSmith.Server.Security;

/// <summary>2026-10-15-4a4db: whether a dialog message is taken at all, and whether its turn may start runs.</summary>
internal sealed record DialogAdmission(bool Admitted, bool MayStartRuns, string? Refusal = null)
{
    internal static DialogAdmission Refused { get; } = new(false, false);

    /// <summary>2026-11-17-f3a8a: the installation's own conversation, refused with its reason rather than hidden.</summary>
    internal static DialogAdmission Installation { get; } = new(false, false,
        "The 'Agent Smith' conversation is the admin assistant, which needs the admin.assistant permission "
        + "(the admin role holds it).");
}
