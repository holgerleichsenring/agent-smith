using System.Net;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: on an installation without sign-in a tokenless caller lists every PoC and may import — the
/// dashboard learns the switch from this answer — and gets an empty 404 once the switch is off.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocListEnforceOffTests(PocsEnforceOffFixture fixture) : IClassFixture<PocsEnforceOffFixture>
{
    [Fact]
    public async Task List_NoToken_IsOkWithMayImport_SwitchOff_IsAnEmpty404()
    {
        await fixture.SeedAsync(db => PocRows.AddAsync(db, PocRows.Poc("open-app", "poc-bob")));

        var on = await fixture.Server.Client.SendAsync(fixture.As(null, "/api/pocs"));
        on.StatusCode.Should().Be(HttpStatusCode.OK);
        var answer = (await on.Content.ReadFromJsonAsync<PocListAnswer>())!;
        answer.MayImport.Should().BeTrue();
        answer.Pocs.Select(p => p.Name).Should().Contain("open-app");

        fixture.Pocs.Enabled = false;
        try
        {
            var off = await fixture.Server.Client.SendAsync(fixture.As(null, "/api/pocs"));
            off.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await off.Content.ReadAsStringAsync()).Should().BeEmpty();
        }
        finally
        {
            fixture.Pocs.Enabled = true;
        }
    }
}
