using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

internal static class FixCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var ticketOption = new Option<int>("--ticket", "Ticket number") { IsRequired = true };
        var projectOption = new Option<string>("--project", "Project name") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");
        var headlessOption = new Option<bool>("--headless", "Run without interactive prompts");
        var sourceOptions = new SourceOptions();

        var cmd = new Command("fix", "Fix a bug (plan, execute, test, PR)")
        {
            ticketOption, projectOption, dryRunOption, headlessOption, configOption, verboseOption
        };
        sourceOptions.AddTo(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var ticket = ctx.ParseResult.GetValueForOption(ticketOption);
            var project = ctx.ParseResult.GetValueForOption(projectOption)!;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
            var headless = ctx.ParseResult.GetValueForOption(headlessOption);

            var context = new Dictionary<string, object>();
            sourceOptions.ApplyTo(ctx, context);

            var request = new PipelineRequest(
                project, "fix-bug", new TicketId(ticket.ToString()), Headless: headless,
                Context: context.Count > 0 ? context : null);

            if (isDryRun)
            {
                DryRunPrinter.Print(request);
                return;
            }

            var provider = ServiceProviderFactory.Build(verbose, headless, string.Empty, string.Empty);
            var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();

            var result = await useCase.ExecuteAsync(request, configPath, CancellationToken.None);

            Console.WriteLine(result.IsSuccess ? $"Success: {result.Message}" : $"Failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
