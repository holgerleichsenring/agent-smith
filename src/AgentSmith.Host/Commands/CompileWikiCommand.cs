using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Host.Commands;

internal static class CompileWikiCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var projectOption = new Option<string>("--project", "Project directory containing .agentsmith/runs/") { IsRequired = true };
        var fullOption = new Option<bool>("--full", "Ignore .last-compiled and recompile all runs");

        var cmd = new Command("compile-wiki", "Compile run history into a project knowledge base wiki")
        {
            projectOption, configOption, verboseOption, fullOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var fullRecompile = ctx.ParseResult.GetValueForOption(fullOption);

            var projectPath = Path.GetFullPath(project);

            if (!Directory.Exists(projectPath))
            {
                Console.Error.WriteLine($"Project directory not found: {projectPath}");
                ctx.ExitCode = 1;
                return;
            }

            var agentSmithDir = Path.Combine(projectPath, ".agentsmith");
            if (!Directory.Exists(Path.Combine(agentSmithDir, "runs")))
            {
                Console.Error.WriteLine($"No runs directory found in {agentSmithDir}");
                ctx.ExitCode = 1;
                return;
            }

            var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty);
            var handler = provider.GetRequiredService<ICommandHandler<CompileKnowledgeContext>>();

            var repo = new Repository(projectPath, new BranchName("main"), string.Empty);
            var pipeline = new PipelineContext();
            var context = new CompileKnowledgeContext(repo, fullRecompile, pipeline);

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
