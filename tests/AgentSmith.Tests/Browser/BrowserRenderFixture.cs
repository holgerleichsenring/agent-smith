using System.Net;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283de: the real render_reference composition — parser, guard, opener, hold,
/// materialiser, invocation, text — over a scripted resolver, a counting spawner, a set held in a
/// list and a deposit that records what reached it.
/// </summary>
internal sealed class BrowserRenderFixture(bool spawnsContainers = true)
    : ISandboxFactory, IHostAddressResolver, IReferenceSetReader, IToolImageDeposit
{
    public const string Conversation = "conv-283de";
    public static readonly ResolvedProject Project = new() { Name = "p" };

    public Dictionary<string, IPAddress[]> Hosts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ReferenceSetFile> Set { get; } = [];
    public List<BrowserFakeSandbox> Spawned { get; } = [];
    public List<SandboxSpec> Specs { get; } = [];
    public List<ToolImage> Deposited { get; } = [];
    public int SetReads { get; private set; }
    public List<string> SessionsRead { get; } = [];
    public IHeldSandboxRegister Holds { get; } = AgentSmith.Tests.Sandbox.Holds.Live();

    public RenderReferenceServices Services()
    {
        var specBuilder = new SandboxSpecBuilder(new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var opener = new BrowserSandboxOpener(new SandboxContainerRuntime(spawnsContainers), this, specBuilder,
            Mock.Of<IBrowserImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "registry/agent-smith-sandbox-browser:9.9.9"),
            Options.Create(new SandboxGlobalConfig()), Mock.Of<IRunContextAccessor>(), Holds,
            NullLogger<BrowserSandboxOpener>.Instance);
        var files = new SandboxFileReaderFactory();
        return new RenderReferenceServices(new RenderSourceParser(), new RenderUrlGuard(this, new PublicAddressRule()),
            new ReferenceRenderer(opener, new RenderSourceStager(new ReferenceSetMaterialiser(this, files),
                new RepoRenderSource(files, new RepoTreeListing())), new BrowserRenderInvocation(files)),
            this, new RenderResultText());
    }

    public RenderReferenceToolHost Host(IReadOnlyDictionary<string, ISandbox>? sandboxes = null) =>
        new(Services(), new RenderReferenceScope(Project, Conversation, sandboxes ?? new Dictionary<string, ISandbox>(), []));

    /// <summary>2026-10-01-283df: a run's host — no conversation, the sets the run carries.</summary>
    public RenderReferenceToolHost RunHost(IReadOnlyList<AgentSmith.Contracts.Specs.CarriedReferenceSet> carried) =>
        new(Services(), new RenderReferenceScope(Project, null, new Dictionary<string, ISandbox>(), carried));

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        Specs.Add(spec);
        var sandbox = new BrowserFakeSandbox();
        Spawned.Add(sandbox);
        return Task.FromResult<ISandbox>(sandbox);
    }

    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Task.FromResult(Hosts.TryGetValue(host, out var found) ? found : []);

    public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken)
    {
        SetReads++;
        SessionsRead.Add(sessionId);
        return Task.FromResult<IReadOnlyList<ReferenceSetFile>>(Set);
    }

    public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(["set-1"]);

    public ToolImageDepositResult Deposit(ToolImage image)
    {
        Deposited.Add(image);
        return ToolImageDepositResult.Accepted;
    }
}
