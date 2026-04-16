using System.Net;
using AgentSmith.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AgentSmith.Tests.Handlers;

public sealed class HttpProbeRunnerTests
{
    [Fact]
    public async Task ProbeAsync_WithBearerToken_SendsAuthenticatedRequest()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"data\": \"test\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var runner = new HttpProbeRunner(httpClient, NullLogger<HttpProbeRunner>.Instance);

        var result = await runner.ProbeAsync(
            "user1", "GET", "https://api.example.com/users",
            "my-token", null, null, CancellationToken.None);

        result.StatusCode.Should().Be(200);
        result.Persona.Should().Be("user1");
        result.Method.Should().Be("GET");
        result.ResponseBody.Should().Contain("test");

        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Headers.Authorization!.Scheme == "Bearer" &&
                r.Headers.Authorization.Parameter == "my-token"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ProbeAsync_On401_TriggersReAuth()
    {
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                      { Content = new StringContent("ok") };
            });

        var httpClient = new HttpClient(handler.Object);
        var runner = new HttpProbeRunner(httpClient, NullLogger<HttpProbeRunner>.Instance);

        var result = await runner.ProbeAsync(
            "user1", "GET", "https://api.example.com/data",
            "old-token", null,
            _ => Task.FromResult<string?>("new-token"),
            CancellationToken.None);

        result.StatusCode.Should().Be(200);
        callCount.Should().Be(2);
    }
}
