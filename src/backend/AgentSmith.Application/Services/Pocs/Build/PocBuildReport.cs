using System.Globalization;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: reads a build run's scan report and gate status back and decides the image verdict with
/// <see cref="PocImageRules"/>. A report over build.report_kb is refused naming the key; a report that does not parse,
/// or a catalog whose secret confidences cannot be read, is a problem, never a pass.
/// </summary>
public sealed class PocBuildReport(
    TrivyReportParser parser, PocImageRules rules, PocTrivySecretRules secretRules, PocTrivyIgnore ignore)
{
    private const long BytesPerKb = 1024; // not a limit: the unit of build.report_kb

    public async Task<PocReportRead> ReadAsync(IPipelineRunner runner, string runId, PocBuildJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(job);
        var report = await runner.ReadArtifactTextAsync(runId, PocBuildArtifacts.Report, job.Build.ReportKb * BytesPerKb, ct);
        if (report is null) return new PocReportRead(null, null, null);
        if (report.IsTooLarge)
            return new PocReportRead(null, null, $"the scan report is {Kb(report.SizeBytes)} KB, over {Bound(job)}; raise it");
        var gate = await runner.ReadArtifactTextAsync(runId, PocBuildArtifacts.Gate, PocBuildArtifacts.SmallFileBytes, ct);
        var (entries, unreadable) = parser.Parse(report.Text!);
        if (unreadable is not null) return new PocReportRead(null, gate?.Text?.Trim(), unreadable);
        var (confidenceOf, failure) = secretRules.Read();
        if (failure is not null) return new PocReportRead(null, gate?.Text?.Trim(), failure);
        var verdict = rules.Decide(entries, ignore.Of(job.Subject), confidenceOf, job.Scan);
        return new PocReportRead(verdict, gate?.Text?.Trim(), null);
    }

    private static string Kb(long bytes) => Math.Ceiling(bytes / (double)BytesPerKb).ToString(CultureInfo.InvariantCulture);

    private static string Bound(PocBuildJob job) =>
        $"{Contracts.Models.Configuration.PocBuildConfig.Key}.report_kb of target '{job.Target.Name}' ({job.Build.ReportKb} KB)";
}
