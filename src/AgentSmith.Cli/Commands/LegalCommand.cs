using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Host.Commands;

internal static class LegalCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var sourceOption = new Option<string>("--source", "Path to document (PDF, DOCX)") { IsRequired = true };
        var projectOption = new Option<string>("--project", () => "legal", "Project name from config");
        var outputOption = new Option<string>("--output", () => "console", "Output format: console | markdown | file");
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");

        var cmd = new Command("legal", "Analyze a legal document")
        {
            sourceOption, projectOption, outputOption, dryRunOption, configOption, verboseOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var source = ctx.ParseResult.GetValueForOption(sourceOption)!;
            var project = ctx.ParseResult.GetValueForOption(projectOption)!;
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "console";
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);

            var request = new PipelineRequest(project, "legal-analysis", Headless: true,
                Context: new Dictionary<string, object>
                {
                    [ContextKeys.SourceFilePath] = Path.GetFullPath(source),
                    [ContextKeys.OutputFormat] = output,
                    [ContextKeys.SkillsPathOverride] = PipelinePresets.GetDefaultSkillsPath("legal-analysis"),
                });

            if (isDryRun)
            {
                DryRunPrinter.Print(request, new Dictionary<string, string>
                {
                    ["Source"] = source,
                    ["Output"] = output
                });
                return;
            }

            var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty);
            var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();

            var result = await useCase.ExecuteAsync(request, configPath, CancellationToken.None);

            Console.WriteLine(result.IsSuccess
                ? $"Legal analysis complete: {result.Message}"
                : $"Legal analysis failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
