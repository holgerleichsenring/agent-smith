using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-09-86e1: the files inside an upload through the BOOTED server — listed, previewed,
/// served without ever running as a page, compared by hash, and removed one at a time.
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class ReferenceFileRouteTests : IDisposable
{
    private const string Dialog = "d-86e1";
    private const string Upload = $"/api/spec-dialog/references?dialogId={Dialog}&project=sample";

    private const string Config = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        repos:
          sample-repo:
            type: github
            url: https://github.com/sample/repo
            auth: token
        trackers:
          sample-ado:
            type: azure_devops
            organization: sample-org
            project: SampleProject
            auth: token
        projects:
          sample:
            agent: claude-default
            tracker: sample-ado
            repos: [sample-repo]
            pipeline: code
        """;

    private readonly List<string> _tempFiles = [];

    [Fact]
    public async Task FileRoutes_UploadedFolder_ListPreviewServeHashAndRemoveOne()
    {
        await using var server = await BootAsync();
        var setId = await UploadAsync(server, ("site/index.html", "<script>alert(1)</script>"), ("site/app.py", "print(1)"));
        var files = $"/api/spec-dialog/references/{setId}/files?dialogId={Dialog}";

        (await server.Client.GetStringAsync(files)).Should().Contain("\"path\":\"site/app.py\"").And.Contain("\"bytes\":8");
        (await server.Client.GetStringAsync($"{Files(setId)}/preview?dialogId={Dialog}&path=site/app.py"))
            .Should().Contain("\"kind\":\"text\"").And.Contain("\"text\":\"print(1)\"");
        var served = await server.Client.GetAsync($"{Files(setId)}/content?dialogId={Dialog}&path=site/index.html");
        served.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream", "an upload never runs as a page");
        served.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        (await server.Client.GetStringAsync($"/api/spec-dialog/references/hashes?dialogId={Dialog}"))
            .Should().Contain($"\"setId\":\"{setId}\"").And.Contain("\"name\":\"site\"");

        var removed = await server.Client.DeleteAsync($"{files}&path=site/app.py");

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent, await removed.Content.ReadAsStringAsync());
        (await server.Client.GetStringAsync(files)).Should().NotContain("app.py");
    }

    [Fact]
    public async Task FileContent_OtherConversation_NotFound()
    {
        await using var server = await BootAsync();
        var setId = await UploadAsync(server, ("brief.md", "# brief"));

        var stranger = await server.Client.GetAsync($"{Files(setId)}/content?dialogId=d-other&path=brief.md");
        var missing = await server.Client.GetAsync($"{Files(setId)}/content?dialogId={Dialog}&path=nope.md");

        stranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_SingleFile_IsNamedByTheFileAndARepeatIsRefused()
    {
        await using var server = await BootAsync();

        var first = await server.Client.PostAsync(Upload, Pick(("brief.md", "# brief")));
        var again = await server.Client.PostAsync(Upload, Pick(("notes/brief-copy.md", "# brief")));

        (await first.Content.ReadAsStringAsync()).Should().Contain("\"name\":\"brief.md\"");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("'brief.md'");
    }

    private static string Files(string setId) => $"/api/spec-dialog/references/{setId}/files";

    private static async Task<string> UploadAsync(BootedServer server, params (string Path, string Text)[] files)
    {
        var answer = await server.Client.PostAsync(Upload, Pick(files));
        var body = await answer.Content.ReadAsStringAsync();
        answer.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("setId").GetString()!;
    }

    private static MultipartFormDataContent Pick(params (string Path, string Text)[] files)
    {
        var form = new MultipartFormDataContent("----WebKitFormBoundary86e1");
        foreach (var (path, text) in files)
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
            part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"file\"", FileName = $"\"{path}\"" };
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part);
        }
        return form;
    }

    private async Task<BootedServer> BootAsync()
    {
        var server = await BootedServer.StartAsync(new BootPlan(NewConfig()));
        (await server.Client.PostAsync("/api/config/import", new StringContent(Config, Encoding.UTF8, "text/yaml"))).EnsureSuccessStatusCode();
        return server;
    }

    private string NewConfig()
    {
        var db = Temp("db");
        _tempFiles.Add(db + "-wal");
        _tempFiles.Add(db + "-shm");
        MigratedStoreTemplate.CopyToFile(db);
        var path = Temp("yml");
        File.WriteAllText(path, $"persistence:\n  provider: sqlite\n  connection_string: Data Source={db}\n");
        return path;
    }

    private string Temp(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-86e1-{Guid.NewGuid():N}.{extension}");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }
}
