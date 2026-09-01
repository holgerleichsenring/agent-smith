namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0356/2026-08-31-7097: the ONE POSIX-sh sweep a sandbox runs to report what it
/// carries — the fixed toolchains a coding master mechanizes with, plus the binaries
/// this repository's declared verify stages name. One probe, two readers: the fixed
/// half distils into the master's capability line, the declared half into a report
/// about what the image is missing.
/// </summary>
internal static class ToolchainProbeCommand
{
    // `p` reports a tool's own first version line; `q` only reports that the name
    // resolves. A derived binary is never EXECUTED — the image is being asked what it
    // has, and running an unknown tool to find out is a different question.
    // Absent tools stay silent; `true` keeps the exit code green either way.
    internal const string Fixed =
        "p() { command -v \"$1\" >/dev/null 2>&1 && echo \"$1 $($2 2>&1 | head -n 1)\"; }; "
        + "q() { command -v \"$1\" >/dev/null 2>&1 && echo \"$1\"; }; "
        + "p bash 'bash --version'; p git 'git --version'; p dotnet 'dotnet --version'; "
        + "p node 'node --version'; p npm 'npm --version'; p python3 'python3 --version'; "
        + "p java 'java --version'; p go 'go version'; p cargo 'cargo --version'; "
        + "p make 'make --version'; ";

    // What the fixed half already asks about — a derived binary that repeats one of
    // these would report the same tool twice in the capability line.
    internal static readonly string[] FixedTools =
        ["bash", "git", "dotnet", "node", "npm", "python3", "java", "go", "cargo", "make"];

    /// <summary>The sweep for one sandbox: the fixed list, then each derived binary.
    /// Derived names are bare by construction (<see cref="BareCommandBinary"/>), so
    /// nothing here can carry shell syntax into the command.</summary>
    public static string For(IReadOnlyList<DeclaredStageBinary> derived)
    {
        ArgumentNullException.ThrowIfNull(derived);
        var sweep = string.Concat(derived
            .Where(d => !FixedTools.Contains(d.Binary, StringComparer.Ordinal))
            .Select(d => $"q {d.Binary}; "));
        return Fixed + sweep + "true";
    }
}
