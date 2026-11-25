using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.Series;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Measurement;

/// <summary>
/// 2026-11-25-9992k: what a comparison of two models stands on — the per-model settings survive the studio and the
/// import, a question a run asks is counted once whatever the replay, and the series master's own asks are counted on
/// its record.
/// </summary>
public sealed class ModelComparisonRecordTests : IDisposable
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-25T10:00:00Z");
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    [Fact]
    public void Studio_ApiAndEffort_ReachTheRoleThroughItsEntry()
    {
        var agent = RawConfigPatch.Agent(Entity(new AgentCatalogModel("gpt-6-sol", "sol", Api: "responses", ReasoningEffort: "high")), null);

        agent.Catalog["m"].Api.Should().Be("responses");
        var primary = new ModelRoleChain(agent).For(TaskType.Primary);
        primary.Api.Should().Be("responses");
        primary.ReasoningEffort.Should().Be("high");
    }

    [Fact]
    public void Studio_InlineRolesThatDifferOnlyInApi_BecomeTwoEntries()
    {
        var agent = new AgentConfig
        {
            Type = "azure_openai",
            Models = new ModelRegistryConfig
            {
                Primary = new ModelAssignment { Model = "gpt-6-sol", Deployment = "d", Api = "responses", ReasoningEffort = "low" },
                Scout = new ModelAssignment { Model = "gpt-6-sol", Deployment = "d" },
            },
        };

        var (catalog, models) = AgentCatalogProjection.Of(agent);

        models["primary"].Should().NotBe(models["scout"]);
        catalog[models["primary"]].Api.Should().Be("responses");
        catalog[models["primary"]].ReasoningEffort.Should().Be("low");
        catalog[models["scout"]].Api.Should().BeNull();
    }

    [Fact]
    public void Import_ApiAndEffort_AreSettingsTheStoreKeeps()
    {
        var plan = new ConfigImportPlanner(
            new RawConfigYaml(), new ConfigDocumentAssembler(), new RawConfigTreeReader(), new StoredKeyDiff(),
            new RetiredConfigKeyDetector(new RawConfigTreeReader(), new ConfigKeyPathMatcher())).Plan("""
            agents:
              a:
                type: azure_openai
                catalog:
                  sol:
                    model: gpt-6-sol
                    api: responses
                    reasoning_effort: medium
                models:
                  primary:
                    use: sol
            """, "test.yml");

        plan.Dropped.Should().BeEmpty();
    }

    [Theory]
    [InlineData("anthropic", "responses", null)]
    [InlineData("ollama", null, "high")]
    public void Registry_ApiOrEffortOnAnotherProvider_IsRefusedByName(string type, string? api, string? effort)
    {
        var agent = new AgentConfig
        {
            Type = type,
            Models = new ModelRegistryConfig { Primary = new ModelAssignment { Model = "m", Api = api, ReasoningEffort = effort } },
        };

        var read = () => new AgentSmith.Infrastructure.Services.Providers.Agent.ConfigBasedModelRegistry(
            agent, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).GetModel(TaskType.Primary);

        read.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>().WithMessage($"*provider '{type}'*api or reasoning_effort*");
    }

    [Fact]
    public void Registry_ResponsesOnAzure_IsTheRoleAsWritten() =>
        new AgentSmith.Infrastructure.Services.Providers.Agent.ConfigBasedModelRegistry(
                new AgentConfig
                {
                    Type = "azure_openai",
                    Models = new ModelRegistryConfig { Primary = new ModelAssignment { Model = "m", Api = "responses", ReasoningEffort = "max" } },
                },
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
            .GetModel(TaskType.Primary).ReasoningEffort.Should().Be("max");

    [Fact]
    public void RunMetrics_CountsAskHumanCalls_AndNoOtherTool()
    {
        var metrics = RunMetrics.From([Call("ask_human"), Call("read_file"), Call("ask_human")]);

        metrics.QuestionsAsked.Should().Be(2);
        metrics.ToView().QuestionsAsked.Should().Be(2);
    }

    [Fact]
    public async Task QuestionFamily_FoldsAnAskOnce_AReplayOfItsPositionNotAgain()
    {
        await using (var ctx = Context())
        {
            ctx.Add(new Run { Id = "r1", Project = "p", TicketId = "7", Pipeline = "code", Status = "running", StartedAt = T });
            await ctx.SaveChangesAsync();
        }
        var applier = RunEventAppliers.Default();
        foreach (var (tool, seq) in new[] { ("ask_human", 5L), ("ask_human", 5L), ("read_file", 6L), ("ask_human", 7L) })
        {
            await using var uow = Context();
            await applier.ApplyAsync(uow, Call(tool), seq, CancellationToken.None);
        }

        await using var read = Context();
        var run = await read.Set<Run>().SingleAsync();
        RunStoryJson.TryDeserialize<RunMetrics>(run.RunMetricsJson)!.QuestionsAsked.Should().Be(2);
        (await read.Set<AgentSmith.Infrastructure.Persistence.Entities.RunEvent>().CountAsync()).Should().Be(2,
            "the ask's trail row is the fold's identity; another tool call is the trail writer's");
    }

    [Fact]
    public async Task SeriesAsk_CountsAStoredQuestion_NotOneRefusedWhileAnotherWaits()
    {
        var store = new InMemorySeriesRunStore();
        await store.OpenAsync(SeriesRecords.Running(), default);
        var beat = new SeriesBeat(store.Records["sr-1"], [], SeriesRecords.T);
        var ask = new SeriesAskTool(beat, store, 30);

        await ask.AskHumanAsync("Merge SCRUM-43 first?");
        var refused = await ask.AskHumanAsync("And SCRUM-44?");

        refused.Should().Contain("already waits");
        store.Records["sr-1"].MasterQuestionsAsked.Should().Be(1);
    }

    private static ToolCallEvent Call(string tool) => new("r1", tool, 2, T);

    private static AgentEntity Entity(AgentCatalogModel entry) =>
        new("agent", "azure_openai", null, null, null, null,
            new Dictionary<string, AgentCatalogModel> { ["m"] = entry },
            new Dictionary<string, string> { ["primary"] = "m" },
            null, null, null, null);

    private AgentSmithDbContext Context() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
