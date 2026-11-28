using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-2c06: a design turn's run_command through the REAL server composition, in a work clone of a repository whose
/// nuget.config and .npmrc name the project's private feeds. Seen live: the clone staged no credentials, npm ci failed for
/// lack of registry authentication and the design agent asked the operator about it. Here each restore answers 401 until
/// its user-level config is written, as the real feed would, so a green restore proves the clone staged the credentials.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DesignTurnFeedCompositionTests
{
    private const string Repo = "design-feed-fixture";
    private const string Project = "fixture-design-feed";
    private const string NuGetFeed = "https://pkgs.dev.azure.com/sample-org/_packaging/sample-feed/nuget/v3/index.json";
    private const string NpmFeed = "https://pkgs.dev.azure.com/sample-org/_packaging/sample-feed/npm/registry/";

    [Fact]
    public async Task WorkClone_PrivateFeeds_RestoreWithStagedCredentials()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.DesignFeed), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<SandboxContainerRuntime>();
            services.AddSingleton(new SandboxContainerRuntime(SpawnsContainers: true));
            services.RemoveAll<ISandboxBusIsolation>();
            services.AddSingleton<ISandboxBusIsolation>(new PerJobBus());
        });
        var sandboxes = harness.StubSandboxFactory!;
        sandboxes.ListedFiles = new(StringComparer.Ordinal) { ["nuget.config"] = NuGetConfig(), [".npmrc"] = Npmrc() };
        sandboxes.ShellAnswers["dotnet restore"] = (1, $"error NU1301: Unable to load the service index for source {NuGetFeed}. Response status code does not indicate success: 401 (Unauthorized).");
        sandboxes.ShellAnswers["npm ci"] = (1, "npm error code E401\nnpm error Unable to authenticate, need: Basic realm=\"https://pkgsprodsu3weu.app.pkgs.visualstudio.com/\"");
        sandboxes.AnswerAfterWriting("NuGet.Config", "dotnet restore", (0, "Restored /work/Fixture.csproj"));
        sandboxes.AnswerAfterWriting(".npmrc", "npm ci", (0, "added 12 packages in 1s"));
        harness.ChatClient
            .EnqueueToolCall("run_command", $$"""{"command":"dotnet restore","repo":"{{Repo}}"}""")
            .EnqueueToolCall("run_command", $$"""{"command":"npm ci","repo":"{{Repo}}"}""")
            .EnqueueText("Both restores pass.");

        await RunTurnAsync(harness);

        var results = harness.ChatClient.CallMessages.SelectMany(m => m).SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? string.Empty).ToList();
        results.Should().Contain(r => r.Contains("Restored /work/Fixture.csproj"), "the NuGet credentials were staged first");
        results.Should().Contain(r => r.Contains("added 12 packages"), "the npm credentials were staged first");
        var steps = sandboxes.Spawned.Select(s => s.Sandbox.RanSteps)
            .Single(ran => ran.Any(s => s.Args?.Contains("--detach") == true)); // the work clone, not the read scope
        var nuget = steps.FindIndex(s => s.Kind == StepKind.WriteFile && s.Path == "/root/.nuget/NuGet/NuGet.Config"
            && s.Content!.Contains("harness-feed-token") && s.Content.Contains(NuGetFeed));
        var npm = steps.FindIndex(s => s.Kind == StepKind.WriteFile && s.Path == "/root/.npmrc"
            && s.Content!.Contains("harness-feed-token") && s.Content.Contains("//pkgs.dev.azure.com/"));
        var firstShell = steps.FindIndex(s => s.Command == "/bin/sh");
        nuget.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(firstShell, "the NuGet user config is staged before the model's first command");
        npm.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(firstShell, "the npm user config is staged before the model's first command");
        sandboxes.Spawned.SelectMany(s => s.Sandbox.RanSteps).Where(s => s.Kind == StepKind.WriteFile).Should().OnlyContain(s => s.Path!.StartsWith("/root/"),
            "credentials go to user-level config, never into the repository");
    }

    private sealed class PerJobBus : ISandboxBusIsolation
    {
        public bool IsShared => false;
    }

    private static async Task RunTurnAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(DispatcherDefaults.PlatformDashboard, "d-feed", "d-feed", "U-feed",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        var state = opened.AppendTurn(new TranscriptTurn(TranscriptRole.User, "does the solution restore?", DateTimeOffset.UtcNow));
        await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U-feed", true), CancellationToken.None);
    }

    private static string NuGetConfig() => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
            <add key="sample-feed" value="{NuGetFeed}" />
          </packageSources>
        </configuration>
        """;

    private static string Npmrc() => $"registry={NpmFeed}\nalways-auth=true\n";
}
