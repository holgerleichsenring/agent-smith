using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

internal static class SecurityScanCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var branchOption = new Option<string>("--branch", () => string.Empty, "Branch to check out and scan in full (default: the repository's default branch)");
        var outputOption = new Option<string>("--output", () => "console", "Output formats (comma-separated): console, summary, markdown, sarif");
        var outputDirOption = new Option<string?>("--output-dir", "Directory for file-based output (markdown, sarif)");
        var projectOption = new Option<string>("--project", () => string.Empty, "Project name from config (legacy; prefer --agent)");
        var agentOption = new Option<string>("--agent", () => string.Empty, "Agent name from config — runs the scan without a project (preferred). Wins over --project.");
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");
        var sourceOptions = new SourceOptions();

        var cmd = new Command("security-scan", "Analyze code for security vulnerabilities")
        {
            branchOption, outputOption, outputDirOption, projectOption, agentOption, configOption, verboseOption, dryRunOption
        };
        sourceOptions.AddTo(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var branch = ctx.ParseResult.GetValueForOption(branchOption) ?? string.Empty;
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "console";
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOption);
            var project = ctx.ParseResult.GetValueForOption(projectOption) ?? string.Empty;
            var agent = ctx.ParseResult.GetValueForOption(agentOption) ?? string.Empty;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);

            if (string.IsNullOrWhiteSpace(agent) && string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("security-scan requires --agent <name> (preferred) or --project <name>.");
                ctx.ExitCode = 1;
                return;
            }

            var scanContext = new Dictionary<string, object>
            {
                [ContextKeys.OutputFormat] = output,
            };
            sourceOptions.ApplyTo(ctx, scanContext);

            if (!string.IsNullOrWhiteSpace(branch))
                scanContext[ContextKeys.ScanBranch] = branch;
            if (outputDir is not null)
                scanContext[ContextKeys.OutputDir] = outputDir;

            var projectName = string.IsNullOrWhiteSpace(project) ? "security-scan" : project;
            var request = new PipelineRequest(projectName, "security-scan", Headless: true, Context: scanContext,
                AgentName: string.IsNullOrWhiteSpace(agent) ? null : agent);

            if (isDryRun)
            {
                DryRunPrinter.Print(request, new Dictionary<string, string>
                {
                    ["Branch"] = string.IsNullOrWhiteSpace(branch) ? "(default branch)" : branch,
                    ["Output"] = output
                });
                return;
            }

            var provider = ServiceProviderFactory.Build(configPath, verbose, headless: true);
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
                ? $"Security scan complete: {result.Message}"
                : $"Security scan failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
