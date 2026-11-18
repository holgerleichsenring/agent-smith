using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-11-17-f3a8d, end to end: an admin sets two secrets to the same value through the secure route. What is stored
/// differs and holds no plaintext; no response, log entry or run event carries the value; the configuration resolves
/// it; and a reader or an operator is refused.
/// </summary>
[Trait("TestProcess", "env-2")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SecretValueEndpointTests(SecretStoreAuthorityFixture fixture) : IClassFixture<SecretStoreAuthorityFixture>
{
    private const string Value = "f3a8d-value-0123456789";
    private const string Route = "/api/config/secret-values/";

    [Fact]
    public async Task SetTwoEqualValues_StoredApartEncrypted_NeverEchoedLoggedOrRecorded()
    {
        var first = await SendAsync(Admin(), HttpMethod.Put, Route + "f3a8d_one", new { value = Value });
        var second = await SendAsync(Admin(), HttpMethod.Put, Route + "f3a8d_two", new { value = Value });
        var state = await SendAsync(Admin(), HttpMethod.Get, Route.TrimEnd('/'));

        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var answered = await first.Content.ReadAsStringAsync() + await second.Content.ReadAsStringAsync() + await state.Content.ReadAsStringAsync();
        answered.Should().NotContain(Value).And.Contain("f3a8d_one");
        var rows = await ReadAsync(db => db.Set<StoredSecret>().Where(s => s.Name.StartsWith("f3a8d_")).ToListAsync());
        rows.Should().HaveCount(2);
        rows[0].Ciphertext.Should().NotEqual(rows[1].Ciphertext);
        rows.Should().OnlyContain(r => !Encoding.UTF8.GetString(r.Ciphertext).Contains(Value));
        var events = await ReadAsync(db => db.RunEvents.Select(e => e.PayloadJson).ToListAsync());
        events.Should().NotContain(p => p != null && p.Contains(Value));
        fixture.Logs.Entries.Should().NotContain(e => e.Message.Contains(Value));
        fixture.Server.Services.GetRequiredService<ISecretValues>().Resolve("f3a8d_one").Should().Be(Value);
    }

    [Fact]
    public async Task ReaderOrOperator_IsRefused()
    {
        (await SendAsync(Token(BuiltInRoles.Reader), HttpMethod.Put, Route + "f3a8d_x", new { value = Value }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(Token(BuiltInRoles.Operator), HttpMethod.Get, Route.TrimEnd('/')))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AShortValue_IsRefusedNamingWhy()
    {
        var response = await SendAsync(Admin(), HttpMethod.Put, Route + "f3a8d_short", new { value = "abc" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("at least 6 characters");
    }

    private string Admin() => Token(BuiltInRoles.Admin);

    private string Token(string role) => fixture.Issuer.Token(AuthorityFixture.Audience, extra: [new Claim("roles", role)]);

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Server.Client.SendAsync(request);
    }

    private async Task<T> ReadAsync<T>(Func<AgentSmithDbContext, Task<T>> read)
    {
        await using var scope = fixture.Server.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>());
    }
}
