using System.Net;
using System.Net.Http.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Pocs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>
/// 2026-11-23-933dg, over HTTP on an enforcing server with a master key: the owner's PUT writes through a gated
/// poc-variables run; a PUT while another run of the PoC is live answers 409 naming it and writes nothing; another poc
/// user gets the empty 404; GET shows plain values and never a secret; Start with variables writes them in the PoC's
/// project before poc-deploy is queued; a variable that breaks the rule refuses Start with nothing written.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocVariableEndpointTests(PocVariablesFixture fixture) : IClassFixture<PocVariablesFixture>
{
    private const string Value = "sk-live-value-933dg";
    private readonly PocImportClient _client = new(fixture);

    [Fact]
    public async Task Put_ByTheOwner_WritesThroughAPocVariablesRun_AndGetShowsNoSecretValue()
    {
        var poc = await SeedAsync("vars-app", "poc-ann");

        var put = await _client.CallAsync(Ann, HttpMethod.Put, $"/api/pocs/{poc.Id}/variables", new
        {
            set = new object[] { new { name = "OPENAI_API_KEY", secret = true, value = Value }, new { name = "LOG_LEVEL", secret = false, value = "debug" } },
        });
        var get = await _client.CallAsync(Ann, HttpMethod.Get, $"/api/pocs/{poc.Id}/variables");

        var written = await put.Content.ReadAsStringAsync();
        put.StatusCode.Should().Be(HttpStatusCode.OK, written);
        written.Should().Contain("OPENAI_API_KEY (secret)").And.NotContain(Value);
        var shown = await get.Content.ReadAsStringAsync();
        shown.Should().Contain("\"value\":\"debug\"").And.Contain("OPENAI_API_KEY").And.NotContain(Value);
        (await _client.ReadAsync(db => db.Set<StoredSecret>().AsNoTracking().CountAsync(s => s.Scope == SecretScope.Poc(poc.Id).Text))).Should().Be(1);
        fixture.Logs.Entries.Should().NotContain(e => e.Message.Contains(Value));
        (await VariablesRunsAsync()).Should().BePositive("the write ran as a poc-variables run of poc-ann");
    }

    [Fact]
    public async Task Put_WhileAnotherRunOfThePocIsLive_Answers409NamingIt_AndWritesNothing()
    {
        var poc = await SeedAsync("busy-vars", "poc-ann");
        await using (var scope = fixture.Server.Services.CreateAsyncScope())
        {
            var claim = await scope.ServiceProvider.GetRequiredService<PocRunGate>().ClaimAsync(poc, PipelinePresets.PocDeployName, "deploying", default);
            claim.RunId.Should().NotBeNull();
        }

        var put = await _client.CallAsync(Ann, HttpMethod.Put, $"/api/pocs/{poc.Id}/variables",
            new { set = new[] { new { name = "OPENAI_API_KEY", secret = true, value = Value } } });

        put.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await put.Content.ReadAsStringAsync()).Should().Contain("is in progress (run ");
        (await _client.ReadAsync(db => db.Set<StoredSecret>().AsNoTracking().CountAsync(s => s.Scope == SecretScope.Poc(poc.Id).Text))).Should().Be(0);
    }

    [Fact]
    public async Task Put_OnAnotherPersonsPoc_IsAnEmpty404()
    {
        var poc = await SeedAsync("anns-app", "poc-ann");

        var put = await _client.CallAsync(Bob, HttpMethod.Put, $"/api/pocs/{poc.Id}/variables",
            new { set = new[] { new { name = "OPENAI_API_KEY", secret = true, value = Value } } });

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await put.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Start_WithVariables_WritesThemInThePocsProject_BeforePocDeployIsQueuedWithThePocId()
    {
        var poc = await SeedAsync("start-vars", "poc-ann");
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());
        var before = await VariablesRunsAsync();

        var response = await _client.StartAsync(Ann, upload, new
        {
            choice = "replace", pocId = poc.Id, confirmed = true,
            variables = new[] { new { name = "OPENAI_API_KEY", secret = true, value = Value } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var started = (await response.Content.ReadFromJsonAsync<PocImportClient.StartAnswer>())!;
        (await VariablesRunsAsync()).Should().Be(before + 1);
        var queued = fixture.Queued.Should().ContainSingle(q => q.RunId == started.RunId).Subject;
        (queued.PipelineName, queued.Context![ContextKeys.PocId].ToString()).Should().Be((PipelinePresets.PocDeployName, poc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        queued.Context.Values.Select(v => v.ToString()).Should().NotContain(v => v!.Contains(Value));
        (await _client.ReadAsync(db => db.Set<StoredSecret>().AsNoTracking().CountAsync(s => s.Scope == SecretScope.Poc(poc.Id).Text))).Should().Be(1);
    }

    [Fact]
    public async Task Start_AVariableBreakingTheRule_IsRefused_WithNothingWritten()
    {
        var poc = await SeedAsync("bad-vars", "poc-ann");
        var upload = await _client.UploadIdAsync(Ann, PocZips.App());

        var response = await _client.StartAsync(Ann, upload, new
        {
            choice = "replace", pocId = poc.Id, confirmed = true, variables = new[] { new { name = "log_level", secret = false, value = "x" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("'log_level' is no PoC variable name");
        (await _client.ReadAsync(db => db.Set<PocRevision>().AsNoTracking().CountAsync(r => r.UploadId == upload))).Should().Be(0);
    }

    private Task<int> VariablesRunsAsync() => _client.ReadAsync(db =>
        db.Set<Run>().AsNoTracking().CountAsync(r => r.Pipeline == PipelinePresets.PocVariablesName && r.Project == "poc-ann"));

    private Task<Poc> SeedAsync(string name, string project) => _client.ReadAsync(async db =>
    {
        var poc = PocRows.Poc(name, project);
        (poc.Owner, poc.State) = ("ann-person", PocStates.Ready);
        return await PocRows.AddAsync(db, poc);
    });
}
