using System.Security.Claims;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Browser;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-13-8762r: a conversation's mocks as the operator reaches them — listed on the dialog view
/// without bytes, the screenshot by (spec, version), and the whole page only as a sandboxed download.
/// </summary>
public sealed class DialogMockRouteTests : IDisposable
{
    private const string Dialog = "d-8762r";
    private const string Owner = "person-a";
    private const string Project = "sample";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogOwnership _ownership;
    private readonly MemoryMocks _mocks = new();

    public DialogMockRouteTests()
    {
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(_repository, BoundSessionNudges.Quiet(_repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        _ownership = new SpecDialogOwnership(_repository);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SpecDialogView_CarriesMocksWithShotIds()
    {
        var session = await OpenAsync();
        var (first, _) = await _mocks.AddAsync(session, "spec-a", "<p>1</p>", default);
        await _mocks.SetShotAsync(session, first!.SetId, [0xFF, 0xD8], default);
        await _mocks.AddAsync(session, "spec-a", "<p>2</p>", default);

        var view = (await Reader(_mocks).ReadAsync(Dialog, default)).Session!;

        view.Mocks.Should().Equal(
            new SpecDialogMockView("spec-a", 1, "spec-a-mock-v1", DateTimeOffset.UnixEpoch, true),
            new SpecDialogMockView("spec-a", 2, "spec-a-mock-v2", DateTimeOffset.UnixEpoch, false));
        (await Reader(new MemoryMocks()).ReadAsync(Dialog, default)).Session!.Mocks.Should().BeNull("null with none");
    }

    [Fact]
    public async Task MockShot_ServesTheJpeg_NeverTheHtml_ReachChecked()
    {
        var session = await OpenAsync();
        var (version, _) = await _mocks.AddAsync(session, "spec-a", "<script>alert(1)</script>", default);
        await _mocks.SetShotAsync(session, version!.SetId, [0xFF, 0xD8, 0x07], default);
        var http = Http(Owner);

        var served = await SpecDialogMockEndpoints.ShotAsync(http, session, "spec-a", 1, _ownership, _mocks, default);
        var stranger = await SpecDialogMockEndpoints.ShotAsync(Http("person-b"), session, "spec-a", 1, _ownership, _mocks, default);

        var file = served.Should().BeOfType<FileContentHttpResult>().Subject;
        file.ContentType.Should().Be("image/jpeg");
        file.FileContents.ToArray().Should().Equal(0xFF, 0xD8, 0x07);
        http.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        stranger.Should().BeOfType<NotFound>("a conversation the caller cannot reach is not found");
    }

    [Fact]
    public async Task MockShot_VersionWithoutScreenshot_Is404()
    {
        var session = await OpenAsync();
        await _mocks.AddAsync(session, "spec-a", "<p/>", default);

        (await SpecDialogMockEndpoints.ShotAsync(Http(Owner), session, "spec-a", 1, _ownership, _mocks, default)).Should().BeOfType<NotFound>();
        (await SpecDialogMockEndpoints.ShotAsync(Http(Owner), session, "spec-a", 2, _ownership, _mocks, default)).Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task MockHtml_ServedAsSandboxedAttachment_NeverInline()
    {
        var session = await OpenAsync();
        await _mocks.AddAsync(session, "spec-a", "<script>alert(1)</script>", default);
        var http = Http(Owner);

        var served = await SpecDialogMockEndpoints.HtmlAsync(http, session, "spec-a", 1, _ownership, _mocks, default);
        var stranger = await SpecDialogMockEndpoints.HtmlAsync(Http("person-b"), session, "spec-a", 1, _ownership, _mocks, default);

        var file = served.Should().BeOfType<FileContentHttpResult>().Subject;
        file.ContentType.Should().Be("application/octet-stream", "the model's page never runs as the dashboard");
        file.FileDownloadName.Should().Be("spec-a-mock-v1.html");
        http.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        http.Response.Headers.ContentSecurityPolicy.ToString().Should().Be("sandbox");
        stranger.Should().BeOfType<NotFound>();
    }

    private SpecDialogViewReader Reader(IConversationMockReader mocks) =>
        new(_sessions, new SpecDialogProjectCatalog(Loader()),
            new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)),
            new SpecDialogLatestOutcomeStore(_repository, BoundSessionNudges.Quiet(_repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance),
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
            new SpecDialogTurnGate(TimeProvider.System), new SpecDialogTicketTextRepository(_context), TestUploads.Over(_context), mocks);

    private async Task<string> OpenAsync() =>
        (await _sessions.OpenAsync("dashboard", Dialog, Dialog, Owner,
            new ActiveScope { Project = Project, Repos = ["repo-a"] }, CancellationToken.None)).JobId;

    private static DefaultHttpContext Http(string subject) =>
        new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "test")) };

    private static IConfigurationLoader Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                [Project] = new() { Name = Project, Repos = [new RepoConnection { Name = "repo-a" }] },
            },
        });
        return loader.Object;
    }
}
