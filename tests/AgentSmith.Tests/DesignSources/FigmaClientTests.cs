using System.Net;
using AgentSmith.Contracts.Models.Design;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Design;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ab: the Figma client against a fake handler built from the documented REST
/// shapes — where the token goes, how a rate limit is met, and what a failure carries.
/// </summary>
public sealed class FigmaClientTests
{
    [Fact]
    public async Task FigmaClient_SendsTokenHeaderOnlyToApiHost()
    {
        var handler = FakeFigmaHandler.Answering();
        var client = FigmaFakes.Client(handler);

        var nodes = await client.GetNodesAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", 3, null, CancellationToken.None);
        var variables = await client.GetLocalVariablesAsync(FigmaFakes.SecretName, "AbCdEf123456", CancellationToken.None);

        nodes.Body.Should().NotBeNull();
        variables.Body.Should().NotBeNull();
        handler.Requests.Should().HaveCount(2).And.OnlyContain(r => r.RequestUri!.Host == "api.figma.com");
        handler.Requests.Should().OnlyContain(r => r.Headers.GetValues(FigmaClient.TokenHeader).Single() == FigmaFakes.Token);
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/v1/files/AbCdEf123456/nodes?ids=1%3A2&depth=3");
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be("/v1/files/AbCdEf123456/variables/local");
    }

    [Fact]
    public async Task FigmaClient_SecretWithoutValue_SendsNothing()
    {
        var handler = FakeFigmaHandler.Answering();

        var read = await FigmaFakes.Client(handler, token: null)
            .GetNodesAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", 3, null, CancellationToken.None);

        read.Failure!.Kind.Should().Be(FigmaReadFailureKind.Forbidden);
        read.Failure.Detail.Should().Contain(FigmaFakes.SecretName);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task FigmaClient_429ShortRetryAfter_RetriesOnceThenSucceeds()
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.RateLimited(2));
        handler.NodeResponses.Enqueue(() => FigmaFakes.Json(FigmaFakes.Nodes));
        var clock = new InstantClock();

        var read = await FigmaFakes.Client(handler, clock)
            .GetNodesAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", 3, null, CancellationToken.None);

        read.Body.Should().NotBeNull();
        handler.Requests.Should().HaveCount(2);
        clock.Waits.Should().ContainSingle().Which.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task FigmaClient_429Twice_ReportsRateLimitedWithWait()
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.RateLimited(1));
        handler.NodeResponses.Enqueue(() => FigmaFakes.RateLimited(20));

        var read = await FigmaFakes.Client(handler, new InstantClock())
            .GetNodesAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", 3, null, CancellationToken.None);

        handler.Requests.Should().HaveCount(2, "one retry, bounded");
        read.Failure!.Kind.Should().Be(FigmaReadFailureKind.RateLimited);
        read.Failure.RetryAfter.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task FigmaClient_429LongRetryAfter_IsReportedWithoutWaiting()
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.RateLimited(120));
        var clock = new InstantClock();

        var read = await FigmaFakes.Client(handler, clock)
            .GetNodesAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", 3, null, CancellationToken.None);

        read.Failure!.RetryAfter.Should().Be(TimeSpan.FromSeconds(120));
        clock.Waits.Should().BeEmpty("a long wait is the model's decision");
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, FigmaReadFailureKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, FigmaReadFailureKind.NotFound)]
    [InlineData(HttpStatusCode.BadGateway, FigmaReadFailureKind.Unknown)]
    public async Task FigmaClient_FailureStatus_MapsToKindWithoutBody(HttpStatusCode status, FigmaReadFailureKind kind)
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.Status(status));

        var read = await FigmaFakes.Client(handler)
            .GetNodesAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", 3, null, CancellationToken.None);

        read.Failure!.Kind.Should().Be(kind);
        read.Failure.Detail.Should().NotContain("FIGMA-BODY-MARKER");
    }

    [Fact]
    public async Task FigmaClient_ConnectionFails_ReportsUnreachableWithoutTheMessage()
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => throw new HttpRequestException("socket " + FigmaFakes.Token));

        var read = await FigmaFakes.Client(handler)
            .GetNodesAsync(FigmaFakes.SecretName, "AbCdEf123456", "1:2", 3, null, CancellationToken.None);

        read.Failure!.Kind.Should().Be(FigmaReadFailureKind.Unreachable);
        read.Failure.Detail.Should().NotContain(FigmaFakes.Token);
    }

    [Fact]
    public void AddDesignProviders_ResolvesFigmaClientOnTheApiHost()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<AgentSmith.Contracts.Services.ISecretValues>(
                new AgentSmith.Contracts.Services.LoadedSecretValues(
                    AgentSmith.Contracts.Models.Configuration.AgentSmithConfig.Empty()));
        services.AddDesignProviders();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IFigmaClient>().Should().BeOfType<FigmaClient>();
    }
}
