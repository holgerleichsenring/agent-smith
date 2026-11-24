using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: a real in-process sandbox whose scanner binary is faked — the run of /osv-scanner is
/// recorded, writes <see cref="Report"/> where the scan asked for it and exits <see cref="ExitCode"/>; every other step
/// (the lockfile writes, the split, the reads) runs for real.</summary>
internal sealed class OsvSandbox : ISandbox
{
    private readonly InProcessSandbox _inner;

    public OsvSandbox()
    {
        Directory.CreateDirectory(Dir);
        _inner = new InProcessSandbox("osv", Dir, ownsWorkDir: true, InProcessStepEnvironment.Inherit, NullLogger.Instance);
    }

    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "as-933dc-osv-" + Guid.NewGuid().ToString("N"));

    public string JobId => _inner.JobId;

    public int ExitCode { get; set; } = 1;

    public string Report { get; set; } = """{"results": []}""";

    public List<Step> Steps { get; } = [];

    public Step? Scanner => Steps.FirstOrDefault(s => s.Command == "/osv-scanner");

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        Steps.Add(step);
        if (step.Command != "/osv-scanner") return _inner.RunStepAsync(step, progress, ct);
        if (ExitCode is 0 or 1) File.WriteAllText(Path.Combine(Dir, "osv-report.json"), Report);
        return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, ExitCode, false, 0.1,
            ExitCode is 0 or 1 ? null : "failed to download database", null));
    }

    public string Written(string path) => File.ReadAllText(Path.Combine(Dir, path));

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
