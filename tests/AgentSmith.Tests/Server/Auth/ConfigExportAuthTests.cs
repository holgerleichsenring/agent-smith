using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: `agentsmith config export` assembles from the DOCUMENT STORE alone, and the auth
/// block is never stored there — it is bootstrap, read before the store exists. Shipping an
/// auth descriptor would therefore not preserve a lost block; it would emit a FALSE one, an
/// installation reading "enforce: false, no authority" out of a server that has both.
/// A lie in a disaster-recovery artifact is worse than an omission.
/// </summary>
public sealed class ConfigExportAuthTests
{
    [Fact]
    public void Export_WithAnAuthBlockConfigured_EmitsNoAuthSection()
    {
        var configured = new RawAgentSmithConfig
        {
            Auth = new TokenAuthorityConfig
            {
                Authority = "https://an-authority-that-is-really-configured",
                Audience = "an-audience",
                Enforce = true,
            },
        };
        var assembler = new ConfigDocumentAssembler();

        var rows = assembler.Decompose(configured)
            .Select(d => new ConfigDocRow(d.Type, d.Id, d.Doc, 1))
            .ToList();
        var exported = new RawConfigYaml().Serialize(assembler.Assemble(rows));

        exported.Should().NotContain("auth:");
        exported.Should().NotContain("an-authority-that-is-really-configured");
    }
}
