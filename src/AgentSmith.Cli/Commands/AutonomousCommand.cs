using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Host.Commands;

internal static class AutonomousCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var projectOption = new Option<string>("--project", "Project name") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");

        var cmd = new Command("autonomous", "Observe a project and write improvement tickets")
        {
            projectOption, configOption, verboseOption, dryRunOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption)!;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);

            var request = new PipelineRequest(
                project, "autonomous", Headless: true,
                Context: new Dictionary<string, object>
                {
                    [ContextKeys.SkillsPathOverride] = PipelinePresets.GetDefaultSkillsPath("autonomous"),
                });

            if (isDryRun)
            {
                DryRunPrinter.Print(request);
                return;
            }

            var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty);
            var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();

            CommandResult result;
            try
            {
                result = await useCase.ExecuteAsync(request, configPath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result = CommandResult.Fail($"Unhandled exception: {ex.Message}");
                Console.Error.WriteLine($"Fatal: {ex}");
            }

            Console.WriteLine(result.IsSuccess
                ? $"Autonomous scan complete: {result.Message}"
                : $"Autonomous scan failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
