using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-11-18-78efb, end to end: GITHUB_WEBHOOK_SECRET stored in the admin area verifies a signed delivery; once its row
/// no longer opens, every GitHub delivery is refused and the log names the secret, never the value. GitLab, with no
/// value anywhere, still falls open.
/// </summary>
[Trait("TestProcess", "env-2")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class UnreadableWebhookSecretBootTests(SecretStoreAuthorityFixture fixture) : IClassFixture<SecretStoreAuthorityFixture>
{
    private const string Value = "78efb-webhook-value-0123456789";
    private const string Secret = "GITHUB_WEBHOOK_SECRET";
    private const string Body = """{"zen":"ping"}""";

    [Fact]
    public async Task AStoredWebhookSecret_ThatNoLongerOpens_RefusesEveryDelivery_AndNoValueAtAllStillFallsOpen()
    {
        (await SetAsync(Secret, Value)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GithubAsync(Sign(Body, Value))).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "the stored value verifies");

        await TamperAsync(Secret.ToLowerInvariant());
        (await SetAsync("other_78efb", "another-value-0123")).StatusCode.Should().Be(HttpStatusCode.OK, "a set reloads the store");

        (await GithubAsync(Sign(Body, Value))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        fixture.Logs.Entries.Should().Contain(e => e.Level == LogLevel.Error
            && e.Message.Contains(Secret) && e.Message.Contains("cannot be read"));
        fixture.Logs.Entries.Should().NotContain(e => e.Message.Contains(Value));
        using var gitlab = new HttpRequestMessage(HttpMethod.Post, "/webhook/gitlab") { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        gitlab.Headers.Add("X-Gitlab-Event", "Push Hook");
        (await fixture.Server.Client.SendAsync(gitlab)).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> GithubAsync(string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhook/github") { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-GitHub-Event", "ping");
        request.Headers.Add("X-Hub-Signature-256", signature);
        return await fixture.Server.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SetAsync(string name, string value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/config/secret-values/" + name) { Content = JsonContent.Create(new { value }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            fixture.Issuer.Token(AuthorityFixture.Audience, extra: [new Claim("roles", BuiltInRoles.Admin)]));
        return await fixture.Server.Client.SendAsync(request);
    }

    private async Task TamperAsync(string name)
    {
        await using var scope = fixture.Server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>();
        var row = await db.Set<StoredSecret>().SingleAsync(s => s.Name == name);
        row.Ciphertext = [.. row.Ciphertext.Select((b, i) => i == 0 ? (byte)(b ^ 0xFF) : b)];
        await db.SaveChangesAsync();
    }

    private static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
}
