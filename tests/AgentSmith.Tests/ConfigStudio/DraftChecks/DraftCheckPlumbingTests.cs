using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Services.Providers.DraftChecks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace AgentSmith.Tests.ConfigStudio.DraftChecks;

/// <summary>2026-10-02-5f89b: the client, the answer and the budget the checks stand on.</summary>
public sealed class DraftCheckPlumbingTests
{
    [Fact]
    public void AddDraftChecks_TheNamedClient_DoesNotFollowRedirects()
    {
        using var provider = new ServiceCollection().AddDraftChecks().BuildServiceProvider();

        HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(DraftCheckHttp.ClientName);
        while (handler is DelegatingHandler delegating) handler = delegating.InnerHandler!;

        handler.Should().BeOfType<SocketsHttpHandler>().Which.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public async Task DraftCheckHttp_NotAnHttpAddress_AnswersWithoutSending()
    {
        var host = new ScriptedHostHandler();

        var answer = await host.Http().GetAsync("git.example.test/api", DraftCheckAuth.GitLab("t"), CancellationToken.None);

        answer.Failure.Should().Be("The host is not an http(s) address.");
        host.Requests.Should().BeEmpty();
    }

    [Fact]
    public void DraftCheckAnswer_SuccessThatIsNotJson_HasNoJsonAndIsNoIdentity()
    {
        var answer = DraftCheckAnswer.Answered("h", 200, "<html/>");

        answer.Json().Should().BeNull();
        answer.IdentityStep(_ => "x").Ok.Should().BeFalse();
        answer.HostStep().Ok.Should().BeTrue();
    }

    [Fact]
    public async Task DraftCheckCollector_BudgetRunsOut_EndsWithATimeStep()
    {
        var report = await new DraftCheckCollector().CollectAsync(
            DraftCheckStep.Pass(DraftCheckStep.Secret, "ok"), Endless, CancellationToken.None,
            budget: TimeSpan.FromMilliseconds(50));

        report.Steps[^1].Key.Should().Be(DraftCheckStep.Time);
        report.Ok.Should().BeFalse();
    }

    private static async IAsyncEnumerable<DraftCheckStep> Endless(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct);
        yield break;
    }
}
