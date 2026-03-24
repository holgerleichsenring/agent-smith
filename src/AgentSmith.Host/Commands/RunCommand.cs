using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Host.Commands;

internal static class RunCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var inputArg = new Argument<string>(
            "input", "Ticket reference and project, e.g. \"fix #123 in my-project\"");
        var dryRunOption = new Option<bool>("--dry-run", "Parse intent and show pipeline, but don't execute");
        var headlessOption = new Option<bool>("--headless", "Run without interactive prompts");
        var pipelineOption = new Option<string>("--pipeline", () => string.Empty, "Override pipeline name");
        var jobIdOption = new Option<string>("--job-id", () => string.Empty, "Redis Streams job ID");
        var redisUrlOption = new Option<string>("--redis-url", () => string.Empty, "Redis connection URL");
        var channelIdOption = new Option<string>("--channel-id", () => string.Empty, "Source channel ID");
        var platformOption = new Option<string>("--platform", () => string.Empty, "Source platform");

        var cmd = new Command("run", "Execute a pipeline (ticket, analysis, scan)")
        {
            inputArg, configOption, dryRunOption, verboseOption, headlessOption,
            jobIdOption, redisUrlOption, channelIdOption, platformOption, pipelineOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var input = ctx.ParseResult.GetValueForArgument(inputArg);
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var headless = ctx.ParseResult.GetValueForOption(headlessOption);
            var jobId = ctx.ParseResult.GetValueForOption(jobIdOption) ?? string.Empty;
            var redisUrl = ctx.ParseResult.GetValueForOption(redisUrlOption) ?? string.Empty;
            var pipelineOverride = ctx.ParseResult.GetValueForOption(pipelineOption) ?? string.Empty;

            var provider = ServiceProviderFactory.Build(verbose, headless, jobId, redisUrl);

            if (dryRun)
            {
                var intentParser = provider.GetRequiredService<IIntentParser>();
                var intent = await intentParser.ParseAsync(input, CancellationToken.None);
                var configLoader = provider.GetRequiredService<IConfigurationLoader>();
                var config = configLoader.LoadConfig(configPath);
                var projName = intent.ProjectName.Value;
                config.Projects.TryGetValue(projName, out var pc);
                var pipeline = string.IsNullOrWhiteSpace(pipelineOverride) ? pc?.Pipeline ?? "fix-bug" : pipelineOverride;

                DryRunPrinter.Print(new PipelineRequest(projName, pipeline, intent.TicketId, Headless: headless));
                return;
            }

            var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();
            var pipelineOvr = string.IsNullOrWhiteSpace(pipelineOverride) ? null : pipelineOverride;

            CommandResult result;
            try
            {
                result = await useCase.ExecuteAsync(input, configPath, headless, pipelineOvr, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result = CommandResult.Fail($"Unhandled exception: {ex.Message}");
                Console.Error.WriteLine($"Fatal: {ex}");
            }

            if (!string.IsNullOrWhiteSpace(jobId))
            {
                var reporter = provider.GetRequiredService<IProgressReporter>();
                if (result.IsSuccess)
                    await reporter.ReportDoneAsync(result.Message, result.PrUrl, CancellationToken.None);
                else
                    await reporter.ReportErrorAsync(
                        result.Message, result.FailedStep, result.TotalSteps, result.StepName, CancellationToken.None);
            }

            Console.WriteLine(result.IsSuccess ? $"Success: {result.Message}" : $"Failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
