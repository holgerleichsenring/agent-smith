namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-46ef: the programs a read-only source scope serves — written down as an
/// ALLOWANCE rather than as a list of shells to refuse.
/// <para>
/// A refusal list is a guess about what an attacker will name, and this repository already
/// carries one whose own comment admits the binary that matters sits in an argument
/// (<see cref="ShellReservedWords"/>). The set of programs the SERVER itself sends to a
/// scope is finite and knowable, so that set is the rule: git materialises the checkout,
/// find backs the file search, curl backs the HTTP tool. Nothing else the server builds
/// reaches a scope, and a step the model authors goes out as a shell with a command string
/// — which is not in here.
/// </para>
/// <para>
/// The program is only HALF the rule. A served process does exactly what its arguments tell
/// it to, so every operand must be a server constant or a value the server has shaped; that
/// half is enforced where the model's value enters, in the tools themselves.
/// </para>
/// </summary>
public static class SourceScopeProgramAllowance
{
    private static readonly HashSet<string> Programs = new(StringComparer.Ordinal)
    {
        "git", "find", "curl",
    };

    /// <summary>True when the server itself sends this program to a scope.</summary>
    public static bool Allows(string? program) =>
        !string.IsNullOrEmpty(program) && Programs.Contains(program);

    /// <summary>The set, for a refusal that says what it would have served.</summary>
    public static string Listed => string.Join(", ", Programs.Order(StringComparer.Ordinal));
}
