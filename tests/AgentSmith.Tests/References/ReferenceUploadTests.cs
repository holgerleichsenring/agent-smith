using System.Security.Claims;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.References;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.References.ReferenceUploadRequests;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283db: a website dropped into a design conversation, through the route, over the
/// REAL durable store — stored as one set whole, or refused whole naming the file and the limit.
/// </summary>
public sealed class ReferenceUploadTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Dialog = "d-283db";
    private const string Owner = "person-a";
    private const string Project = "sample";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogOwnership _ownership;

    public ReferenceUploadTests()
    {
        _context = MigratedStoreTemplate.Context(_connection);
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
        _ownership = new SpecDialogOwnership(_repository);
    }

    [Fact]
    public async Task ReferenceUpload_MacFolderWithDsStoreAndMacosx_IsStoredWithoutThem()
    {
        var result = await UploadAsync(
            ("site/index.html", Text("<h1>")), ("site/css/site.css", Text("h1{color:#c0ffee}")),
            ("site/.DS_Store", Text("x")), ("__MACOSX/site/._index.html", Text("x")));

        var set = result.Should().BeOfType<Ok<ReferenceSetView>>().Subject.Value!;
        set.Name.Should().Be("site");
        set.Files.Should().Be(2);
        var stored = await StoredAsync();
        stored.Select(f => f.RelativePath).Should().BeEquivalentTo("site/index.html", "site/css/site.css");
        stored.Should().OnlyContain(f => f.Kind == ReferenceFileKind.Site && f.SetId == set.SetId);
        stored.Single(f => f.RelativePath.EndsWith(".css")).MediaType.Should().Be("text/css");
    }

    [Fact]
    public async Task ReferenceUpload_ATwentyMegabyteFolder_IsStoredAsOneSet()
    {
        var files = Enumerable.Range(0, 4)
            .Select(i => ($"site/img/{i}.png", new byte[ReferenceUploadLimits.MaxFileBytes])).ToArray();

        var set = (await UploadAsync(files)).Should().BeOfType<Ok<ReferenceSetView>>().Subject.Value!;

        set.Bytes.Should().Be(4 * ReferenceUploadLimits.MaxFileBytes);
        (await StoredAsync()).Select(f => f.SetId).Distinct().Should().Equal(set.SetId);
    }

    [Fact]
    public async Task ReferenceUpload_PathWithDotDot_IsRefusedNamingTheFile()
    {
        var result = await UploadAsync(("site/index.html", Text("<h1>")), ("site/../../etc/passwd.txt", Text("x")));

        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);
        result.Should().BeOfType<BadRequest<string>>().Which.Value.Should().Contain("'site/../../etc/passwd.txt'");
        (await StoredAsync()).Should().BeEmpty("a set is stored whole or not at all");
    }

    [Fact]
    public async Task ReferenceUpload_FaviconIcoAvifSvg_AreAccepted()
    {
        var result = await UploadAsync(
            ("site/favicon.ico", [0, 0, 1, 0]), ("site/hero.avif", Text("avif")), ("site/logo.svg", Text("<svg/>")));

        StatusOf(result).Should().Be(StatusCodes.Status200OK, "the magic-byte check is the dialog image's, not a site's");
        (await StoredAsync()).Select(f => f.MediaType).Should().BeEquivalentTo("image/x-icon", "image/avif", "image/svg+xml");
    }

    [Fact]
    public async Task ReferenceUpload_DisallowedExtension_RefusesTheWholeSet()
    {
        var result = await UploadAsync(("site/index.html", Text("<h1>")), ("site/deploy.sh", Text("rm")));

        result.Should().BeOfType<BadRequest<string>>().Which.Value.Should().Contain("'site/deploy.sh'");
        (await StoredAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ReferenceUpload_DeclaredBodyOver26Megabytes_IsRefusedUnread()
    {
        var body = new UnreadableStream();
        var http = new DefaultHttpContext();
        http.Request.Body = body;
        http.Request.ContentLength = ReferenceUploadLimits.RouteBodyBytes + 1;
        http.Request.ContentType = "multipart/form-data; boundary=x";

        var result = await UploadAsync(http);

        StatusOf(result).Should().Be(StatusCodes.Status413PayloadTooLarge);
        body.WasRead.Should().BeFalse("a declared length over the ceiling is refused without reading");
    }

    [Fact]
    public async Task ReferenceUpload_SomeoneElsesConversation_Is409()
    {
        await OpenAsync("person-b");

        var result = await UploadAsync(("site/index.html", Text("<h1>")));

        StatusOf(result).Should().Be(StatusCodes.Status409Conflict);
        (await StoredAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ReferenceUpload_OneZip_IsUnpackedIntoOneSet()
    {
        var zip = Zip(("index.html", Text("<h1>")), ("css/a.css", Text("a{}")));

        var set = (await UploadAsync(("landing.zip", zip))).Should().BeOfType<Ok<ReferenceSetView>>().Subject.Value!;

        set.Name.Should().Be("landing");
        (await StoredAsync()).Select(f => f.RelativePath).Should().BeEquivalentTo("landing/index.html", "landing/css/a.css");
    }

    [Fact]
    public async Task ReferenceUpload_AFourthSet_IsRefusedNamingTheLimit()
    {
        for (var i = 0; i < ReferenceUploadLimits.MaxSetsPerConversation; i++)
            StatusOf(await UploadAsync(($"s{i}/index.html", Text("<h1>")))).Should().Be(StatusCodes.Status200OK);

        var fourth = await UploadAsync(("s3/index.html", Text("<h1>")));

        fourth.Should().BeOfType<BadRequest<string>>().Which.Value.Should().Contain("3 websites");
    }

    [Fact]
    public async Task ReferenceList_TheConversationsSets_AreListedForItsOwner()
    {
        await UploadAsync(("site/index.html", Text("<h1>")), ("site/a.css", Text("a{}")));

        var listed = await SpecDialogReferenceEndpoints.ListAsync(
            Dialog, Principal(Owner), _ownership, _repository, new ReferenceSetRepository(_context), CancellationToken.None);
        var theirs = await SpecDialogReferenceEndpoints.ListAsync(
            Dialog, Principal("person-b"), _ownership, _repository, new ReferenceSetRepository(_context), CancellationToken.None);

        listed.Should().BeOfType<Ok<List<ReferenceSetView>>>().Which.Value!.Should().ContainSingle()
            .Which.Files.Should().Be(2);
        StatusOf(theirs).Should().Be(StatusCodes.Status403Forbidden);
    }

    private async Task<IResult> UploadAsync(params (string Path, byte[] Bytes)[] files) =>
        await UploadAsync(await MultipartAsync(files));

    private async Task<IResult> UploadAsync(HttpContext http)
    {
        http.User = Principal(Owner);
        var paths = new ReferencePathRule();
        var ignore = new ReferenceIgnoreList();
        var upload = new ReferenceSetUpload(
            new ReferenceZipReader(paths, ignore, new ZipEntryChecksum()),
            new ReferenceSetValidator(paths, ignore, new ReferenceFileTypes()), new ReferenceFileTypes(),
            new SpecDialogConversationResolver(_sessions, _ownership, Commands()),
            new ReferenceSetRepository(_context));
        return await SpecDialogReferenceEndpoints.UploadAsync(
            http, Dialog, Project, new ReferenceUploadBody(NullLogger<ReferenceUploadBody>.Instance), upload,
            CancellationToken.None);
    }

    private SpecDialogCommandHandler Commands() =>
        new(_sessions, new SpecDialogScopeResolver(Loader()), new SpecDialogReplyComposer(),
            new SpecDialogMessenger(
                [new DashboardAdapter(NullLogger<DashboardAdapter>.Instance, new AgentSmith.Tests.SpecDialog.RecordingDialogHub())],
                NullLogger<SpecDialogMessenger>.Instance));

    private async Task OpenAsync(string owner) =>
        await _sessions.OpenAsync(Platform, Dialog, Dialog, owner,
            new ActiveScope { Project = Project, Repos = ["repo-a"] }, CancellationToken.None);

    private async Task<IReadOnlyList<ReferenceFile>> StoredAsync()
    {
        _context.ChangeTracker.Clear();
        return await _context.Set<ReferenceFile>().AsNoTracking().ToListAsync();
    }

    private static int StatusOf(IResult result) =>
        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode
        ?? throw new InvalidOperationException("the route answered without a status code");

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));

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

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public bool WasRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count) { WasRead = true; return 0; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return ValueTask.FromResult(0);
        }
    }
}
