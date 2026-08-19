namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0451: whether a declared stage command can actually fail — because one that cannot is
/// not a verification.
/// <para>
/// Live run 587c: the analyzer wrote <c>echo Build command placeholder</c> as a repository's
/// declared build stage. Verification ran it, it exited 0, and the mechanical gate reported
/// that repository "[build+test] green" without anything having compiled. The delivery
/// account caught it — "the build command was a placeholder and does not demonstrate a
/// migrated solution build" — and was right while the gate was wrong.
/// </para>
/// <para>
/// A declared stage that cannot fail is worth what no declared stage is worth: the resolver
/// falls through to discovery, and when that finds nothing the run reports a resolution
/// failure instead of a build it never ran. Failing loudly is the point — p0420 already
/// refuses to account for a phase over a tree that does not compile, and this stops the
/// gate from asserting the opposite.
/// </para>
/// </summary>
public static class VerificationCommand
{
    // Shell no-ops: they take any argument and always succeed. `#` is a comment line,
    // which the shell also accepts and ignores.
    private static readonly string[] CannotFail = ["echo", "true", ":", "printf", "#"];

    /// <summary>
    /// True when the command could report a failure. Read of the command ITSELF, never of
    /// its arguments — `dotnet build -p:Message=echo` is a real build.
    /// </summary>
    public static bool CanFail(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        var head = command.TrimStart();
        var end = head.IndexOf(' ');
        var verb = end < 0 ? head : head[..end];
        return !CannotFail.Contains(verb.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
