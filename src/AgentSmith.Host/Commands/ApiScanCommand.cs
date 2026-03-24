using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Host.Commands;

internal static class ApiScanCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var swaggerOption = new Option<string>("--swagger", "Path or URL to swagger.json") { IsRequired = true };
        var targetOption = new Option<string>("--target", "Base URL of the running API") { IsRequired = true };
        var outputOption = new Option<string>("--output", () => "console", "Output format: sarif | markdown | console");
        var projectOption = new Option<string>("--project", () => string.Empty, "Project name from config");
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");

        var cmd = new Command("api-scan", "Scan a running API against its OpenAPI spec")
        {
            swaggerOption, targetOption, outputOption, projectOption, configOption, verboseOption, dryRunOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var swagger = ctx.ParseResult.GetValueForOption(swaggerOption)!;
            var target = ctx.ParseResult.GetValueForOption(targetOption)!;
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "console";
            var project = ctx.ParseResult.GetValueForOption(projectOption) ?? string.Empty;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);

            var projectName = !string.IsNullOrWhiteSpace(project) ? project : "api-security";
            var swaggerPath = swagger.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? swagger : Path.GetFullPath(swagger);

            var request = new PipelineRequest(projectName, "api-security-scan", Headless: true,
                Context: new Dictionary<string, object>
                {
                    [ContextKeys.SwaggerPath] = swaggerPath,
                    [ContextKeys.ApiTarget] = target,
                    [ContextKeys.OutputFormat] = output,
                });

            if (isDryRun)
            {
                DryRunPrinter.Print(request, new Dictionary<string, string>
                {
                    ["Swagger"] = swagger,
                    ["Target"] = target,
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
                ? $"API scan complete: {result.Message}"
                : $"API scan failed: {result.Message}");
            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
