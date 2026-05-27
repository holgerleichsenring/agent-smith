using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

internal static class SkillsPullCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var versionOption = new Option<string?>(
            "--version", "Release tag to pull (overrides skills.version from config). Mutually exclusive with --url.");
        var urlOption = new Option<string?>(
            "--url", "Explicit tarball URL (overrides skills.url from config). Mutually exclusive with --version.");
        var outputOption = new Option<string?>(
            "--output", "Directory to extract into (overrides skills.cacheDir from config).");
        var sha256Option = new Option<string?>(
            "--sha256", "Expected SHA256 of the tarball (overrides skills.sha256 from config).");
        var forceOption = new Option<bool>(
            "--force", "Re-pull even if the marker matches the requested version.");

        var pullCmd = new Command("pull", "Download and extract the agentsmith-skills release tarball")
        {
            versionOption, urlOption, outputOption, sha256Option, forceOption,
            configOption, verboseOption,
        };

        pullCmd.SetHandler(async (InvocationContext ctx) =>
        {
            var versionArg = ctx.ParseResult.GetValueForOption(versionOption);
            var urlArg = ctx.ParseResult.GetValueForOption(urlOption);
            var outputArg = ctx.ParseResult.GetValueForOption(outputOption);
            var sha256Arg = ctx.ParseResult.GetValueForOption(sha256Option);
            var force = ctx.ParseResult.GetValueForOption(forceOption);
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var configPath = ctx.ParseResult.GetValueForOption(configOption);

            if (!string.IsNullOrWhiteSpace(versionArg) && !string.IsNullOrWhiteSpace(urlArg))
            {
                Console.Error.WriteLine("Error: --version and --url are mutually exclusive.");
                ctx.ExitCode = 2;
                return;
            }

            await using var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty);
            var client = provider.GetRequiredService<ISkillsRepositoryClient>();
            var marker = provider.GetRequiredService<ISkillsCacheMarker>();

            var skillsConfig = TryLoadSkillsConfig(provider, configPath);

            var version = versionArg ?? (string.IsNullOrWhiteSpace(urlArg) ? skillsConfig?.Version : null);
            var url = urlArg ?? (string.IsNullOrWhiteSpace(versionArg) ? skillsConfig?.Url : null);
            var output = outputArg ?? skillsConfig?.CacheDir;
            var sha256 = sha256Arg ?? skillsConfig?.Sha256;

            if (string.IsNullOrWhiteSpace(output))
            {
                Console.Error.WriteLine(
                    "Error: --output not given and no skills.cacheDir found in config.");
                ctx.ExitCode = 2;
                return;
            }

            if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(url))
            {
                Console.Error.WriteLine(
                    "Error: --version or --url required (and no skills.version/url in config).");
                ctx.ExitCode = 2;
                return;
            }

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

    private static SkillsConfig? TryLoadSkillsConfig(IServiceProvider provider, string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            return null;

        try
        {
            return provider.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath).Skills;
        }
        catch
        {
            return null;
        }
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
