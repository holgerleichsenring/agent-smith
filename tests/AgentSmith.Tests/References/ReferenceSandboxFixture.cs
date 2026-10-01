using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283dc: the real reference composition — factory, opener, hold, materialiser — over
/// an in-memory container and a set held in a dictionary.
/// </summary>
internal sealed class ReferenceSandboxFixture : ISandboxFactory, IReferenceSetReader, ISandboxFileReaderFactory
{
    public const string Conversation = "conv-283dc";
    public const string SetId = "set-1";
    public static readonly ResolvedProject Project = new() { Name = "p" };

    public List<InMemoryFileSandbox> Spawned { get; } = [];

    public List<ReferenceSetFile> Set { get; } = [];

    public int SetReads { get; private set; }

    public IReferenceSetSandboxFactory Factory(IHeldSandboxRegister holds)
    {
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var opener = new SourceScopeOpener(
            new SourceScopeMaterialiser(new SourceScopeRefresh()), this, specBuilder, Mock.Of<IRunContextAccessor>());
        return new ReferenceSetSandboxFactory(
            opener, new ReferenceSetMaterialiser(this, this), holds, NullLogger<ReferenceSetSandbox>.Instance);
    }

    public ISourceScopeSandbox Open(IHeldSandboxRegister holds, string address = "reference:site") =>
        Factory(holds).Create(Project, Conversation, address, SetId);

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var sandbox = new InMemoryFileSandbox();
        Spawned.Add(sandbox);
        return Task.FromResult<ISandbox>(sandbox);
    }

    public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken)
    {
        SetReads++;
        return Task.FromResult<IReadOnlyList<ReferenceSetFile>>(sessionId == Conversation && setId == SetId ? Set : []);
    }

    public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(sessionId == Conversation ? [SetId] : []);

    public ISandboxFileReader Create(ISandbox sandbox) => new SandboxFileReaderFactory().Create(sandbox);
}
