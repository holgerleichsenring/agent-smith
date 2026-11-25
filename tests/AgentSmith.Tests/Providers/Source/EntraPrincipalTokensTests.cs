using System.Net;
using System.Text;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-d0e2: a service principal's Entra tokens, pinned against Microsoft's documented client credentials grant on faked
/// answers — POST {authority}/{tenant}/oauth2/v2.0/token with grant_type, client_id, client_secret and the Azure DevOps scope;
/// a 200 with access_token and expires_in; the token reused for half its lifetime; a refusal named with Entra's
/// error_description.
/// </summary>
public sealed class EntraPrincipalTokensTests
{
    private static readonly MachineIdentity Principal = new("agent-smith-bot", "bot@example.com", "SP_SECRET", TenantId: "tenant-1", ClientId: "client-1");
    private static readonly MachineTarget Target = new("Repo 'app' (machine identity 'agent-smith-bot')", "https://dev.azure.com/org/p/_git/app");
    private readonly SettableClock _clock = new() { Now = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero) };
    private readonly Handler _entra = new();

    private EntraPrincipalTokens Tokens() => new(new HttpClient(_entra), _clock);

    [Fact]
    public void Token_PostsTheClientCredentialsGrant_ToTheTenantsTokenEndpoint_AndReadsTheAccessToken()
    {
        _entra.Answer(HttpStatusCode.OK, """{"token_type":"Bearer","expires_in":3599,"access_token":"eyJa.eyJb.sig"}""");

        Tokens().Token(Target, Principal, "s3cret").Should().Be("eyJa.eyJb.sig");

        var (url, form) = _entra.Requests.Single();
        url.Should().Be("https://login.microsoftonline.com/tenant-1/oauth2/v2.0/token");
        form.Should().Contain("grant_type=client_credentials").And.Contain("client_id=client-1").And.Contain("client_secret=s3cret")
            .And.Contain("scope=" + Uri.EscapeDataString("499b84ac-1321-427f-aa17-267ca6975798/.default"));
    }

    [Fact]
    public void Token_IsReused_ForHalfItsLifetime()
    {
        _entra.Answer(HttpStatusCode.OK, """{"expires_in":3600,"access_token":"eyJ1.a.b"}""");
        _entra.Answer(HttpStatusCode.OK, """{"expires_in":3600,"access_token":"eyJ2.a.b"}""");
        var tokens = Tokens();

        tokens.Token(Target, Principal, "s3cret").Should().Be("eyJ1.a.b");
        _clock.Now = _clock.Now.AddMinutes(29);
        tokens.Token(Target, Principal, "s3cret").Should().Be("eyJ1.a.b");
        _clock.Now = _clock.Now.AddMinutes(2);
        tokens.Token(Target, Principal, "s3cret").Should().Be("eyJ2.a.b");
        _entra.Requests.Should().HaveCount(2);
    }

    [Fact]
    public void Token_Refused_IsAMachineCredentialException_NamingEntrasDescription()
    {
        _entra.Answer(HttpStatusCode.Unauthorized, """{"error":"invalid_client","error_description":"AADSTS7000215: Invalid client secret provided."}""");

        var act = () => Tokens().Token(Target, Principal, "wrong");

        act.Should().Throw<MachineCredentialException>().Which.Message.Should()
            .Contain("Entra answered 401").And.Contain("AADSTS7000215").And.Contain("SP_SECRET").And.Contain("Repo 'app'");
    }

    [Fact]
    public void Token_AnUnreadableAnswer_IsAMachineCredentialException()
    {
        _entra.Answer(HttpStatusCode.OK, "not json");

        var act = () => Tokens().Token(Target, Principal, "s3cret");

        act.Should().Throw<MachineCredentialException>();
    }

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _answers = new();
        public List<(string Url, string Form)> Requests { get; } = [];

        public void Answer(HttpStatusCode status, string body) => _answers.Enqueue((status, body));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Method.Should().Be(HttpMethod.Post);
            Requests.Add((request.RequestUri!.ToString(), request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult()));
            var (status, body) = _answers.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }
}
