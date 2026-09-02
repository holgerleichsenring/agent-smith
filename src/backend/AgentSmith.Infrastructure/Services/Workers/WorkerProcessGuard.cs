using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0426: decides whether the worker PROCESS produced something worth reading at all — a
/// timeout or a non-zero exit is a failed call, whatever it wrote.
/// <para>
/// Extracted from <see cref="ExternalWorkerChatClient"/>, which composes the call and
/// hands the answer on. Judging the process is a third job, and the class had no room left
/// for it.
/// </para>
/// </summary>
internal static class WorkerProcessGuard
{
    private const int FailureTail = 500;

    // p0419: the prompt SIZE travels with the failure. "Prompt is too long" without a
    // number leaves the next person guessing which message grew — and with compaction
    // reporting 10 -> 10 messages, the count was never the thing that mattered.
    public static void RequireUsable(
        WorkerRequest request, WorkerProcessResult result, int promptChars)
    {
        if (result.TimedOut)
            throw new ExternalWorkerCallException(
                request,
                $"the worker did not answer within the per-call timeout "
                + $"(prompt was {promptChars:N0} chars)",
                result.Duration);
        if (result.ExitCode != 0)
            throw new ExternalWorkerCallException(
                request,
                $"the worker CLI exited with {result.ExitCode} on a {promptChars:N0}-char "
                + $"prompt: {Tail(result)}",
                result.Duration);
        // 2026-09-01-b0d7: the CLI states its own failure — out of turns, an error during
        // execution — on the envelope and still exits 0. The exit code alone calls that a
        // good call, and the loop then reasons about whatever half-answer came with it.
        if (result.Envelope?.FailureReason is { } reported)
            throw new ExternalWorkerCallException(
                request, $"{reported} (prompt was {promptChars:N0} chars)", result.Duration);
    }

    /// <summary>
    /// p0419: BOTH streams. An agent CLI states its refusal — a usage limit, a prompt over
    /// the input ceiling — on stdout and exits non-zero with stderr empty, so reporting
    /// stderr alone produced "exited with 1: (no stderr)" and cost run c96d its
    /// implementation phase with no recorded reason. The same rule the sandbox learned: a
    /// failing process is quoted from whatever it actually said.
    /// </summary>
    private static string Tail(WorkerProcessResult result)
    {
        var text = string.Join(
            "\n",
            new[] { result.StandardError, result.StandardOutput }
                .Select(part => part?.Trim())
                .Where(part => !string.IsNullOrEmpty(part)));
        if (text.Length == 0) return "(the worker said nothing on either stream)";
        return text.Length <= FailureTail ? text : "…" + text[^FailureTail..];
    }
}
