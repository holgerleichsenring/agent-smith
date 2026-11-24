using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: reads the scanner's JSON report out of the sandbox whole. A report is easily larger than one file
/// read carries, so it is split into line-bounded parts in the sandbox and the parts are read in order; a part that
/// cannot be read is null, and the scan fails rather than parse half a report.
/// </summary>
public sealed class PocOsvReportReader(ISandboxFileReaderFactory files)
{
    private const string Parts = "osv-report-parts";
    private const string LinesPerPart = "4000"; // not a limit: report lines per part, each part well under one read

    public async Task<string?> ReadAsync(ISandbox sandbox, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var split = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", $"mkdir -p {Parts} && split -l {LinesPerPart} -a 4 {PocOsvRun.Report} {Parts}/p-"],
            WorkingDirectory: Repository.SandboxWorkPath), null, ct);
        if (split.ExitCode != 0) return null;
        var reader = files.Create(sandbox);
        var report = new System.Text.StringBuilder();
        foreach (var part in (await reader.ListAsync(Parts, 1, ct)).Order(StringComparer.Ordinal))
        {
            if (await reader.TryReadAsync(Path(part), ct) is not { } text) return null;
            report.Append(text);
        }
        return report.ToString();
    }

    private static string Path(string listed) => listed.Contains('/', StringComparison.Ordinal) ? listed : $"{Parts}/{listed}";
}
