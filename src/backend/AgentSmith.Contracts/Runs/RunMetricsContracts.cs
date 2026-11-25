using AgentSmith.Contracts.Events;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0369: the incremental fold behind <see cref="RunMetricsView"/>. Folds
/// <see cref="LlmCallFinishedEvent"/> + <see cref="SandboxResultEvent"/> as they
/// arrive (the applier persists it as JSON on the run row, so a mid-run run
/// already shows its metrics). Mutable with public accessors so the whole fold —
/// including the per-path last-content-hash state that makes redundancy
/// content-aware — round-trips through the stored JSON.
/// </summary>
public sealed class RunMetrics
{
    // p0369: an LLM call is "cold" when its prompt is large enough that skipping
    // the cache means a real re-process, not a trivially small prompt.
    private const long ColdInputThreshold = 1024;

    /// <summary>2026-11-25-9992k: the tool a run asks the operator with.</summary>
    public const string QuestionTool = "ask_human";

    private static readonly HashSet<string> FileVerbs = new(StringComparer.Ordinal)
        { "ReadFile", "WriteFile", "Grep", "ListFiles", "DirectoryTree" };

    public long LlmActiveMs { get; set; }
    public long ThrottleWaitMs { get; set; }
    public long SandboxCommandMs { get; set; }

    public int Reads { get; set; }
    public int Writes { get; set; }
    public int RunCommands { get; set; }
    public int Greps { get; set; }

    public Dictionary<string, int> ReadsByPath { get; set; } = new();
    public Dictionary<string, int> WritesByPath { get; set; } = new();

    // Redundancy fold-state: the last content hash seen per path.
    public Dictionary<string, string> LastReadHashByPath { get; set; } = new();
    public Dictionary<string, string> LastWriteHashByPath { get; set; } = new();
    public int RedundantReads { get; set; }
    public int RedundantWrites { get; set; }

    public int ColdLlmCalls { get; set; }
    public int CacheDiscards { get; set; }
    public long UncachedReprocessedTokens { get; set; }
    // Discard-transition state: was the previous LLM call served warm from cache?
    public bool PrevCallWarm { get; set; }

    public int BuildTestInvocations { get; set; }
    public int BuildTestFailures { get; set; }

    public int QuestionsAsked { get; set; }

    public static RunMetrics From(IEnumerable<RunEvent> events)
    {
        var metrics = new RunMetrics();
        foreach (var ev in events) metrics.Apply(ev);
        return metrics;
    }

    public RunMetrics Apply(RunEvent ev)
    {
        switch (ev)
        {
            case LlmCallFinishedEvent e: ApplyLlm(e); break;
            case SandboxResultEvent e: ApplySandbox(e); break;
            case ToolCallEvent { Tool: QuestionTool }: QuestionsAsked++; break; // 2026-11-25-9992k
        }
        return this;
    }

    private void ApplyLlm(LlmCallFinishedEvent e)
    {
        LlmActiveMs += e.DurationMs;
        ThrottleWaitMs += e.ThrottleWaitMs;
        var input = e.TokensIn + e.CachedTokensIn + e.CacheCreationTokensIn;
        var warm = e.CachedTokensIn > 0;
        var cold = input > ColdInputThreshold && e.CachedTokensIn == 0;
        if (cold)
        {
            ColdLlmCalls++;
            UncachedReprocessedTokens += e.TokensIn;
            if (PrevCallWarm) CacheDiscards++;
        }
        PrevCallWarm = warm;
    }

    private void ApplySandbox(SandboxResultEvent e)
    {
        SandboxCommandMs += e.DurationMs;
        switch (e.Command)
        {
            case "ReadFile": Reads++; TallyRead(e); break;
            case "WriteFile": Writes++; TallyWrite(e); break;
            case "Grep": Greps++; break;
            default:
                if (!FileVerbs.Contains(e.Command)) { RunCommands++; TallyBuildTest(e); }
                break;
        }
    }

    private void TallyRead(SandboxResultEvent e) =>
        TallyFile(e, ReadsByPath, LastReadHashByPath, () => RedundantReads++);

    private void TallyWrite(SandboxResultEvent e) =>
        TallyFile(e, WritesByPath, LastWriteHashByPath, () => RedundantWrites++);

    private static void TallyFile(
        SandboxResultEvent e, Dictionary<string, int> counts,
        Dictionary<string, string> lastHash, Action onRedundant)
    {
        var path = e.Summary;
        if (string.IsNullOrEmpty(path)) return;
        counts[path] = counts.TryGetValue(path, out var c) ? c + 1 : 1;
        if (e.ContentHash is null) return;
        if (lastHash.TryGetValue(path, out var last) && last == e.ContentHash) onRedundant();
        lastHash[path] = e.ContentHash;
    }

    private void TallyBuildTest(SandboxResultEvent e)
    {
        var s = e.Summary;
        if (string.IsNullOrEmpty(s)) return;
        if (!s.Contains("dotnet build", StringComparison.OrdinalIgnoreCase)
            && !s.Contains("dotnet test", StringComparison.OrdinalIgnoreCase)) return;
        BuildTestInvocations++;
        if (e.ExitCode != 0) BuildTestFailures++;
    }

    public RunMetricsView ToView(int topN = 5) => new(
        LlmActiveMs, ThrottleWaitMs, SandboxCommandMs,
        Reads, Writes, RunCommands, Greps,
        Top(ReadsByPath, topN), Top(WritesByPath, topN),
        RedundantReads, RedundantWrites,
        ColdLlmCalls, CacheDiscards, UncachedReprocessedTokens,
        BuildTestInvocations, BuildTestFailures, QuestionsAsked);

    private static IReadOnlyList<FileAccessView> Top(Dictionary<string, int> counts, int n) =>
        counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(n).Select(kv => new FileAccessView(kv.Key, kv.Value)).ToList();
}
