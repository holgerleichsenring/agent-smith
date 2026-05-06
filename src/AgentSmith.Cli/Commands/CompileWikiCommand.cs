using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Cli.Commands;

internal static class CompileWikiCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var projectOption = new Option<string>("--project", "Project directory containing .agentsmith/runs/") { IsRequired = true };
        var fullOption = new Option<bool>("--full", "Ignore .last-compiled and recompile all runs");
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be compiled without executing");

        var cmd = new Command("compile-wiki", "Compile run history into a project knowledge base wiki")
        {
            projectOption, configOption, verboseOption, fullOption, dryRunOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var fullRecompile = ctx.ParseResult.GetValueForOption(fullOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);

            var projectPath = Path.GetFullPath(project);

            if (isDryRun)
            {
                Console.WriteLine("Dry run - would execute:");
                Console.WriteLine($"  Command:  compile-wiki");
                Console.WriteLine($"  Project:  {projectPath}");
                Console.WriteLine($"  Full:     {fullRecompile}");
                Console.WriteLine("  Steps:");
                Console.WriteLine("    - Scan .agentsmith/runs/ for new runs");
                Console.WriteLine("    - Compile run results into knowledge base wiki");
                Console.WriteLine("    - Update .last-compiled marker");
                return;
            }

            if (!Directory.Exists(projectPath))
            {
                Console.Error.WriteLine($"Project directory not found: {projectPath}");
                ctx.ExitCode = 1;
                return;
            }

            if (!Directory.Exists(Path.Combine(projectPath, ".agentsmith", "runs")))
            {
                Console.Error.WriteLine($"No runs directory found in {projectPath}/.agentsmith");
                ctx.ExitCode = 1;
                return;
            }

            var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty);
            var handler = provider.GetRequiredService<ICommandHandler<CompileKnowledgeContext>>();

            var jobId = Guid.NewGuid().ToString("N");
            var sandboxLogger = provider.GetService<ILogger<InProcessSandbox>>() ?? NullLogger<InProcessSandbox>.Instance;
            // No `await using` here: InProcessSandbox.DisposeAsync deletes workDir, but
            // workDir IS the user's project directory. The sandbox is one-shot for the
            // duration of this CLI command — process exit cleans up.
            var sandbox = new InProcessSandbox(jobId, projectPath, sandboxLogger);

            var pipeline = new PipelineContext();
            pipeline.Set(ContextKeys.Sandbox, (ISandbox)sandbox);

            var repo = new Repository(new BranchName("main"), string.Empty);
            var context = new CompileKnowledgeContext(
                repo, fullRecompile, new AgentConfig { Type = "claude" }, pipeline);

            CommandResult result;
            try
            {
                result = await handler.ExecuteAsync(context, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result = CommandResult.Fail($"Unhandled exception: {ex.Message}");
                Console.Error.WriteLine($"Fatal: {ex}");
            }

            Console.WriteLine(result.IsSuccess
                ? $"Wiki compiled: {result.Message}"
                : $"Wiki compilation failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
