using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: one design conversation's turns over the real scope composition — the
/// factory, the opener, the materialiser and the register — with only the container faked.
/// <para>
/// A turn opens a FRESH scope, reads through it and ends it; what the turn saw is captured
/// BEFORE the scope is disposed, because a disposed scope reports the placeholder job id it
/// had before it materialised.
/// </para>
/// </summary>
internal sealed class SourceScopeHoldFixture
{
    public const string Conversation = "a1b2c3d4";
    public const string Url = "https://stub.test/repo-a";

    public static readonly ResolvedProject Project = new() { Name = "p" };

    public FakeSandboxSpawner Spawner { get; } = new();

    public AsyncLocalSourceScopeObserverAccessor Observers { get; } = new();

    public ISourceScopeSandboxFactory Scopes(IHeldSandboxRegister register)
    {
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var opener = new SourceScopeOpener(
            new SourceScopeMaterialiser(new SourceScopeRefresh()), Spawner, specBuilder, Mock.Of<IRunContextAccessor>());
        return new SourceScopeSandboxFactory(
            opener, Observers, register, NullLogger<SourceScopeSandbox>.Instance);
    }

    public async Task<TurnRead> TurnAsync(
        ISourceScopeSandboxFactory scopes, string? conversation = null, string? revision = null)
    {
        var scope = await OpenAsync(scopes, conversation, revision);
        var read = new TurnRead(scope, scope.JobId, scope.ResolvedSha, scope.IsMaterialized);
        await scope.DisposeAsync();
        return read;
    }

    /// <summary>A turn that has read but has not ended — where a filing reads its provenance.</summary>
    public async Task<ISourceScopeSandbox> OpenAsync(
        ISourceScopeSandboxFactory scopes, string? conversation = null, string? revision = null)
    {
        var scope = scopes.Create(Project, Repo(), revision, conversation ?? Conversation);
        await scope.MaterializeAsync(CancellationToken.None);
        return scope;
    }

    public static RepoConnection Repo() =>
        new() { Name = "repo-a", Type = RepoType.GitHub, Url = Url };

    internal sealed record TurnRead(
        ISourceScopeSandbox Scope, string JobId, string? Sha, bool Materialised);
}
