using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.References;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.References;

/// <summary>2026-11-04-96a0c: storing what a turn read never fails the answer the turn already has.</summary>
public sealed class ReferenceReadRecorderTests
{
    private readonly ReferenceSandboxFixture _fixture = new();

    [Fact]
    public async Task ReferenceReadRecorder_StoreThrows_StampStillCarriesTheCounts()
    {
        _fixture.Set.AddRange([new ReferenceSetFile("app/a.cs", "a"u8.ToArray()), new ReferenceSetFile("app/b.cs", "b"u8.ToArray())]);
        var coverage = new ReferenceCoverage(
            new HashSet<string> { "app/a.cs", "app/b.cs", "app/c.cs" }, new HashSet<string> { "app/b.cs" }, 0);
        var upload = (ReferenceSetSandbox)_fixture.Factory(Holds.None()).Create(
            ReferenceSandboxFixture.Project, ReferenceSandboxFixture.Conversation, "reference:app", ReferenceSandboxFixture.SetId,
            coverage: coverage);
        await upload.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: "app/a.cs"),
            null, CancellationToken.None);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(f => f.CreateScope()).Throws(new InvalidOperationException("the store is gone"));

        var stamped = await new ReferenceReadRecorder(scopes.Object, NullLogger<ReferenceReadRecorder>.Instance)
            .RecordAsync(upload, CancellationToken.None);

        (stamped.ThisTurn, stamped.Opened, stamped.Files).Should().Be((1, 2, 3));
        scopes.Verify(f => f.CreateScope(), Times.Once, "the store was tried");
        await upload.DisposeAsync();
    }
}
