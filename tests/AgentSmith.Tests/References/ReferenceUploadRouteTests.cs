using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AgentSmith.Server.Services.References;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-02-0d72: the website upload through the BOOTED server — the dashboard's exact query
/// string, a multipart body shaped like a browser folder pick, the real binding and the real store.
/// A folder holding one file outside the extension list answered 400 with its reason in the body;
/// the page showed "HTTP 400" and the server wrote nothing. Such files are now skipped and named,
/// and a refusal that remains is logged once.
/// </summary>
[Trait("TestProcess", "env-2")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ReferenceUploadRouteTests : IDisposable
{
    private const string Route = "/api/spec-dialog/references?dialogId=d-0d72-new&project=sample";

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
    private readonly RecordingLoggerProvider _log = new();

    [Fact]
    public async Task ReferenceUploadRoute_BrowserSourceFolder_AnswersCredentialFilesAndLeftOut()
    {
        await using var server = await BootAsync();

        var answer = await server.Client.PostAsync(Route, FolderPick("site/index.html", "site/form/app.py",
            "site/.env", "site/.gitignore", "site/.venv/lib/x.py", "site/.DS_Store"));

        answer.StatusCode.Should().Be(HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
        var body = await answer.Content.ReadAsStringAsync();
        body.Should().Contain("\"files\":4").And.Contain("\"leftOutCount\":1")
            .And.Contain("\"path\":\"site/.venv/\"").And.Contain("\"credentialFiles\":[\"site/.env\"]");
        body.Should().NotContain(".DS_Store", "what the ignore list drops stays silent");
    }

    [Fact]
    public async Task ReferenceUploadRoute_BrowserFolderOfRebuildableFilesOnly_Answers400NamingThemAndLogsAWarning()
    {
        await using var server = await BootAsync();

        var answer = await server.Client.PostAsync(Route, FolderPick("site/node_modules/a.js", "site/.git/HEAD"));

        answer.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await answer.Content.ReadAsStringAsync()).Should().Contain("'site/.git/'");
        _log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
                && e.Category == typeof(ReferenceSetUpload).FullName,
                "a refusal is the line an operator looks for")
            .Which.Message.Should().Contain("d-0d72-new").And.Contain("'site/.git/'");
    }

    [Fact]
    public async Task ReferenceUploadRoute_BrowserFolderOfWebsiteFiles_IsStoredOnANewConversation()
    {
        await using var server = await BootAsync();

        var answer = await server.Client.PostAsync(Route, FolderPick("site/index.html", "site/css/site.css"));
        var listed = await server.Client.GetAsync(Route);

        answer.StatusCode.Should().Be(HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
        listed.StatusCode.Should().Be(HttpStatusCode.OK, "the list binds with the page's project on the query");
        (await listed.Content.ReadAsStringAsync()).Should().Contain("\"files\":2");
    }

    private async Task<BootedServer> BootAsync()
    {
        var server = await BootedServer.StartAsync(new BootPlan(NewConfig())
        {
            Services = services => services.AddSingleton<ILoggerProvider>(_log),
        });
        var imported = await server.Client.PostAsync(
            "/api/config/import", new StringContent(Config, Encoding.UTF8, "text/yaml"));
        imported.EnsureSuccessStatusCode();
        return server;
    }

    /// <summary>What a browser posts for a folder pick: one "file" part per file, its relative path as
    /// the quoted file name, and the part's own media type.</summary>
    private static MultipartFormDataContent FolderPick(params string[] paths)
    {
        var form = new MultipartFormDataContent("----WebKitFormBoundary0d72");
        foreach (var path in paths)
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes($"content of {path}"));
            part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"file\"", FileName = $"\"{path}\"",
            };
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part);
        }
        return form;
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
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-0d72-{Guid.NewGuid():N}.{extension}");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }
}
