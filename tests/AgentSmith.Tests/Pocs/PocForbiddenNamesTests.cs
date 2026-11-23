using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocZips;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: names a PoC may not carry anywhere in its tree are refused naming the entry — a reference
/// upload leaves them out, a PoC is refused so the person removes them.</summary>
public sealed class PocForbiddenNamesTests
{
    private readonly PocArchiveReader _reader = Reader();

    [Theory]
    [InlineData("app/src/deep/.env", ".env")]
    [InlineData("app/src/.env.local", ".env.local")]
    [InlineData("app/ops/docker-compose.override.yml", "docker-compose.override.yml")]
    [InlineData("app/ops/compose.yaml", "compose.yaml")]
    [InlineData("app/keys/id_rsa", "id_rsa")]
    [InlineData("app/keys/id_ed25519", "id_ed25519")]
    [InlineData("app/certs/server.pem", "server.pem")]
    [InlineData("app/certs/store.p12", "store.p12")]
    [InlineData("app/certs/STORE.PFX", "STORE.PFX")]
    [InlineData("app/vendor/lib/.git/config", ".git")]
    [InlineData("app/vendor/lib/.git", ".git")]
    public void Admit_AForbiddenNameAtDepth_IsRefusedNamingIt(string path, string name)
    {
        var admission = _reader.Admit(App((path, Text("x"))), new PocArchiveLimits());

        admission.Check.Should().Be(PocArchiveChecks.Forbidden);
        admission.Refusal.Should().StartWith($"'{path["app/".Length..]}' carries '{name}', which a PoC may not hold");
    }

    [Theory]
    [InlineData("app/.gitignore")]
    [InlineData("app/.envrc.example.txt")]
    [InlineData("app/docs/compose.md")]
    public void Admit_ANameThatOnlyLooksAlike_Passes(string path)
    {
        _reader.Admit(App((path, Text("x"))), new PocArchiveLimits()).IsRefused.Should().BeFalse();
    }
}
