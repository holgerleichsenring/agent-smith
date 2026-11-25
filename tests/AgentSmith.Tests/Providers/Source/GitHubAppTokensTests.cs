using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-9992h: a GitHub App machine identity's tokens, pinned against GitHub's documented exchange on faked answers —
/// an RS256 JWT (iss the app id, iat 60 s back, exp within ten minutes) sent as Bearer to
/// POST /app/installations/{id}/access_tokens, a 201 with token and expires_at, the token reused until five minutes before it
/// expires; the App's login {slug}[bot] from GET /app; a refusal named with GitHub's answer.
/// </summary>
public sealed class GitHubAppTokensTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly MachineIdentity App = new("agent-smith-bot", "1+agent-smith-bot[bot]@users.noreply.github.com", "GH_APP_KEY", "123456", "987");
    private static readonly RepoConnection Repo = new() { Name = "app", Type = RepoType.GitHub, Url = "https://github.com/example/app", Machine = App };
    private readonly RSA _key = RSA.Create(2048);
    private readonly SettableClock _clock = new() { Now = Now };
    private readonly Handler _github = new();

    private GitHubAppTokens Tokens() => new(new HttpClient(_github), _clock);

    private string Pem => _key.ExportPkcs8PrivateKeyPem();

    [Fact]
    public void InstallationToken_SendsTheSignedJwt_ToTheInstallationEndpoint_AndReadsTheToken()
    {
        _github.Answer(HttpStatusCode.Created, """{"token":"ghs_one","expires_at":"2026-11-25T13:00:00Z"}""");

        Tokens().InstallationToken(Repo, App, Pem).Should().Be("ghs_one");

        var sent = _github.Requests.Single();
        (sent.Method, sent.Url).Should().Be((HttpMethod.Post, "https://api.github.com/app/installations/987/access_tokens"));
        sent.Accept.Should().Be("application/vnd.github+json");
        sent.ApiVersion.Should().Be("2022-11-28");
        sent.UserAgent.Should().NotBeNullOrEmpty("GitHub refuses a request without a User-Agent");
        var (header, claims) = Verified(sent.Bearer!);
        header.GetProperty("alg").GetString().Should().Be("RS256");
        claims.GetProperty("iss").GetString().Should().Be("123456");
        claims.GetProperty("iat").GetInt64().Should().Be(Now.ToUnixTimeSeconds() - 60, "backdated against clock drift");
        (claims.GetProperty("exp").GetInt64() - Now.ToUnixTimeSeconds()).Should().BeInRange(1, 600, "at most ten minutes on");
    }

    [Fact]
    public void InstallationToken_IsReused_UntilFiveMinutesBeforeItExpires_ThenMintedAgain()
    {
        _github.Answer(HttpStatusCode.Created, """{"token":"ghs_one","expires_at":"2026-11-25T13:00:00Z"}""");
        _github.Answer(HttpStatusCode.Created, """{"token":"ghs_two","expires_at":"2026-11-25T14:00:00Z"}""");
        var tokens = Tokens();

        tokens.InstallationToken(Repo, App, Pem).Should().Be("ghs_one");
        _clock.Now += TimeSpan.FromMinutes(54);
        tokens.InstallationToken(Repo, App, Pem).Should().Be("ghs_one");
        _clock.Now += TimeSpan.FromMinutes(2);
        tokens.InstallationToken(Repo, App, Pem).Should().Be("ghs_two");
        _github.Requests.Should().HaveCount(2);
    }

    [Fact]
    public void InstallationToken_OnEnterprise_GoesToTheHostsApiV3()
    {
        _github.Answer(HttpStatusCode.Created, """{"token":"ghs_e","expires_at":"2026-11-25T13:00:00Z"}""");

        Tokens().InstallationToken(Repo with { Url = "https://ghe.example.com/example/app" }, App, Pem);

        _github.Requests.Single().Url.Should().Be("https://ghe.example.com/api/v3/app/installations/987/access_tokens");
    }

    [Fact]
    public void InstallationToken_Refused_IsAMachineCredentialException_WithGitHubsAnswer()
    {
        _github.Answer(HttpStatusCode.NotFound, """{"message":"Not Found"}""");

        var act = () => Tokens().InstallationToken(Repo, App, Pem);

        act.Should().Throw<MachineCredentialException>()
            .Which.Message.Should().Contain("GitHub answered 404").And.Contain("Not Found").And.Contain("GH_APP_KEY");
    }

    [Fact]
    public void InstallationToken_ABadKeyOrAnUnreadableAnswer_IsAMachineCredentialException()
    {
        var bad = () => Tokens().InstallationToken(Repo, App, "not a pem");
        bad.Should().Throw<MachineCredentialException>().Which.Message.Should().Contain("GH_APP_KEY");

        _github.Answer(HttpStatusCode.Created, "not json");
        var unreadable = () => Tokens().InstallationToken(Repo, App, Pem);
        unreadable.Should().Throw<MachineCredentialException>();
    }

    [Fact]
    public void AppLogin_IsTheSlugAsABot_ReadOnceFromGetApp()
    {
        _github.Answer(HttpStatusCode.OK, """{"id":1,"slug":"agent-smith-bot"}""");
        var tokens = Tokens();

        tokens.AppLogin(Repo, App, Pem).Should().Be("agent-smith-bot[bot]");
        tokens.AppLogin(Repo, App, Pem).Should().Be("agent-smith-bot[bot]");

        _github.Requests.Should().ContainSingle().Which.Should().Match<Sent>(s => s.Method == HttpMethod.Get && s.Url == "https://api.github.com/app");
    }

    // The JWT's header and claims, after checking its signature against the App's public key.
    private (JsonElement Header, JsonElement Claims) Verified(string jwt)
    {
        var parts = jwt.Split('.');
        _key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue("the JWT is signed RS256 with the App's private key");
        return (JsonDocument.Parse(Base64Url(parts[0])).RootElement, JsonDocument.Parse(Base64Url(parts[1])).RootElement);
    }

    private static byte[] Base64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }

    private sealed record Sent(HttpMethod Method, string Url, string? Bearer, string? Accept, string? ApiVersion, string? UserAgent);

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _answers = new();
        public List<Sent> Requests { get; } = [];

        public void Answer(HttpStatusCode status, string body) => _answers.Enqueue((status, body));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Sent(request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.Parameter,
                request.Headers.Accept.SingleOrDefault()?.MediaType,
                request.Headers.TryGetValues("X-GitHub-Api-Version", out var v) ? v.Single() : null, request.Headers.UserAgent.ToString()));
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            var (status, body) = _answers.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }
}
