using System.Security.Claims;
using System.Text;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: an operator shows the design partner what they are looking at, over the
/// REAL durable store (SQLite in-memory with the shipped migrations).
/// <para>
/// The load-bearing cases are the ones an upload gets wrong by copying the message route. That
/// route authorises through a check that passes when no open session is found — safe for a
/// watch, unsafe for a WRITE — and it dispatches after answering, which would store a row
/// against a conversation that may not exist yet. An upload resolves its session inside the
/// request, OPENS one when none is open (the page's own opening post is an unordered race it
/// must not lose to), and refuses only a conversation that exists and is somebody else's.
/// </para>
/// </summary>
public sealed class DialogImageUploadTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Dialog = "d-3af8";
    private const string Owner = "person-a";
    private const string Intruder = "person-b";
    private const string Project = "sample";

    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogAttachmentRepository _attachments;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogOwnership _ownership;
    private readonly RecordingDialogHub _hub = new();

    public DialogImageUploadTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
        _attachments = new SpecDialogAttachmentRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
        _ownership = new SpecDialogOwnership(_repository);
    }

    [Fact]
    public async Task Dialog_AMessageWithAnImage_StoresItAgainstTheSession()
    {
        var session = await OpenAsync();

        var result = await UploadAsync(Png);

        StatusOf(result).Should().Be(StatusCodes.Status200OK);
        var stored = await StoredAsync();
        stored.Should().ContainSingle();
        stored[0].SessionId.Should().Be(session, "a dialog id is a tab; the session is the conversation");
        stored[0].MediaType.Should().Be("image/png");
        Convert.FromBase64String(stored[0].ContentBase64).Should().Equal(Png);
    }

    [Fact]
    public async Task Dialog_AnUploadWithNoOpenSession_OpensOneRatherThanLosingTheImage()
    {
        var result = await UploadAsync(Png);

        StatusOf(result).Should().Be(StatusCodes.Status200OK,
            "the page opens its conversation with an unordered second post, and a refusal "
            + "here would lose exactly the opening screenshot");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        opened.Should().NotBeNull();
        (await StoredAsync())[0].SessionId.Should().Be(opened!.JobId);
    }

    /// <summary>
    /// The verb matters. Opening over an existing session IS the fork, so an upload that lost
    /// the race to the page's own open command must not open a second one on top of it.
    /// </summary>
    [Fact]
    public async Task Dialog_AnUploadOntoAnAlreadyOpenSession_DoesNotForkIt()
    {
        var session = await OpenAsync();
        await _sessions.AppendTurnAsync(
            Platform, Dialog, TranscriptRole.User, "the settings pane", null, null, CancellationToken.None);

        await UploadAsync(Png);

        var still = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        still!.JobId.Should().Be(session, "the operator's conversation is still the open one");
        still.Transcript.Should().ContainSingle("the upload closed nothing and started nothing");
    }

    [Fact]
    public async Task Dialog_AnUploadToAnotherPrincipalsConversation_IsRefusedDistinguishably()
    {
        await OpenAsync();

        var result = await UploadAsync(Png, caller: Intruder);

        StatusOf(result).Should().Be(StatusCodes.Status409Conflict,
            "an upload is to a conversation the caller is in, so the page must be able to "
            + "tell this from a permission it does not hold and recover");
        StatusOf(result).Should().NotBe(StatusCodes.Status403Forbidden,
            "403 is what the authorization layer answers for a missing permission — a route "
            + "answering it itself would pass its permission test with the requirement deleted");
        (await StoredAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Dialog_AnImageOverTheCap_IsRefusedBeforeTheBodyIsBound()
    {
        await OpenAsync();
        var http = Request(new ThrowingBody(), declaredLength: SpecDialogImageBody.Bytes + 1);

        var result = await UploadAsync(http);

        StatusOf(result).Should().Be(StatusCodes.Status413PayloadTooLarge);
        (await StoredAsync()).Should().BeEmpty(
            "the refusal acted on the DECLARED length — the body was never read, and the "
            + "stream would have thrown if it had been");
    }

    [Fact]
    public async Task Dialog_AnUndeclaredBodyOverTheCap_IsRefusedWhileItIsRead()
    {
        await OpenAsync();
        var oversize = new byte[SpecDialogImageBody.Bytes + 1];
        Png.CopyTo(oversize, 0);

        var result = await UploadAsync(Request(new MemoryStream(oversize), declaredLength: null));

        StatusOf(result).Should().Be(StatusCodes.Status413PayloadTooLarge);
        (await StoredAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Dialog_AKindTheBytesDoNotSupport_IsRefused()
    {
        await OpenAsync();

        var result = await UploadAsync(Encoding.UTF8.GetBytes("%PDF-1.7 not an image at all"));

        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest,
            "the declared type is attacker-supplied here, so the kind is read from the bytes");
        (await StoredAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Dialog_AResume_KeepsTheConversationsImages()
    {
        var session = await OpenAsync();
        await UploadAsync(Png);

        var resumed = await new SpecDialogResumer(
                _repository, new SpecDialogTurnGate(TimeProvider.System),
                new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)),
                TimeProvider.System, NullLogger<SpecDialogResumer>.Instance)
            .ResumeAsync(session, Owner, Platform, "d-second-tab", "d-second-tab", CancellationToken.None);

        resumed.Should().BeOfType<SpecDialogResumed>();
        var view = await ReadAsync("d-second-tab");
        view!.Images.Should().ContainSingle(
            "the rows are keyed on the SESSION, so a conversation moved to a new tab keeps them");
    }

    [Fact]
    public async Task Dialog_DeletingTheConversation_SweepsItsImages()
    {
        var session = await OpenAsync();
        await UploadAsync(Png);
        var elsewhere = await StoreAgainstAsync("another-conversation");

        await SpecDialogDeletionEndpoints.DeleteAsync(
            session, Principal(Owner), _ownership, new SpecDialogTurnGate(TimeProvider.System),
            new SpecDialogConversationDeleter(_context, _repository, new DialogueAnswerRepository(
                _context, new AgentSmith.Infrastructure.Persistence.Services.Translators.SqliteUniqueViolationTranslator()),
                _attachments),
            AgentSmith.Tests.Sandbox.Holds.None(), CancellationToken.None);

        (await StoredAsync()).Select(row => row.Id).Should().Equal([elsewhere],
            "the conversation's images go with it in the transaction it already opens, and "
            + "nothing else does");
    }

    [Fact]
    public async Task Dialog_AStoredImage_IsServedBackToItsOwnerAndToNobodyElse()
    {
        await OpenAsync();
        await UploadAsync(Png);
        var id = (await StoredAsync())[0].Id;

        var mine = await SpecDialogImageEndpoints.ServeAsync(
            id, Principal(Owner), _ownership, _attachments, CancellationToken.None);
        var theirs = await SpecDialogImageEndpoints.ServeAsync(
            id, Principal(Intruder), _ownership, _attachments, CancellationToken.None);
        var missing = await SpecDialogImageEndpoints.ServeAsync(
            id + 500, Principal(Owner), _ownership, _attachments, CancellationToken.None);

        mine.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>()
            .Which.FileContents.ToArray().Should().Equal(Png);
        StatusOf(theirs).Should().Be(StatusCodes.Status404NotFound);
        StatusOf(theirs).Should().Be(StatusOf(missing),
            "an id is a number a caller can guess, so 'not yours' and 'not there' answer alike");
    }

    [Fact]
    public async Task Dialog_TheTranscriptRead_AddressesTheImagesRatherThanCarryingThem()
    {
        await OpenAsync();
        await UploadAsync(Png);

        var view = await ReadAsync(Dialog);

        view!.Images.Should().ContainSingle();
        view.Images[0].MediaType.Should().Be("image/png");
        view.Images[0].Id.Should().Be((await StoredAsync())[0].Id,
            "this read is issued after every message, so the bytes are fetched once, not per reply");
    }

    private async Task<SpecDialogSessionView?> ReadAsync(string dialogId) =>
        (await new SpecDialogViewReader(
                _sessions, new SpecDialogProjectCatalog(Loader()),
                new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)),
                new SpecDialogLatestOutcomeStore(
                    _repository, NullLogger<SpecDialogLatestOutcomeStore>.Instance),
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                new SpecDialogTurnGate(TimeProvider.System), _attachments)
            .ReadAsync(dialogId, CancellationToken.None)).Session;

    private async Task<long> StoreAgainstAsync(string sessionId) =>
        (await _attachments.AddAsync(
            new SpecDialogAttachment
            {
                SessionId = sessionId, MediaType = "image/png", ContentBase64 = Convert.ToBase64String(Png),
            },
            CancellationToken.None)).Id;

    private Task<IResult> UploadAsync(byte[] body, string caller = Owner) =>
        UploadAsync(Request(new MemoryStream(body), body.LongLength), caller);

    private async Task<IResult> UploadAsync(HttpContext http, string caller = Owner)
    {
        http.User = Principal(caller);
        return await SpecDialogImageEndpoints.UploadAsync(
            http, Dialog, Project,
            new SpecDialogImageBody(NullLogger<SpecDialogImageBody>.Instance),
            new ImageKindFromBytes(),
            new SpecDialogConversationResolver(_sessions, _ownership, Commands()),
            _attachments, CancellationToken.None);
    }

    /// <summary>The real command handler, which is what must do the opening: its own guard
    /// returns WITHOUT opening when a session is already open on the thread.</summary>
    private SpecDialogCommandHandler Commands() =>
        new(_sessions,
            new SpecDialogScopeResolver(Loader()),
            new SpecDialogReplyComposer(),
            new SpecDialogMessenger(
                [new DashboardAdapter(NullLogger<DashboardAdapter>.Instance, _hub)],
                NullLogger<SpecDialogMessenger>.Instance));

    private static HttpContext Request(Stream body, long? declaredLength)
    {
        var http = new DefaultHttpContext();
        http.Request.Body = body;
        http.Request.ContentLength = declaredLength;
        return http;
    }

    private async Task<string> OpenAsync(string owner = Owner) =>
        (await _sessions.OpenAsync(
            Platform, Dialog, Dialog, owner,
            new ActiveScope { Project = Project, Repos = ["repo-a"] }, CancellationToken.None)).JobId;

    private async Task<IReadOnlyList<SpecDialogAttachment>> StoredAsync()
    {
        _context.ChangeTracker.Clear(); // a bulk delete is not tracked; read what the store holds
        return await _context.Set<SpecDialogAttachment>().AsNoTracking().OrderBy(a => a.Id).ToListAsync();
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

    /// <summary>A body that proves it was never read: reading it at all fails the test.</summary>
    private sealed class ThrowingBody : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("the body was read after the size refusal");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
