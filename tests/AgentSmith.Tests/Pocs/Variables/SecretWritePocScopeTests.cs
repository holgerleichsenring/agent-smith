using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: a write in a PoC's scope adds no catalog secret and keeps the masker's minimum; the
/// variable seed never reaches a checkpoint while the sealed result does.</summary>
public sealed class SecretWritePocScopeTests
{
    private readonly Mock<ISecretVault> _vault = new();
    private readonly Mock<IConfigStore> _store = new();

    [Fact]
    public async Task SecretWrite_PocScope_AddsNoCatalogSecretEntity()
    {
        _store.Setup(s => s.GetSecrets()).Returns([]);

        var report = await new SecretWrite(_vault.Object, _store.Object).RunAsync(
            new SecretWriteRequest("OPENAI_API_KEY", SecretWriteRequest.Set, new SecretInput("sk-value-123456"), "ann", SecretScope.Poc(7)), "run-1", default);

        report.Written.Should().BeTrue();
        _store.Verify(s => s.UpsertSecret(It.IsAny<SecretEntity>(), It.IsAny<ChangeAttribution>()), Times.Never);
        _vault.Verify(v => v.SetAsync(SecretScope.Poc(7), "OPENAI_API_KEY", It.IsAny<SecretInput>(), "ann", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SecretWrite_AFiveCharacterPocSecret_IsRefusedNamingTheMinimum()
    {
        var report = await new SecretWrite(_vault.Object, _store.Object).RunAsync(
            new SecretWriteRequest("API_KEY", SecretWriteRequest.Set, new SecretInput("12345"), "ann", SecretScope.Poc(7)), "run-1", default);

        (report.Written, report.Line.Contains("at least 6 characters")).Should().Be((false, true));
    }

    [Fact]
    public void Checkpoint_ExcludesTheVariableWrite_AndKeepsTheSeal()
    {
        var serializer = new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance);
        var context = new PipelineContext();
        context.Set(ContextKeys.PocVariableWrite, new PocVariableWrite(7, [new PocVariableEntry("K", PocVariableKinds.Secret, new SecretInput("sk-value-123456"))], "ann"));
        context.Set(ContextKeys.SecretWriteRequest, new SecretWriteRequest("x", SecretWriteRequest.Set, new SecretInput("sk-value-123456"), "ann"));
        context.Set(ContextKeys.PocSeal, new PocSealResult(false, [new PocSecretFile("poc-secrets.sealed.yaml", "kind: SealedSecret")], "hmac-sha256:ab", "cd"));

        var json = serializer.Serialize(context);
        var restored = new PipelineContext();
        serializer.Restore(json, restored);

        json.Should().NotContain("sk-value-123456").And.NotContain(ContextKeys.PocVariableWrite);
        restored.Get<PocSealResult>(ContextKeys.PocSeal).Files.Single().FileName.Should().Be("poc-secrets.sealed.yaml");
    }
}
