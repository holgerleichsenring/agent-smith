using System.Text;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0491: the text a <c>run_command</c> hands the model, assembled from the result the
/// sandbox agent produced rather than from the live output feed.
///
/// <para>The agent captures a run step's stdout into <see cref="StepResult.OutputContent"/>
/// (p0258) AND streams it line by line for the sandbox drawer. This text used to be built
/// from the stream, so the answer a tool call returned depended on that feed keeping up —
/// and when it fell behind, the model was handed <c>stdout:</c> with nothing under it and
/// truthfully reported that the command produced no output. A run parked asking the operator
/// to provision a checkout whose inventory sat, all 27,355 characters of it, in the result
/// body of the very command said to have returned nothing.</para>
///
/// <para>stderr has no body to switch to — the agent captures stdout only — so it is still
/// taken from the stream. A failure's cause rides out on the exit code and the result's own
/// error message, both of which arrive with the result.</para>
/// </summary>
internal static class RunCommandOutput
{
    private const string TruncationNotice = "\n... (output truncated at 1 MB)";

    /// <summary>
    /// The labeled-section text: header lines, then stdout, then stderr.
    /// <paramref name="streamedStdout"/> is the fallback for a sandbox agent image
    /// predating p0258, which leaves the body null on run steps.
    /// </summary>
    public static string Render(
        StepResult result, long elapsedMs,
        string streamedStdout, string streamedStderr, bool streamTruncated)
    {
        var (stdout, stdoutTruncated) = Bound(result.OutputContent ?? streamedStdout);
        var sb = new StringBuilder();
        AppendHeader(sb, result, elapsedMs, stdoutTruncated || streamTruncated);
        sb.Append("stdout:\n").Append(stdout.TrimEnd('\r', '\n')).Append('\n');
        sb.Append('\n');
        sb.Append("stderr:\n").Append(streamedStderr.TrimEnd('\r', '\n'));
        return sb.ToString();
    }

    private static void AppendHeader(
        StringBuilder sb, StepResult result, long elapsedMs, bool truncated)
    {
        sb.Append("exit_code: ").Append(result.ExitCode).Append('\n');
        sb.Append("elapsed_ms: ").Append(elapsedMs).Append('\n');
        sb.Append("truncated: ").Append(truncated ? "true" : "false").Append('\n');
        if (result.TimedOut) sb.Append("timed_out: true\n");
        // p0407: a command the sandbox killed carries the reason ("timed out after 900s")
        // and a failing one carries its stderr summary. Without this line the model — and
        // the operator reading the trace — saw a bare non-zero exit and no cause.
        if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            sb.Append("error: ").Append(result.ErrorMessage.Trim()).Append('\n');
        sb.Append('\n');
    }

    /// <summary>
    /// Holds the same 1 MB ceiling the streamed buffer enforced, so switching the source
    /// did not raise what a single command can spend of the model's context. The
    /// <c>truncated:</c> header reports what THIS renderer cut.
    /// </summary>
    private static (string Text, bool Truncated) Bound(string text)
    {
        const int max = SizeLimits.RunCommandMaxBufferBytes;
        if (Encoding.UTF8.GetByteCount(text) <= max) return (text, false);

        // Decode the kept prefix leniently and drop a rune the cut split in half.
        var kept = new UTF8Encoding(false, false)
            .GetString(Encoding.UTF8.GetBytes(text), 0, max)
            .TrimEnd('\uFFFD');
        return (kept + TruncationNotice, true);
    }
}
