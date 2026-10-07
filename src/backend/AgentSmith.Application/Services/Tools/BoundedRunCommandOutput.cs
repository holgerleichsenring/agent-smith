using System.Text;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-07-6b9db: the text a model-authored <c>run_command</c> hands the model, each section
/// within its own budget. A TypeScript monorepo's <c>npm test</c> wrote about a megabyte of jest
/// output to stderr, which reached the model whole and grew the next call by ~470,000 tokens.
/// <para>
/// stdout is the content and stderr an excerpt; each keeps its head and its tail (a build's
/// verdict is at the end, p0419) around a marker naming the true total, and the header states
/// both totals. The labels and the leading <c>exit_code:</c> are what the toolchain probe and
/// the phase command log parse, so they keep their shape.
/// </para>
/// </summary>
internal static class BoundedRunCommandOutput
{
    public static string Render(StepResult result, long elapsedMs, StreamedStepOutput streamed)
    {
        const int stdoutMax = SizeLimits.RunCommandStdoutMaxChars;
        const int stderrMax = SizeLimits.RunCommandStderrMaxChars;
        var stdout = Stdout(result.OutputContent, streamed.Stdout);
        var stderr = OutputSection.Of(streamed.Stderr);
        var sb = new StringBuilder();
        RunCommandHeader.Append(sb, result, elapsedMs, stdout.IsCut(stdoutMax) || stderr.IsCut(stderrMax),
            [stdout.CountLine("stdout_chars"), stderr.CountLine("stderr_chars")],
            SizeLimits.RunCommandErrorLineMaxChars);
        sb.Append("stdout:\n").Append(stdout.Bound(stdoutMax)).Append('\n');
        sb.Append('\n');
        sb.Append("stderr:\n").Append(stderr.Bound(stderrMax));
        return sb.ToString();
    }

    /// <summary>
    /// stdout comes from the result body (p0491: the stream can lag or be empty while the body is
    /// full). The agent stops the body at its capture cap with no flag, so a body that long may
    /// be cut: a stream that counted MORE supplies the tail and the total; otherwise only the
    /// body's head is known, and the section says its tail was not captured.
    /// </summary>
    private static OutputSection Stdout(string? body, StreamCapture stream)
    {
        // A sandbox agent image predating p0258 leaves the body null on run steps.
        if (body is null) return OutputSection.Of(stream);
        if (body.Length < SizeLimits.RunStepCapturedStdoutMaxChars) return new(body, string.Empty, body.Length);
        if (stream.Total > body.Length) return new(body, stream.After(body.Length), stream.Total);
        return new(body, string.Empty, body.Length, TailNotCaptured: true);
    }
}
