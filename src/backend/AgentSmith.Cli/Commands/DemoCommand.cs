using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Cli.Services.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// p0326: `agentsmith demo` — one command proving the whole loop (inline
/// ticket → fix-bug pipeline → local commit) on a bundled sample project.
/// The only credential it needs is an LLM key: no tracker, no repo remote,
/// no Docker, no Redis. Preflight (the p0324 subset) gates the run so a
/// broken environment fails with fix hints before any pipeline tokens.
/// </summary>
internal static class DemoCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var agentOption = new Option<string?>(
            "--agent", "Agent name from the agents: catalog (default: the first configured agent)");
        var workspaceOption = new Option<string?>(
            "--workspace", "Directory to materialize the demo workspace into (default: a fresh temp dir)");

        var cmd = new Command(
            "demo",
            "Prove the whole loop on a bundled sample project: inline ticket → fix-bug → local commit. Needs only an LLM key.")
        {
            agentOption, workspaceOption, configOption, verboseOption,
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var invocation = new DemoInvocation(
                configPath,
                ctx.ParseResult.GetValueForOption(agentOption),
                ctx.ParseResult.GetValueForOption(workspaceOption));

            await using var services = ServiceProviderFactory.BuildDemo(verbose, configPath);
            ctx.ExitCode = await services.GetRequiredService<DemoExecutor>()
                .ExecuteAsync(invocation, Console.Out, ctx.GetCancellationToken());
        });

        return cmd;
    }
}
