using AgentSmith.Cli.Services.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// p0326: the demo verb's body — the p0324 preflight subset (config-schema,
/// llm-reachable, sandbox-spawn, infra) gates the run so a broken environment
/// fails with fix hints BEFORE the pipeline spends tokens. DI-resolved so the
/// gate + exit-code contract is testable without System.CommandLine or a live LLM.
/// </summary>
internal sealed class DemoExecutor(IPreflightRunner preflight, IDemoRunner runner)
{
    public async Task<int> ExecuteAsync(
        DemoInvocation invocation, TextWriter output, CancellationToken cancellationToken)
    {
        output.WriteLine("Preflight (config schema, LLM, sandbox, infra) ...");
        var report = await preflight.RunAsync(cancellationToken);
        output.WriteLine(DoctorTextRenderer.Render(report));
        if (report.ExitCode != 0)
        {
            output.WriteLine(
                "Preflight failed — fix the hints above and re-run `agentsmith demo`. "
                + "The pipeline was not started; no tokens were spent on it.");
            return 1;
        }
        return await runner.RunAsync(invocation, output, cancellationToken);
    }
}
