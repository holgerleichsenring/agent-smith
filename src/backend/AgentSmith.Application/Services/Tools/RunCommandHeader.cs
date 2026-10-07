using System.Text;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The header lines above a command's labeled sections. <c>exit_code:</c> stays the FIRST line
/// — the phase command log reads the status from there — and <c>truncated:</c> is the one flag
/// the tool descriptions promise.
/// </summary>
internal static class RunCommandHeader
{
    /// <param name="sectionLines">Extra lines stating each section's total; none for the
    /// program render, whose text is unchanged.</param>
    /// <param name="errorMaxChars">Bound on the error line; null leaves it whole.</param>
    public static void Append(
        StringBuilder sb, StepResult result, long elapsedMs, bool truncated,
        IReadOnlyList<string> sectionLines, int? errorMaxChars)
    {
        sb.Append("exit_code: ").Append(result.ExitCode).Append('\n');
        sb.Append("elapsed_ms: ").Append(elapsedMs).Append('\n');
        sb.Append("truncated: ").Append(truncated ? "true" : "false").Append('\n');
        foreach (var line in sectionLines) sb.Append(line).Append('\n');
        if (result.TimedOut) sb.Append("timed_out: true\n");
        // p0407: a command the sandbox killed carries the reason ("timed out after 900s")
        // and a failing one carries its stderr summary. Without this line the model — and
        // the operator reading the trace — saw a bare non-zero exit and no cause.
        if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            sb.Append("error: ").Append(Error(result.ErrorMessage.Trim(), errorMaxChars)).Append('\n');
        sb.Append('\n');
    }

    // 2026-10-07-6b9db: the agent's OutputTail keeps a last line whole, so one minified line
    // can make the error as large as the output it summarises.
    private static string Error(string message, int? maxChars) =>
        maxChars is { } max ? (string)ToolResultBound.Apply(message, max)! : message;
}
