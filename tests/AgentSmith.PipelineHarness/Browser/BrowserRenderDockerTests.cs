using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Browser;

/// <summary>
/// 2026-10-01-283de docker tier: render_reference through the PRODUCTION Docker spawner — the
/// carrier injects the agent into the browser image, the uploaded set is copied in through the
/// agent, served from the local origin and rendered by Chromium behind the egress proxy. A page
/// of the set pulls a subresource from the sandbox network's Redis host by name; the proxy refuses
/// it and the tool lists it. Images come from HARNESS_BROWSER_IMAGE / HARNESS_AGENT_IMAGE.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class BrowserRenderDockerTests(ITestOutputHelper output) : IReferenceSetReader, IToolImageDeposit
{
    private const string Conversation = "conv-283de-docker";
    private readonly List<ToolImage> _deposited = [];

    [Fact]
    public async Task RenderReference_UploadedSetOnDocker_QuotesTheButtonAndRefusesTheRedisHost()
    {
        if (!DockerAvailability.IsAvailable(out var detail))
        {
            output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
            return;
        }
        var backend = await DockerBackend.BuildAsync();
        var holds = new HeldSandboxRegister(new AliveSandboxHeartbeat(), NullLogger<HeldSandboxRegister>.Instance);
        try
        {
            var host = new RenderReferenceToolHost(Services(backend.Factory, holds), new ResolvedProject { Name = "p" },
                Conversation, new Dictionary<string, ISandbox> { ["reference:site"] = Address() });

            var text = await host.RenderReference("reference:site/redis.html", ["button", "h1"], CancellationToken.None);
            output.WriteLine(text);

            text.Should().Contain("background-color: rgb(192, 255, 238)", "the root-relative stylesheet applied")
                .And.Contain("color: rgb(1, 2, 3)", "the module script ran from the local origin")
                .And.Contain($"{DockerBackend.RedisHostName}:6379", "the Redis host's request is listed as refused");
            text.Should().MatchRegex($@"Requests the egress guard refused \(\d+\):[\s\S]*{DockerBackend.RedisHostName}");
            _deposited.Should().HaveCount(2).And.OnlyContain(i => i.MediaType == "image/jpeg" && i.Bytes.Length < 700 * 1024);
        }
        finally
        {
            await holds.EvictAsync(CancellationToken.None);
        }
    }

    private RenderReferenceServices Services(ISandboxFactory factory, IHeldSandboxRegister holds)
    {
        var specBuilder = new SandboxSpecBuilder(Mock.Of<ISandboxResourceResolver>(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == DockerBackend.AgentImage));
        var opener = new BrowserSandboxOpener(new SandboxContainerRuntime(true), factory, specBuilder,
            Mock.Of<IBrowserImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == DockerBackend.BrowserImage),
            Options.Create(new SandboxGlobalConfig()), Mock.Of<IRunContextAccessor>(), holds,
            NullLogger<BrowserSandboxOpener>.Instance);
        var files = new SandboxFileReaderFactory();
        return new RenderReferenceServices(new RenderSourceParser(),
            new RenderUrlGuard(new DnsHostAddressResolver(), new PublicAddressRule()),
            new ReferenceRenderer(opener, new ReferenceSetMaterialiser(this, files), new BrowserRenderInvocation(files)),
            this, new RenderResultText());
    }

    // The address the turn's map carries; rendering reads only its set id and never opens it.
    private static ReferenceSetSandbox Address() => new(new ResolvedProject { Name = "p" }, Conversation,
        "reference:site", "set-docker", null!, null!, null!, NullLogger.Instance);

    public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken)
    {
        var root = Path.Combine(DockerBackend.RepositoryRoot, "src", "sandbox-images", "browser", "fixture", "set");
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => new ReferenceSetFile(Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllBytes(f)))
            .Append(new ReferenceSetFile("site/redis.html", System.Text.Encoding.UTF8.GetBytes(
                $"<!doctype html><link rel=\"stylesheet\" href=\"/css/site.css\"><script type=\"module\" src=\"/js/app.mjs\"></script>"
                + $"<h1>Redis</h1><button>Go</button><img src=\"http://{DockerBackend.RedisHostName}:6379/probe\">")));
        return Task.FromResult<IReadOnlyList<ReferenceSetFile>>([.. files]);
    }

    public ToolImageDepositResult Deposit(ToolImage image)
    {
        _deposited.Add(image);
        return ToolImageDepositResult.Accepted;
    }
}
