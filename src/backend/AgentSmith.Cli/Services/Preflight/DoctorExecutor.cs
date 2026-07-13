using AgentSmith.Contracts.Services;

namespace AgentSmith.Cli.Services.Preflight;

/// <summary>
/// p0324: the doctor verb's body, DI-resolved so the rendering + exit-code contract
/// is testable without System.CommandLine or live dependencies. Exit code is the
/// spec's contract: 0 all green, 1 on any failure (count capped at 1).
/// </summary>
internal sealed class DoctorExecutor(IPreflightRunner runner)
{
    public async Task<int> ExecuteAsync(bool json, TextWriter output, CancellationToken cancellationToken)
    {
        var report = await runner.RunAsync(cancellationToken);
        output.WriteLine(json
            ? DoctorJsonRenderer.Render(report)
            : DoctorTextRenderer.Render(report));
        return report.ExitCode;
    }
}
