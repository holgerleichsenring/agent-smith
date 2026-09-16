using System.Text.RegularExpressions;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-9033: what a single IPlatformAdapter resolves to, and who no longer asks.
/// <para>
/// Both chat adapters register NON-KEYED, so a single-service resolve yields the LAST
/// registration — and classes that take one adapter were reaching Teams whatever they
/// meant. Two are Slack by name AND by purpose and now say so. The dispatcher is not one
/// of them: Teams resolves that same class, so a fixed adapter there answers Slack on
/// Teams' behalf and back again; it takes the platform per call instead. The rest are the
/// run-trigger chat path, and this pins what they get so a later registration cannot move
/// it unseen.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class DashboardChannelRegistrationTests
{
    [Fact]
    public void Registration_TheSlackNamedSites_ResolveTheSlackAdapter()
    {
        Type[] slackNamed =
        [
            typeof(SlackErrorActionHandler),
            typeof(SlackModalSubmissionHandler),
        ];

        foreach (var site in slackNamed)
        {
            var parameters = site.GetConstructors().Single().GetParameters()
                .Select(parameter => parameter.ParameterType).ToList();

            parameters.Should().Contain(typeof(SlackAdapter),
                $"{site.Name} is Slack by name and by purpose");
            parameters.Should().NotContain(typeof(IPlatformAdapter),
                $"{site.Name} asking for any adapter is what got it the Teams one");
        }
    }

    /// <summary>
    /// The class is named for Slack and serves every channel: SlackEventEndpointHandler and
    /// TeamsEndpoints both resolve it, and TeamsInteractionHandler takes it directly. A
    /// concrete adapter in its constructor is therefore a reply on the wrong channel — and
    /// nothing went red when one was put there, because no test covered the Teams path.
    /// </summary>
    [Fact]
    public void Registration_TheSharedDispatcher_TakesNoFixedAdapter()
    {
        var parameters = typeof(SlackMessageDispatcher).GetConstructors().Single()
            .GetParameters().Select(parameter => parameter.ParameterType).ToList();

        parameters.Should().NotContain(typeof(SlackAdapter));
        parameters.Should().NotContain(typeof(TeamsAdapter));
        parameters.Should().NotContain(typeof(IPlatformAdapter));
        parameters.Should().Contain(typeof(PlatformAdapters),
            "it is handed the platform on every call and answers on that one");
    }

    /// <summary>
    /// A SOURCE GUARD, and named as one. The dispatcher's collaborators are eight concrete
    /// classes deep, so calling DispatchAsync in a test is not affordable — which is exactly
    /// why the regression it guards went unnoticed: no test touches that method at all. What
    /// can be afforded is reading the two reply sites and insisting they pass the platform
    /// they were handed. Revert either to the Slack default and this goes red; that is the
    /// whole claim, and it is less than a behavioural test.
    /// </summary>
    [Fact]
    public void Dispatcher_BothReplySites_AnswerOnThePlatformTheyWereHanded()
    {
        var source = File.ReadAllText(Path.Combine(
            ArchitectureSources.BackendRoot,
            "AgentSmith.Server/Services/Adapters/SlackMessageDispatcher.cs"));

        var calls = Regex.Matches(source, @"adapters\.SendMessageAsync\(\s*");
        calls.Should().HaveCount(2, "the error intent and the catch-all error reply");
        foreach (Match call in calls)
            source[(call.Index + call.Length)..].Should().StartWith("platform,",
                "a fixed platform here answers Teams on Slack and Slack on Teams — the class "
                + "serves both, and DispatchAsync is handed the right one on every call");
    }

    [Fact]
    public async Task Reply_OnTheTeamsPlatform_ReachesTeamsAndNotSlack()
    {
        var slack = new RecordingAdapter(DispatcherDefaults.PlatformSlack);
        var teams = new RecordingAdapter(DispatcherDefaults.PlatformTeams);
        var adapters = new PlatformAdapters([slack, teams], NullLogger<PlatformAdapters>.Instance);

        await adapters.SendMessageAsync(
            DispatcherDefaults.PlatformTeams, "conversation", "it broke", default);

        teams.Sent.Should().Equal("it broke");
        slack.Sent.Should().BeEmpty("an error on Teams is not an error Slack should hear about");
    }

    [Fact]
    public async Task Reply_OnAPlatformWithNoAdapter_IsNotDeliveredAndSaysSo()
    {
        var slack = new RecordingAdapter(DispatcherDefaults.PlatformSlack);
        var adapters = new PlatformAdapters([slack], NullLogger<PlatformAdapters>.Instance);

        await adapters.SendMessageAsync("nowhere", "conversation", "it broke", default);

        slack.Sent.Should().BeEmpty("a channel with no adapter must not fall back to another");
    }

    [Fact]
    public void Registration_ABareResolve_YieldsTheDocumentedAdapter()
    {
        using var provider = Composed().BuildServiceProvider();

        provider.GetRequiredService<IPlatformAdapter>().Should().BeOfType<TeamsAdapter>(
            "the five single-adapter sites that remain are the run-trigger chat path, and "
            + "the last chat registration is what they have always got. The dashboard "
            + "adapter registers BEFORE them for exactly this reason");
    }

    [Fact]
    public void Registration_EveryAdapter_ComposesWithoutTheDashboardApi()
    {
        using var provider = Composed().BuildServiceProvider();

        // What SpecDialogMessenger does when it is built — and it is built whether or not
        // the env-gated dashboard API registered a hub context.
        var adapters = provider.GetServices<IPlatformAdapter>().ToList();

        adapters.Select(adapter => adapter.Platform).Should().BeEquivalentTo(
            [DispatcherDefaults.PlatformDashboard, DispatcherDefaults.PlatformSlack,
             DispatcherDefaults.PlatformTeams]);
    }

    /// <summary>
    /// The refusal itself is the shared route-permission machinery (p0503a/p0517), proven
    /// by its own tests. What this phase has to state is the DECLARATION, and which roles
    /// a dialog that files tickets is bundled into.
    /// </summary>
    [Fact]
    public void Ingest_WithoutThePermission_IsRefused()
    {
        var route = ServerRouteTable.Facts(app => app.MapDashboardApi())
            .Single(fact => fact.Pattern == "/api/spec-dialog/messages");

        route.Permissions.Should().Equal(Permissions.DialogWrite);
        BuiltInRoles.All[BuiltInRoles.Reader].Should().NotContain(Permissions.DialogWrite,
            "a reader sees what the agent did; filing tickets is not that");
        BuiltInRoles.All[BuiltInRoles.Operator].Should().Contain(Permissions.DialogWrite);
        HubMethodPermissions.For(nameof(AgentSmith.Server.Hubs.JobsHub.SubscribeSpecDialog))!
            .Names.Should().Equal(Permissions.DialogWrite);
    }

    /// <summary>Records what reached one platform, so a reply on the wrong one is visible.</summary>
    private sealed class RecordingAdapter(string platform) : IPlatformAdapter
    {
        public string Platform { get; } = platform;

        public List<string> Sent { get; } = [];

        public Task SendMessageAsync(string channelId, string text, CancellationToken ct)
        {
            Sent.Add(text);
            return Task.CompletedTask;
        }

        public Task SendProgressAsync(
            string channelId, int step, int total, string commandName, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<AgentSmith.Contracts.Dialogue.DialogAnswer?> AskTypedQuestionAsync(
            string channelId, AgentSmith.Contracts.Dialogue.DialogQuestion question,
            string? threadId, CancellationToken ct) => Task.FromResult<AgentSmith.Contracts.Dialogue.DialogAnswer?>(null);

        public Task SendInfoAsync(
            string channelId, string title, string text, string? threadId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SendDoneAsync(
            string channelId, string summary, string? prUrl, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SendErrorAsync(
            string channelId, AgentSmith.Server.Models.ErrorContext errorContext,
            CancellationToken ct) => Task.CompletedTask;

        public Task UpdateQuestionAnsweredAsync(
            string channelId, string messageId, string questionText, string answer,
            CancellationToken ct) => Task.CompletedTask;

        public Task SendDetailAsync(string channelId, string text, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SendClarificationAsync(
            string channelId, string suggestion, CancellationToken ct) => Task.CompletedTask;
    }

    private static IServiceCollection Composed()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        return services;
    }
}
