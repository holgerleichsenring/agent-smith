using System.Globalization;
using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-b0d7: renders what an external agent CLI reported for the calls it answered,
/// as its OWN block under <c>cost:</c>. Separate from the priced sections because it is a
/// separate claim: it is not part of <c>total_usd</c> — that transport spends no money
/// against an agent budget — and its cache-creation tokens are the CLI's own system prompt
/// and tool schemas, charged per call, not this run's context.
/// </summary>
internal static class WorkerSpendSectionWriter
{
    internal static void Append(StringBuilder sb, WorkerSpend? spend, CultureInfo ci)
    {
        if (spend is null) return;
        sb.AppendLine("  worker_cli:");
        sb.AppendLine($"    model: {spend.Models}");
        sb.AppendLine($"    calls: {spend.CallCount}");
        sb.AppendLine($"    input: {spend.InputTokens}");
        sb.AppendLine($"    output: {spend.OutputTokens}");
        sb.AppendLine($"    cache_read: {spend.CacheReadTokens}");
        sb.AppendLine($"    cache_create: {spend.CacheCreationTokens}");
        sb.AppendLine(string.Format(ci, "    reported_usd: {0:F4}", spend.ReportedCostUsd));
    }
}
