using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

internal static class ApiScanCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var swaggerOption = new Option<string>("--swagger", "Path or URL to swagger.json") { IsRequired = true };
        var targetOption = new Option<string>("--target", "Base URL of the running API") { IsRequired = true };
        var outputOption = new Option<string>("--output", () => "console", "Output formats (comma-separated): console, summary, markdown, sarif");
        var outputDirOption = new Option<string?>("--output-dir", "Directory for file-based output (markdown, sarif)");
        var projectOption = new Option<string>("--project", () => string.Empty, "Project name from config");
        var dryRunOption = new Option<bool>("--dry-run", "Show pipeline only, don't execute");

        // p79: Persona credential options for active mode
        var adminUserOption = new Option<string?>("--admin-user", "Admin persona username");
        var adminPassOption = new Option<string?>("--admin-pass", "Admin persona password");
        var user1UserOption = new Option<string?>("--user1-user", "User1 persona username");
        var user1PassOption = new Option<string?>("--user1-pass", "User1 persona password");
        var user2UserOption = new Option<string?>("--user2-user", "User2 persona username");
        var user2PassOption = new Option<string?>("--user2-pass", "User2 persona password");

        var sourceOptions = new SourceOptions();
        var cmd = new Command("api-scan", "Scan a running API against its OpenAPI spec")
        {
            swaggerOption, targetOption, outputOption, outputDirOption, projectOption, configOption, verboseOption, dryRunOption,
            adminUserOption, adminPassOption, user1UserOption, user1PassOption, user2UserOption, user2PassOption
        };
        sourceOptions.AddTo(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var swagger = ctx.ParseResult.GetValueForOption(swaggerOption)!;
            var target = ctx.ParseResult.GetValueForOption(targetOption)!;
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "console";
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOption);
            var project = ctx.ParseResult.GetValueForOption(projectOption) ?? string.Empty;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);

            var projectName = !string.IsNullOrWhiteSpace(project) ? project : "api-security";
            var swaggerPath = swagger.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? swagger : Path.GetFullPath(swagger);

            // Set config directory so tool spawners find their config files
            if (!string.IsNullOrWhiteSpace(configPath))
                Environment.SetEnvironmentVariable("AGENTSMITH_CONFIG_DIR",
                    Path.GetDirectoryName(Path.GetFullPath(configPath)));

            // p79: Build personas from CLI credentials
            var personas = BuildPersonas(ctx, adminUserOption, adminPassOption,
                user1UserOption, user1PassOption, user2UserOption, user2PassOption);

            var contextData = new Dictionary<string, object>
            {
                [ContextKeys.SwaggerPath] = swaggerPath,
                [ContextKeys.ApiTarget] = target,
                [ContextKeys.OutputFormat] = output,
            };

            if (outputDir is not null)
                contextData[ContextKeys.OutputDir] = outputDir;

            if (personas.Count > 0)
                contextData[ContextKeys.Personas] = personas;

            sourceOptions.ApplyTo(ctx, contextData);

            // Pre-flight banner — final source state and skill count are emitted
            // by TryCheckoutSourceHandler after resolution (p0102a).
            var modeLabel = personas.Count > 0
                ? $"Active mode — {personas.Count} persona(s): {string.Join(", ", personas.Keys)}"
                : "Passive mode — no credentials provided";
            Console.WriteLine($"{modeLabel} | Resolving source...");

            var request = new PipelineRequest(projectName, "api-security-scan", Headless: true,
                Context: contextData);

            if (isDryRun)
            {
                DryRunPrinter.Print(request, new Dictionary<string, string>
                {
                    ["Swagger"] = swagger,
                    ["Target"] = target,
                    ["Output"] = output,
                    ["Output Dir"] = outputDir ?? "(default)"
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

    private static Dictionary<string, PersonaCredentials> BuildPersonas(
        InvocationContext ctx,
        Option<string?> adminUserOption, Option<string?> adminPassOption,
        Option<string?> user1UserOption, Option<string?> user1PassOption,
        Option<string?> user2UserOption, Option<string?> user2PassOption)
    {
        var personas = new Dictionary<string, PersonaCredentials>(StringComparer.OrdinalIgnoreCase);

        AddPersona(personas, "admin",
            ctx.ParseResult.GetValueForOption(adminUserOption),
            ctx.ParseResult.GetValueForOption(adminPassOption));
        AddPersona(personas, "user1",
            ctx.ParseResult.GetValueForOption(user1UserOption),
            ctx.ParseResult.GetValueForOption(user1PassOption));
        AddPersona(personas, "user2",
            ctx.ParseResult.GetValueForOption(user2UserOption),
            ctx.ParseResult.GetValueForOption(user2PassOption));

        return personas;
    }

    private static void AddPersona(
        Dictionary<string, PersonaCredentials> personas,
        string name, string? username, string? password)
    {
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            personas[name] = new PersonaCredentials { Username = username, Password = password };
    }

}
