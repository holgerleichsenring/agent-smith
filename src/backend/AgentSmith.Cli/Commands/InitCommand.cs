using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

internal static class InitCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var projectOption = new Option<string>("--project", "Project name") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");
        var refreshOption = new Option<bool>(
            "--refresh-principles",
            "Recompose an existing principles.md from the catalog (core, language delta, framework "
            + "overlays); its Project Specifics section is kept");
        var sourceOptions = new SourceOptions();

        var cmd = new Command("init", "Bootstrap a new project (.agentsmith/ directory)")
        {
            projectOption, dryRunOption, refreshOption, configOption, verboseOption
        };
        sourceOptions.AddTo(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption)!;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
            var refresh = ctx.ParseResult.GetValueForOption(refreshOption);

            var context = new Dictionary<string, object>();
            sourceOptions.ApplyTo(ctx, context);
            if (refresh) context[ContextKeys.RefreshPrinciples] = true;

            var request = new PipelineRequest(project, "init-project", IsInit: true, Headless: true,
                Context: context.Count > 0 ? context : null);

            if (isDryRun)
            {
                DryRunPrinter.Print(request, refresh
                    ? new Dictionary<string, string> { ["Refresh principles"] = "yes" }
                    : null);
                return;
            }

            var provider = ServiceProviderFactory.Build(configPath, verbose, headless: true);
            var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();

            var result = await useCase.ExecuteAsync(request, configPath, CancellationToken.None);

            Console.WriteLine(result.IsSuccess ? $"Success: {result.Message}" : $"Failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
