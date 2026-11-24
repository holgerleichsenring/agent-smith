using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: the upload and Start calls a test makes as one group's person, and the rows it reads
/// back.</summary>
internal sealed class PocImportClient(PocImportFixture fixture)
{
    public Task<HttpResponseMessage> UploadAsync(string group, byte[] zip, string fileName = "todo.zip", string? target = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", fileName } };
        if (target is not null) form.Add(new StringContent(target), "target");
        return SendAsync(group, HttpMethod.Post, "/api/pocs/uploads", form);
    }

    public async Task<string> UploadIdAsync(string group, byte[] zip, string fileName = "todo.zip")
    {
        var response = await UploadAsync(group, zip, fileName);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UploadAnswer>())!.UploadId;
    }

    public Task<HttpResponseMessage> StartAsync(string group, string uploadId, object body) =>
        SendAsync(group, HttpMethod.Post, $"/api/pocs/uploads/{uploadId}/import", JsonContent.Create(body));

    /// <summary>2026-11-23-933dg: any JSON call as that group's person (the variables routes).</summary>
    public Task<HttpResponseMessage> CallAsync(string group, HttpMethod method, string path, object? body = null) =>
        SendAsync(group, method, path, body is null ? null : JsonContent.Create(body));

    public async Task<T> ReadAsync<T>(Func<AgentSmithDbContext, Task<T>> read)
    {
        await using var scope = fixture.Server.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>());
    }

    private Task<HttpResponseMessage> SendAsync(string group, HttpMethod method, string path, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", fixture.Issuer.Token(PocsAuthorityFixture.Audience, extra: [new Claim("groups", group)], subject: $"{group}-person"));
        return fixture.Server.Client.SendAsync(request);
    }

    public sealed record UploadAnswer(string UploadId, int Port, int EntryCount, string ProposedName, string[] Checks, MatchAnswer? Match);

    public sealed record MatchAnswer(long PocId, string Verdict, bool PortChanged);

    public sealed record StartAnswer(long PocId, int Revision, string RunId);
}
