using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.DraftChecks;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Registry;

/// <summary>
/// 2026-11-23-933de: the registry Test action — the draft's own problems and a pull_auth outside the secrets catalog
/// stop it before any request, even when a server environment variable of that name holds a value; a sound draft
/// answers the probe sentence.
/// </summary>
public sealed class PocRegistryDraftCheckTests
{
    private readonly FakeRegistryHost _host = new();

    [Fact]
    public async Task PocRegistryDraftCheck_PullAuthNamesEnvironmentVariableNotInCatalog_SendsNoRequest()
    {
        var draft = PocsConfigTests.SoundRegistry();
        draft.PullAuth = "SERVER_ONLY_VARIABLE";

        var report = await Check(catalog: []).RunAsync(draft, CancellationToken.None);

        report.Steps.Should().ContainSingle().Which.Should().Match<DraftCheckStep>(s => s.Key == DraftCheckStep.Secret && !s.Ok);
        report.Steps[0].Detail.Should().Contain("pocs.targets[].registry.pull_auth names 'SERVER_ONLY_VARIABLE'");
        _host.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PocRegistryDraftCheck_ProblemInDraft_RefusedBeforeFirstRequest()
    {
        var draft = PocsConfigTests.SoundRegistry();
        draft.LoginServer = "collector.example.net";

        var report = await Check().RunAsync(draft, CancellationToken.None);

        report.Steps.Should().ContainSingle().Which.Key.Should().Be(DraftCheckStep.Values);
        report.Steps[0].Detail.Should().Contain("pocs.targets[].registry.login_server is 'collector.example.net'");
        _host.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PocRegistryDraftCheck_UnreadableStoredToken_SaysWhy()
    {
        var report = await Check(catalog: new() { ["poc_pull_token"] = "" }, unreadable: "the master key changed")
            .RunAsync(PocsConfigTests.SoundRegistry(), CancellationToken.None);

        report.Steps.Should().ContainSingle().Which.Detail.Should().Contain("the master key changed");
        _host.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PocRegistryDraftCheck_SoundDraft_AnswersTheProbe_AndTheMissingScope()
    {
        var reached = await Check().RunAsync(PocsConfigTests.SoundRegistry(), CancellationToken.None);
        reached.Ok.Should().BeTrue();
        reached.Steps[^1].Detail.Should().Be("The pull token reaches pocregistry.azurecr.io/pocs.");

        _host.ScopeMap = "other/";
        var missing = await Check().RunAsync(PocsConfigTests.SoundRegistry(), CancellationToken.None);
        missing.Steps[^1].Should().Match<DraftCheckStep>(s => s.Key == DraftCheckStep.Scope && !s.Ok);
    }

    private PocRegistryDraftCheck Check(Dictionary<string, string>? catalog = null, string? unreadable = null)
    {
        var secrets = new LoadedSecretValues(new AgentSmithConfig
        {
            Secrets = catalog ?? new() { ["poc_pull_token"] = FakeRegistryHost.PullToken },
            UnreadableSecrets = unreadable is null ? new Dictionary<string, string>() : new() { ["poc_pull_token"] = unreadable },
        });
        // A PoC run's resolver would find SERVER_ONLY_VARIABLE in the environment; the draft check never asks it.
        var resolver = new CredentialResolver(secrets, name => name == "SERVER_ONLY_VARIABLE" ? FakeRegistryHost.PullToken : null);
        var factory = new ContainerRegistryFactory(resolver, new Clients(_host), new CapturingLogger<ContainerRegistryFactory>());
        return new PocRegistryDraftCheck(secrets, factory, new DraftCheckCollector());
    }

    private sealed class Clients(FakeRegistryHost host) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => host.Client();
    }
}
