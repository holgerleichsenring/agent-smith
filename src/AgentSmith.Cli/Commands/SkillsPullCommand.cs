using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Commands;

internal static class SkillsPullCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var versionOption = new Option<string?>(
            "--version", "Release tag to pull (e.g. v1.0.0). Mutually exclusive with --url.");
        var urlOption = new Option<string?>(
            "--url", "Explicit tarball URL. Mutually exclusive with --version.");
        var outputOption = new Option<string>(
            "--output", "Directory to extract the catalog into") { IsRequired = true };
        var sha256Option = new Option<string?>(
            "--sha256", "Expected SHA256 of the tarball (verification)");
        var forceOption = new Option<bool>(
            "--force", "Re-pull even if the marker matches the requested version");

        var pullCmd = new Command("pull", "Download and extract the agentsmith-skills release tarball")
        {
            versionOption, urlOption, outputOption, sha256Option, forceOption,
            configOption, verboseOption,
        };

        pullCmd.SetHandler(async (InvocationContext ctx) =>
        {
            var version = ctx.ParseResult.GetValueForOption(versionOption);
            var url = ctx.ParseResult.GetValueForOption(urlOption);
            var output = ctx.ParseResult.GetValueForOption(outputOption)!;
            var sha256 = ctx.ParseResult.GetValueForOption(sha256Option);
            var force = ctx.ParseResult.GetValueForOption(forceOption);
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

            if (!string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(url))
            {
                Console.Error.WriteLine("Error: --version and --url are mutually exclusive.");
                ctx.ExitCode = 2;
                return;
            }

            if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(url))
            {
                Console.Error.WriteLine("Error: one of --version or --url is required.");
                ctx.ExitCode = 2;
                return;
            }

            await using var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty);
            var client = provider.GetRequiredService<ISkillsRepositoryClient>();
            var marker = provider.GetRequiredService<ISkillsCacheMarker>();

            Uri tarballUrl;
            string? markerVersion = null;

            if (!string.IsNullOrWhiteSpace(version))
            {
                tarballUrl = client.ResolveReleaseUrl(version);
                markerVersion = version;

                if (!force && marker.Read(output) == version &&
                    Directory.Exists(Path.Combine(output, "skills")))
                {
                    Console.WriteLine($"Already at {version} in {output} (use --force to re-pull)");
                    return;
                }
            }
            else
            {
                tarballUrl = new Uri(url!);
            }

            try
            {
                await client.PullAsync(tarballUrl, output, sha256, ctx.GetCancellationToken());
                if (markerVersion is not null)
                    marker.Write(output, markerVersion);
                Console.WriteLine($"Catalog extracted to {output}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Pull failed: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return pullCmd;
    }
}

internal static class SkillsCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var cmd = new Command("skills", "Skill-catalog management commands")
        {
            SkillsPullCommand.Create(configOption, verboseOption),
        };
        return cmd;
    }
}
