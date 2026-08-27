using System.Text.Json;
using AgentSmith.Server.Services.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-27-7098: the answer a failure nobody planned for gets. Before this, ASP.NET's
/// default 500 carried an EMPTY body, and a client that prefers a reason from the body had
/// none to prefer — which is the whole reason a start reported itself as a status code.
/// </summary>
public sealed class UnhandledFailureAnswerTests
{
    [Fact]
    public async Task Start_AnUnexpectedFailure_StillCarriesAReason()
    {
        var context = NewContext();

        var handled = await NewAnswer().TryHandleAsync(
            context, new InvalidOperationException("the job queue is not reachable"),
            CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        (await ReasonOf(context)).Should().Contain("the job queue is not reachable");
    }

    [Fact]
    public async Task TryHandleAsync_AnExceptionWithNoMessage_StillNamesItsType()
    {
        var context = NewContext();

        await NewAnswer().TryHandleAsync(context, new NoMessageException(), CancellationToken.None);

        (await ReasonOf(context)).Should().Contain(nameof(NoMessageException));
    }

    [Fact]
    public async Task TryHandleAsync_AResponseAlreadyOnTheWire_DeclinesInsteadOfThrowing()
    {
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(new StartedResponse());

        var handled = await NewAnswer().TryHandleAsync(
            new DefaultHttpContext(features), new InvalidOperationException("too late"),
            CancellationToken.None);

        handled.Should().BeFalse("a response already sent cannot be replaced by one with a reason");
    }

    [Fact]
    public async Task TryHandleAsync_TheCallerHungUp_DeclinesInsteadOfThrowing()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var handled = await NewAnswer().TryHandleAsync(
            NewContext(), new InvalidOperationException("nobody is listening"), aborted.Token);

        handled.Should().BeFalse();
    }

    [Fact]
    public void Reason_AnOrdinaryException_NamesTheTypeAndTheMessage()
    {
        UnhandledFailureAnswer.Reason(new InvalidOperationException("no capacity"))
            .Should().Be("InvalidOperationException: no capacity");
    }

    private static UnhandledFailureAnswer NewAnswer() =>
        new(NullLogger<UnhandledFailureAnswer>.Instance);

    // WriteAsJsonAsync resolves its options from the request services, and a bare
    // DefaultHttpContext has none.
    private static DefaultHttpContext NewContext() => new()
    {
        RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        Response = { Body = new MemoryStream() },
    };

    private static async Task<string> ReasonOf(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("reason").GetString()!;
    }

    private sealed class StartedResponse : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class NoMessageException : Exception
    {
        public override string Message => string.Empty;
    }
}
