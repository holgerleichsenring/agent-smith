using System.Net;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: GET /api/pocs on an enforcing server — a poc-role caller bound to poc-ann lists only poc-ann's PoCs,
/// an operator lists all, a reader is refused 403, and with the switch off even an admin gets an empty 404.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocListEndpointTests(PocsAuthorityFixture fixture) : IClassFixture<PocsAuthorityFixture>
{
    private const string Route = "/api/pocs";

    [Fact]
    public async Task List_PocCaller_SeesOwnProjectsPocs_OperatorSeesAll_AnswerCarriesTargetsAndMayImport()
    {
        await fixture.SeedAsync(async db =>
        {
            await PocRows.AddAsync(db, PocRows.Poc("ann-todo", "poc-ann"));
            await PocRows.AddAsync(db, PocRows.Poc("bob-shop", "poc-bob"));
        });

        var ann = await ListAsync(Ann);
        var op = await ListAsync(Operators);

        ann.Pocs.Select(p => p.Name).Should().Contain("ann-todo").And.NotContain("bob-shop");
        ann.Pocs.Single(p => p.Name == "ann-todo").Host.Should().Be("ann-todo.poc.example.internal");
        ann.MayImport.Should().BeTrue("the poc role holds pocs.write");
        ann.Targets.Should().ContainSingle().Which.Should().Be(new PocTargetView("main", "gh/poc-src", "pocs"));
        op.Pocs.Select(p => p.Name).Should().Contain(["ann-todo", "bob-shop"]);
        op.MayImport.Should().BeFalse("an operator holds pocs.read only");
    }

    [Fact]
    public async Task List_Reader_IsForbidden()
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(Readers, Route));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_SwitchOff_IsAnEmpty404EvenForAnAdmin()
    {
        fixture.Pocs.Enabled = false;
        try
        {
            var response = await fixture.Server.Client.SendAsync(fixture.As(Admins, Route));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        }
        finally
        {
            fixture.Pocs.Enabled = true;
        }
    }

    private async Task<PocListAnswer> ListAsync(string group)
    {
        var response = await fixture.Server.Client.SendAsync(fixture.As(group, Route));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PocListAnswer>())!;
    }
}
