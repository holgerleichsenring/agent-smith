using System.Net;
using System.Net.Http.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocsAuthorityFixture;

namespace AgentSmith.Tests.Pocs.Registry;

/// <summary>
/// 2026-11-23-933de: POST /api/config/pocs/registry/check sits outside the PoCs switch — it answers with PoCs off — and
/// needs config.write with diagnostics.probe. The draft carries a problem, so no request leaves the server.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class PocRegistryCheckEndpointTests(PocsAuthorityFixture fixture) : IClassFixture<PocsAuthorityFixture>
{
    private const string Route = "/api/config/pocs/registry/check";

    [Fact]
    public async Task Check_PocsOff_AnswersTheReport()
    {
        fixture.Pocs.Enabled = false;
        try
        {
            var response = await fixture.Server.Client.SendAsync(Post(Admins));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var report = (await response.Content.ReadFromJsonAsync<DraftCheckReport>())!;
            report.Steps.Should().ContainSingle().Which.Detail.Should().Contain("pocs.targets[].registry.login_server");
        }
        finally
        {
            fixture.Pocs.Enabled = true;
        }
    }

    [Theory]
    [InlineData(Operators)]
    [InlineData(Ann)]
    public async Task Check_WithoutConfigWriteAndDiagnosticsProbe_Is403(string group) =>
        (await fixture.Server.Client.SendAsync(Post(group))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    private HttpRequestMessage Post(string group)
    {
        var request = fixture.As(group, Route);
        request.Method = HttpMethod.Post;
        request.Content = JsonContent.Create(new
        {
            type = "acr", loginServer = "registry.example.com", repository = "pocs", pullUser = "u", pullAuth = "s",
            pullMode = "pull_secret",
        });
        return request;
    }
}
