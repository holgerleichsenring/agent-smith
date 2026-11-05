using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using AgentSmith.Cli.Services.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// p0326: `agentsmith demo` — one command proving the whole loop (inline
/// ticket → code pipeline → local commit) on a bundled sample project.
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
            "Prove the whole loop on a bundled sample project: inline ticket → code → local commit. Needs only an LLM key.")
        {
            agentOption, workspaceOption, configOption, verboseOption,
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var workspace = ctx.ParseResult.GetValueForOption(workspaceOption) ?? DemoInvocation.DefaultWorkspaceDir();
            var configPath = ResolveConfig(ctx.ParseResult, configOption, workspace);
            if (configPath is null)
            {
                Console.Error.WriteLine(DemoConfigFile.NoKeyLine());
                ctx.ExitCode = 1;
                return;
            }
            var invocation = new DemoInvocation(configPath, ctx.ParseResult.GetValueForOption(agentOption), workspace);

            await using var services = ServiceProviderFactory.BuildDemo(verbose, configPath);
            ctx.ExitCode = await services.GetRequiredService<DemoExecutor>()
                .ExecuteAsync(invocation, Console.Out, ctx.GetCancellationToken());
        });

        return cmd;
    }

    // 2026-11-02-2b26a: only a DISCOVERED config that found nothing is replaced by a generated one; an explicit
    // --config to a missing file must fail, not be swapped for a demo config.
    private static string? ResolveConfig(ParseResult parse, Option<string> configOption, string workspace)
    {
        var configPath = parse.GetValueForOption(configOption)!;
        var given = parse.FindResultFor(configOption) is { IsImplicit: false };
        if (given || File.Exists(configPath)) return configPath;
        var generated = DemoConfigFile.TryWrite(Environment.GetEnvironmentVariable, workspace);
        if (generated is not null) Console.WriteLine($"No config found — the demo runs on {generated}");
        return generated;
    }
}
