using System.Net;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Argo;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Argo;

/// <summary>2026-11-23-c81ec: the Argo CD API client against recorded JSON over a faked HTTP host.</summary>
public sealed class ArgoCdClientTests
{
    private const string Application = """
        {"metadata":{"name":"poc-todo"},"status":{"sync":{"status":"Synced","revision":"abc"},
         "health":{"status":"Degraded","message":"Deployment app exceeded its progress deadline"},
         "operationState":{"phase":"Failed","message":"one or more objects failed to apply"},
         "summary":{"images":["pocregistry.azurecr.io/pocs/todo:old@sha256:aa"]}}}
        """;

    private const string Resource = """
        {"manifest":"{\"metadata\":{\"name\":\"app\",\"generation\":4},\"spec\":{\"replicas\":1,\"template\":{\"metadata\":{\"annotations\":{\"agent-smith.io/variables-checksum\":\"sha256:c1\"}},\"spec\":{\"containers\":[{\"name\":\"sidecar\",\"image\":\"x\"},{\"name\":\"app\",\"image\":\"pocregistry.azurecr.io/pocs/todo:abc@sha256:bb\"}]}}},\"status\":{\"observedGeneration\":3,\"readyReplicas\":1,\"updatedReplicas\":1}}"}
        """;

    private readonly FakeArgoHost _host = new();
    private readonly string _token = FakeArgoHost.Jwt(null);

    [Fact]
    public async Task GetApplication_ParsesSyncHealthOperation()
    {
        _host.Answers["/api/v1/applications/poc-todo"] = (HttpStatusCode.OK, Application);

        var state = await Client().GetApplicationAsync("poc-todo", default);

        state.Should().Be(new ArgoApplicationState("Synced", "Degraded", "Deployment app exceeded its progress deadline", "Failed",
            "one or more objects failed to apply"));
        state!.OperationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetApplication_NotFound_AnswersNull() =>
        (await Client().GetApplicationAsync("poc-gone", default)).Should().BeNull();

    [Fact]
    public async Task GetLiveDeployment_SendsGroupVersionKind_ReadsTemplateImageChecksumGenerationsReplicas()
    {
        _host.Answers["/api/v1/applications/poc-todo/resource"] = (HttpStatusCode.OK, Resource);

        var live = await Client().GetLiveDeploymentAsync("poc-todo", "poc-todo", "app", default);

        live.Should().Be(new ArgoLiveDeployment("pocregistry.azurecr.io/pocs/todo:abc@sha256:bb", "sha256:c1", 1, 4, 3, 1, 1));
        _host.Requests.Single().Url.Query.Should().Contain("namespace=poc-todo").And.Contain("resourceName=app")
            .And.Contain("group=apps").And.Contain("version=v1").And.Contain("kind=Deployment");
    }

    [Fact]
    public async Task Refresh_AsksForANormalRefresh()
    {
        _host.Answers["/api/v1/applications/poc-todo"] = (HttpStatusCode.OK, Application);

        await Client().RefreshAsync("poc-todo", default);

        _host.Requests.Single().Url.Query.Should().Be("?refresh=normal");
    }

    [Fact]
    public async Task UpsertApplicationSet_SendsUpsertTrue_WithTheBearerToken()
    {
        _host.Answers["/api/v1/applicationsets"] = (HttpStatusCode.OK, "{}");

        await Client().UpsertApplicationSetAsync(new JsonObject { ["kind"] = "ApplicationSet" }, default);

        var request = _host.Requests.Single();
        (request.Method, request.Url.Query).Should().Be(("POST", "?upsert=true"));
        request.Auth.Should().Be($"Bearer {_token}");
        request.Body.Should().Contain("\"kind\":\"ApplicationSet\"");
    }

    [Fact]
    public async Task ARefusal_NamesUrlPathAndStatus_NeverTheToken_AndAFiveHundredIsTransient()
    {
        _host.Answers["/api/v1/applications/poc-todo"] = (HttpStatusCode.Forbidden, "permission denied " + _token);
        _host.Answers["/api/v1/applications/poc-busy"] = (HttpStatusCode.BadGateway, "");
        var logs = new ListLogger();

        var refused = await FluentActions.Awaiting(() => Client(logs).GetApplicationAsync("poc-todo", default)).Should().ThrowAsync<ArgoCdException>();
        var busy = await FluentActions.Awaiting(() => Client(logs).GetApplicationAsync("poc-busy", default)).Should().ThrowAsync<ArgoCdException>();

        refused.Which.Message.Should().Contain("https://argocd.poc.example.internal").And.Contain("api/v1/applications/poc-todo")
            .And.Contain("403").And.NotContain(_token);
        refused.Which.IsTransient.Should().BeFalse();
        busy.Which.IsTransient.Should().BeTrue();
        logs.Lines.Should().NotContain(l => l.Contains(_token));
    }

    [Fact]
    public void TheToken_IsACatalogValue_SoTheTraceMaskerReplacesIt()
    {
        var secrets = Mock.Of<ISecretValues>(s => s.All() == new[] { _token });

        new SecretMasker(secrets).Apply($"Authorization: Bearer {_token}").Should().NotContain(_token);
    }

    [Fact]
    public async Task Probe_TakesTheToken_AndWarnsUnderTokenWarnDays()
    {
        _host.Answers["/api/version"] = (HttpStatusCode.OK, """{"Version":"v2.13.0"}""");
        _host.Answers["/api/v1/session/userinfo"] = (HttpStatusCode.OK, """{"loggedIn":true,"username":"proj:pocs:agent-smith"}""");
        var soon = FakeArgoHost.Jwt(DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds());

        var probe = await Client(token: soon).ProbeAsync(default);

        probe.Reached.Should().BeTrue();
        probe.Sentence.Should().Contain("takes the token").And.Contain("under token_warn_days (14)");
    }

    [Fact]
    public async Task Probe_NotLoggedIn_IsNotReached()
    {
        _host.Answers["/api/version"] = (HttpStatusCode.OK, "{}");
        _host.Answers["/api/v1/session/userinfo"] = (HttpStatusCode.OK, """{"loggedIn":false}""");

        (await Client().ProbeAsync(default)).Reached.Should().BeFalse();
    }

    [Fact]
    public void Factory_AMissingTokenSecret_IsRefusedNamingTheKey()
    {
        var credentials = Mock.Of<ICredentialResolver>(c => c.Lookup("poc_argo_token") == CredentialLookup.None);
        var factory = new ArgoCdClientFactory(credentials, Mock.Of<IHttpClientFactory>(), TimeProvider.System, NullLogger<ArgoCdClientFactory>.Instance);

        FluentActions.Invoking(() => factory.Create(PocsConfigTests.SoundArgo())).Should().Throw<ArgoCdException>()
            .WithMessage("*pocs.targets[].argo.token_secret names 'poc_argo_token'*");
    }

    private IArgoCdClient Client(ILogger? logger = null, string? token = null)
    {
        var used = token ?? _token;
        return new ArgoCdClient(new ArgoCdHttp(_host.Client(), new Uri("https://argocd.poc.example.internal/"), used, logger ?? NullLogger.Instance),
            PocsConfigTests.SoundArgo(), used, TimeProvider.System);
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception) + exception?.Message);
    }
}
