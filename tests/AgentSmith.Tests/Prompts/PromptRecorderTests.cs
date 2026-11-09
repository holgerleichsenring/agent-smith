using AgentSmith.Contracts.Prompts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Prompts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// 2026-11-09-0620a: a tool loop stores each call as its predecessor plus what it appended, a block once, and the
/// reader rebuilds every call in send order; deletion and retention take the records and the blocks nothing holds.
/// </summary>
public sealed class PromptRecorderTests : IDisposable
{
    private const string Run = "2026-11-09T22-00-00-aaaa";
    private readonly PromptStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task PromptRecorder_AppendedCall_StoresOnlyTheNewBlocksUnderItsParent()
    {
        var first = await RecordAsync(Tools, System, User("go"));
        var second = await RecordAsync(Tools, System, User("go"), Assistant("call"), ToolResult("done"));

        using var db = _store.Db();
        var record = await db.Set<PromptRecord>().SingleAsync(r => r.Id == second);
        record.ParentId.Should().Be(first);
        record.ParentLength.Should().Be(3);
        (await db.Set<PromptRecordBlock>().CountAsync(b => b.RecordId == second)).Should().Be(2);
    }

    [Fact]
    public async Task PromptRecorder_RepeatedBlock_IsStoredOnce()
    {
        await RecordAsync(Tools, System, User("one"));
        await _store.Recorder.RecordAsync("2026-11-09T22-00-00-bbbb", [Tools, System, User("two")], 1, default);

        using var db = _store.Db();
        (await db.Set<PromptBlockRow>().CountAsync()).Should().Be(4, "the tool list and the system prompt are shared");
    }

    [Fact]
    public async Task PromptRecordReader_ChainedRecords_RebuildTheCallInSendOrder()
    {
        await RecordAsync(Tools, System, User("go"));
        await RecordAsync(Tools, System, User("go"), Assistant("call"));
        var third = await RecordAsync(Tools, System, User("go"), Assistant("call"), ToolResult("done"));

        using var db = _store.Db();
        var view = await new PromptRecordReader(db).ReadAsync(Run, third!, default);

        view!.Complete.Should().BeTrue();
        view.ToolCount.Should().Be(1);
        view.Blocks.Select(b => b.Content).Should().Equal("tools", "system", "go", "call", "done");
        view.Blocks.Select(b => b.Position).Should().Equal(0, 1, 2, 3, 4);
    }

    [Fact]
    public async Task RunDeletion_RemovesPromptRecordsAndOrphanBlocks()
    {
        await RecordAsync(Tools, System, User("go"));
        await _store.Recorder.RecordAsync("2026-11-09T22-00-00-bbbb", [Tools, User("other")], 1, default);

        using (var db = _store.Db()) await new RunDeletionRepository(db).DeleteAsync(Run, default);

        using var check = _store.Db();
        (await check.Set<PromptRecord>().Select(r => r.RunId).ToListAsync()).Should().Equal("2026-11-09T22-00-00-bbbb");
        (await check.Set<PromptBlockRow>().Select(b => b.Content).ToListAsync())
            .Should().BeEquivalentTo("tools", "other");
    }

    [Fact]
    public async Task RunRetention_PrunesOldRecordsAndOrphanBlocks()
    {
        await RecordAsync(Tools, System, User("go"));

        using (var db = _store.Db())
            await new RunRetentionService(db, new FutureClock()).PruneAsync(TimeSpan.FromDays(1), default);

        using var check = _store.Db();
        (await check.Set<PromptRecord>().CountAsync()).Should().Be(0);
        (await check.Set<PromptRecordBlock>().CountAsync()).Should().Be(0);
        (await check.Set<PromptBlockRow>().CountAsync()).Should().Be(0);
    }

    private Task<string?> RecordAsync(params PromptBlock[] blocks) => _store.Recorder.RecordAsync(Run, blocks, 1, default);

    private static readonly PromptBlock Tools = new(PromptBlockKind.Tools, string.Empty, "tools");
    private static readonly PromptBlock System = new(PromptBlockKind.Message, "system", "system");
    private static PromptBlock User(string text) => new(PromptBlockKind.Message, "user", text);
    private static PromptBlock Assistant(string text) => new(PromptBlockKind.Message, "assistant", text);
    private static PromptBlock ToolResult(string text) => new(PromptBlockKind.Message, "tool", text);

    private sealed class FutureClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(30);
    }
}
