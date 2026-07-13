using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Cli.Services.Preflight;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// p0324: `agentsmith doctor` — active preflight. Probes every dependency the
/// pipelines rely on (config secrets, each LLM agent, trackers + webhook secrets,
/// repos, the pinned skill catalog, the sandbox backend, infra) and prints one
/// named check per historical silent-failure class with an actionable fix hint.
/// Exit 0 all green, 1 on any failure.
/// </summary>
internal static class DoctorCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var jsonOption = new Option<bool>("--json", "Emit machine-readable JSON (for CI gating)");

        var cmd = new Command(
            "doctor",
            "Run active preflight checks against every configured dependency (LLM, trackers, repos, skills, sandbox, infra)")
        {
            configOption, verboseOption, jsonOption,
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var json = ctx.ParseResult.GetValueForOption(jsonOption);

            await using var services = ServiceProviderFactory.BuildDoctor(verbose, configPath);
            ctx.ExitCode = await services.GetRequiredService<DoctorExecutor>()
                .ExecuteAsync(json, Console.Out, ctx.GetCancellationToken());
        });

        return cmd;
    }
}
