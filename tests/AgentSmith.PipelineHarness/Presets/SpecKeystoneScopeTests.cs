using System.Diagnostics;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-17-a196a, through the REAL composition over two real git repositories: the keystone of a spec
/// that names the server repository accounts for the server alone. Run f381 took a server spec's account
/// over three repositories, and its repair pass then worked in the worker.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecKeystoneScopeTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentsmith-scope-{Guid.NewGuid():N}");

    [Fact]
    public async Task Harness_VerifySpec_ServerSpec_AccountNamesServerOnly()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText(
            """[{"criterion":"the greeting is localised","satisfied":false,"note":"not on the branch"}]""");
        var pipeline = Pipeline(await RepositoryAsync("Sample.Server"), await RepositoryAsync("Sample.Worker"));

        var result = await harness.Services.GetRequiredService<ICommandHandler<VerifySpecContext>>()
            .ExecuteAsync(new VerifySpecContext(new Dictionary<string, ProjectMap>(), pipeline), CancellationToken.None);

        pipeline.Get<IReadOnlyList<SpecAccount>>(ContextKeys.SpecAccounts)
            .Should().ContainSingle().Which.RepoKey.Should().Be("Sample.Server");
        result.Message.Should().Contain("Sample.Server").And.NotContain("Sample.Worker",
            "the worker is another spec's repository, so nothing about it is this spec's verdict");
    }

    private static PipelineContext Pipeline(ISandbox server, ISandbox worker)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "code", new AgentConfig(), SkillsPath: string.Empty, CodingPrinciplesPath: null));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["Sample.Server"] = server, ["Sample.Worker"] = worker });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>
            {
                ["Sample.Server"] = new("default", ".", "generic"), ["Sample.Worker"] = new("default", ".", "generic"),
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["Sample.Server"] = "Sample.Server", ["Sample.Worker"] = "Sample.Worker" });
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("2026-10-17-0000a", "localise", "spec: x", [])
            { Done = ["the greeting is localised"], Repos = ["Sample.Server"] });
        return pipeline;
    }

    private async Task<ISandbox> RepositoryAsync(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(path, "src"));
        var file = Path.Combine(path, "src", "Program.cs");
        await File.WriteAllTextAsync(file, "Console.WriteLine(\"Hello\");\n");
        Git(path, "init", "-q", "-b", "develop");
        Git(path, "config", "user.email", "harness@example.test");
        Git(path, "config", "user.name", "harness");
        Git(path, "add", ".");
        Git(path, "commit", "-q", "-m", "base");
        await File.WriteAllTextAsync(file, "Console.WriteLine(\"Hallo\");\n");
        return new InProcessSandbox("harness", path, ownsWorkDir: false, NullLogger<InProcessSandbox>.Instance);
    }

    private static void Git(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardError = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        process.WaitForExit();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        return Task.CompletedTask;
    }
}
