using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

internal static class SecurityScanCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var repoOption = new Option<string>("--repo", "Path or URL of repository to scan") { IsRequired = true };
        var prOption = new Option<string>("--pr", () => string.Empty, "PR/MR number (diff only)");
        var branchOption = new Option<string>("--branch", () => string.Empty, "Branch to scan (diff against main)");
        var outputOption = new Option<string>("--output", () => "console", "Output format: sarif | markdown | console");
        var projectOption = new Option<string>("--project", () => string.Empty, "Project name from config");
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");

        var cmd = new Command("security-scan", "Analyze code for security vulnerabilities")
        {
            repoOption, prOption, branchOption, outputOption, projectOption, configOption, verboseOption, dryRunOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var repo = ctx.ParseResult.GetValueForOption(repoOption)!;
            var pr = ctx.ParseResult.GetValueForOption(prOption) ?? string.Empty;
            var branch = ctx.ParseResult.GetValueForOption(branchOption) ?? string.Empty;
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "console";
            var project = ctx.ParseResult.GetValueForOption(projectOption) ?? string.Empty;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);

            var projectName = !string.IsNullOrWhiteSpace(project)
                ? project : Path.GetFileName(Path.GetFullPath(repo));

            var scanContext = new Dictionary<string, object>
            {
                [ContextKeys.ScanRepoPath] = Path.GetFullPath(repo),
                [ContextKeys.OutputFormat] = output,
                [ContextKeys.SkillsPathOverride] = PipelinePresets.GetDefaultSkillsPath("security-scan"),
            };
            if (!string.IsNullOrWhiteSpace(pr))
                scanContext[ContextKeys.ScanPrIdentifier] = pr;
            if (!string.IsNullOrWhiteSpace(branch))
                scanContext[ContextKeys.ScanBranch] = branch;

            var request = new PipelineRequest(projectName, "security-scan", Headless: true, Context: scanContext);

            if (isDryRun)
            {
                DryRunPrinter.Print(request, new Dictionary<string, string>
                {
                    ["Repo"] = Path.GetFullPath(repo),
                    ["PR"] = string.IsNullOrWhiteSpace(pr) ? "(full repo)" : $"#{pr}",
                    ["Branch"] = string.IsNullOrWhiteSpace(branch) ? "(main)" : branch,
                    ["Output"] = output
                });
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
                ? $"Security scan complete: {result.Message}"
                : $"Security scan failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
