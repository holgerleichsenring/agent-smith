using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-7c9bb through the REAL composition: the Jira tracker the server builds lists the tickets
/// changed since a cursor against an endpoint that answers like Cloud's search/jql, which refused every
/// first request of the sweep with HTTP 400 while it carried startAt.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class JiraCloudSweepTests
{
    [Fact]
    public async Task RealComposition_JiraCloudSearchJql_ListsTheChangedTickets()
    {
        var bodies = new List<string>();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            var http = new Mock<IHttpClientFactory>();
            http.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(JiraCloud.Handler(bodies), false));
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton(http.Object);
            // The harness stands a fixture in for the tracker; this test is about the production one.
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
            services.RemoveAll<ICredentialResolver>();
            services.AddSingleton(TestCredentials.With(("jira-token", "t")));
        });
        var tracker = new TrackerConnection
        {
            Name = "jira-cloud", Type = TrackerType.Jira, Url = "https://jira.example", Email = "e@example.invalid",
            Auth = "jira-token", Project = "A",
        };

        var lister = (IChangedTicketLister)harness.Services.GetRequiredService<ITicketProviderFactory>().Create(tracker);
        var page = await lister.ChangedSinceAsync(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero), 10, default);

        page.Items.Should().HaveCount(150);
        bodies.Should().NotContain(b => b.Contains("startAt"));
    }
}
