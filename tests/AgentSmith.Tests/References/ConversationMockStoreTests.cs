using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-13-8762r: a conversation's mocks over the real store (SQLite, shipped migrations), through
/// the two ports AddReferenceFiles registers — numbered by rank at read, capped, invisible to every
/// upload read, and swept by the conversation's delete.
/// </summary>
public sealed class ConversationMockStoreTests : IDisposable
{
    private const string Session = "s-8762r";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;

    public ConversationMockStoreTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddScoped(_ => Db());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        services.AddReferenceFiles();
        _provider = services.BuildServiceProvider();
    }

    private IConversationMocks Writer => _provider.GetRequiredService<IConversationMocks>();
    private IConversationMockReader Reader => _provider.GetRequiredService<IConversationMockReader>();

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ConversationMocks_Add_NumbersVersionsPerSpec()
    {
        var a1 = await AddAsync("spec-a");
        var b1 = await AddAsync("spec-b");
        var a2 = await AddAsync("spec-a");

        (a1.Version, b1.Version, a2.Version).Should().Be((1, 1, 2));
        a2.Name.Should().Be("spec-a-mock-v2");
        Writer.Should().BeSameAs(Reader, "one store serves both ports");
    }

    [Fact]
    public async Task ConversationMocks_TwoWritesSameSpec_GetDistinctNumbers()
    {
        var both = await Task.WhenAll(Writer.AddAsync(Session, "spec-a", "<p>1</p>", default), Writer.AddAsync(Session, "spec-a", "<p>2</p>", default));

        both.Select(r => r.Version!.Version).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task ConversationMocks_OverCaps_Refused()
    {
        var (tooLong, refusal) = await Writer.AddAsync(Session, "spec-a", new string('x', IConversationMocks.MaxChars + 1), default);
        tooLong.Should().BeNull();
        refusal.Should().Contain("90000 characters per mock");

        for (var i = 0; i < IConversationMocks.MaxVersionsPerSpec; i++) await AddAsync("spec-a");
        var (over, perSpec) = await Writer.AddAsync(Session, "spec-a", "<p/>", default);

        over.Should().BeNull();
        perSpec.Should().Contain("20 versions, the cap per spec");
    }

    [Fact]
    public async Task ConversationMocks_OverConversationCap_Refused()
    {
        for (var i = 0; i < IConversationMocks.MaxVersionsPerConversation; i++) await AddAsync($"spec-{i}");

        var (over, refusal) = await Writer.AddAsync(Session, "spec-new", "<p/>", default);

        over.Should().BeNull();
        refusal.Should().Contain("100 mock versions, the cap per conversation");
    }

    [Fact]
    public async Task ConversationMocks_VersionNumberAndName_DerivedAtRead_NothingStoresTheNumber()
    {
        var first = await AddAsync("spec-a");
        await AddAsync("spec-a");
        await using (var db = Db())
        {
            (await db.Set<ReferenceFile>().Select(f => f.RelativePath).ToListAsync()).Should().AllBe("spec-a.html");
            await db.Set<ReferenceFile>().Where(f => f.SetId == first.SetId).ExecuteDeleteAsync();
        }

        var listed = await Reader.ListAsync(Session, default);

        listed.Should().ContainSingle().Which.Should().Match<MockVersion>(v => v.Version == 1 && v.Name == "spec-a-mock-v1",
            "the number is the rank of what is held, so the one left is the first");
    }

    [Fact]
    public async Task ConversationMocks_List_LoadsNoContent()
    {
        var version = await AddAsync("spec-a");
        await Writer.SetShotAsync(Session, version.SetId, [0xFF, 0xD8], default);
        var commands = new List<string>();
        await using var db = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection)
            .LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information).Options);

        var listed = await new ConversationMockReadRepository(db).ListAsync(Session, default);

        listed.Should().ContainSingle().Which.HasShot.Should().BeTrue();
        commands.Should().NotBeEmpty().And.OnlyContain(c => !c.Contains("\"Content\""), "the per-message view never loads bytes");
    }

    [Fact]
    public async Task ConversationMocks_SetShot_AddressesTheSet_NotAVersionNumber()
    {
        var a1 = await AddAsync("spec-a");
        var b1 = await AddAsync("spec-b");

        (await Writer.SetShotAsync(Session, b1.SetId, [0xFF, 0xD8, 0x01], default)).Should().BeTrue();

        (await Reader.ShotAsync(Session, "spec-b", 1, default)).Should().Equal(0xFF, 0xD8, 0x01);
        (await Reader.ShotAsync(Session, "spec-a", 1, default)).Should().BeNull("v1 of a is another set than v1 of b");
        (await Writer.SetShotAsync(Session, "mock-unknown", [0xFF], default)).Should().BeFalse();
        a1.Version.Should().Be(b1.Version);
    }

    [Fact]
    public async Task ConversationMocks_MockRows_InvisibleToImagesUploadsAndByteCap()
    {
        var version = await AddAsync("spec-a");
        await Writer.SetShotAsync(Session, version.SetId, [0xFF, 0xD8], default);
        await using var db = Db();

        (await new ReferenceFileRepository(db).ListImagesAsync(Session, default)).Should().BeEmpty();
        (await new ReferenceSetRepository(db).ListAsync(Session, default)).Should().BeEmpty();
        (await new ReferenceUsageRepository(db).BytesAsync(Session, default)).Should().Be(0);
    }

    [Fact]
    public async Task ConversationMocks_ConversationDelete_RemovesThem()
    {
        var version = await AddAsync("spec-a");
        await Writer.SetShotAsync(Session, version.SetId, [0xFF, 0xD8], default);
        await using var db = Db();

        await new ReferenceFileRepository(db).DeleteBySessionAsync(Session, new HashSet<string> { version.SetId }, default);

        (await Reader.ListAsync(Session, default)).Should().BeEmpty("a kept set keeps its uploads, never its mocks");
        (await db.Set<ReferenceFile>().CountAsync(f => f.Kind == ReferenceFileKind.Mock)).Should().Be(0);
    }

    private async Task<MockVersion> AddAsync(string specId) =>
        (await Writer.AddAsync(Session, specId, $"<h1>{specId}</h1>", default)).Version!;

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
