using System.Net;
using AgentSmith.Application;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-10-02-5f89a: over the real CLI composition, two GitLab connections on two instances with
/// two secrets each discover with their own token against their own host.
/// </summary>
public sealed class PerConnectionCredentialCompositionTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"two-gitlabs-{Guid.NewGuid():N}.yml");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public async Task TwoGitLabConnections_DiscoverWithTheirOwnSecrets()
    {
        File.WriteAllText(_path, """
            connections:
              first:
                type: gitlab
                group: team
                host: https://gitlab.one.example
                auth: gitlab_one
              second:
                type: gitlab
                group: team
                host: https://gitlab.two.example
                auth: gitlab_two
            secrets:
              gitlab_one: token-one
              gitlab_two: token-two
            """);
        var handler = new HeaderRecorder();
        using var provider = Compose(handler);
        var config = provider.GetRequiredService<AgentSmithConfig>();
        var gitlab = provider.GetServices<IRepoDiscoveryProvider>().Single(p => p.Type == RepoType.GitLab);

        await gitlab.DiscoverAsync(config.Connections["first"], CancellationToken.None);
        await gitlab.DiscoverAsync(config.Connections["second"], CancellationToken.None);

        handler.Seen.Should().Equal(
            ("gitlab.one.example", "token-one"),
            ("gitlab.two.example", "token-two"));
    }

    private ServiceProvider Compose(HeaderRecorder handler)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddInProcessSandbox();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
        services.AddSingleton(sp => sp.GetRequiredService<IConfigurationLoader>().LoadConfig(_path));
        var http = new Mock<IHttpClientFactory>();
        http.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, false));
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton(http.Object);
        return services.BuildServiceProvider();
    }

    private sealed class HeaderRecorder : HttpMessageHandler
    {
        public List<(string Host, string Token)> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request.RequestUri!.Host, request.Headers.GetValues("PRIVATE-TOKEN").Single()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        }
    }
}
