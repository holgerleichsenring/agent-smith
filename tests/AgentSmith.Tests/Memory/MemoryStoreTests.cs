using AgentSmith.Application.Services.Memory;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Memory;

public sealed class MemoryStoreTests
{
    private const string Root = "/work";
    private const string IndexPath = "/work/.agentsmith/memory/MEMORY.md";

    private readonly InMemorySandboxFileReader _reader = new();
    private readonly MemoryStore _sut;

    public MemoryStoreTests() => _sut = new MemoryStore(_reader, Root);

    [Fact]
    public async Task MemoryStore_TypedEntry_RoundTripsAndUpdatesIndex()
    {
        var entry = new MemoryEntry(
            "no-wrapper-shims", "Migrate callers directly, never bridge via a shim",
            MemoryEntryType.Feedback, "Never bridge an old interface to a new one.", "ratified");

        await _sut.UpsertAsync(entry, CancellationToken.None);
        var listed = await _sut.ListAsync(CancellationToken.None);

        listed.Should().ContainSingle().Which.Should().Be(entry);
        var index = _reader.Files[IndexPath];
        index.Should().Contain("- [no-wrapper-shims](no-wrapper-shims.md) (feedback, ratified) — "
                               + "Migrate callers directly, never bridge via a shim");
        index.Should().NotContain("Never bridge an old interface",
            "content NEVER lives in the index — one line per memory");
    }

    [Fact]
    public async Task UpsertAsync_SameName_UpdatesEntryAndReplacesIndexLine()
    {
        var v1 = new MemoryEntry("ticket-42", "first", MemoryEntryType.Project, "body v1");
        var v2 = new MemoryEntry("ticket-42", "second", MemoryEntryType.Project, "body v2");

        await _sut.UpsertAsync(v1, CancellationToken.None);
        await _sut.UpsertAsync(v2, CancellationToken.None);

        var index = _reader.Files[IndexPath];
        index.Should().Contain("second").And.NotContain("first");
        index.Split('\n').Count(l => l.StartsWith("- [ticket-42]", StringComparison.Ordinal))
            .Should().Be(1, "update-not-duplicate");
        (await _sut.ListAsync(CancellationToken.None)).Single().Body.Should().Be("body v2");
    }

    [Fact]
    public async Task MemoryStore_MalformedFile_SkippedNotThrown()
    {
        await _sut.UpsertAsync(
            new MemoryEntry("good", "a valid entry", MemoryEntryType.Reference, "body"),
            CancellationToken.None);
        _reader.Files["/work/.agentsmith/memory/broken.md"] = "no frontmatter at all";
        _reader.Files["/work/.agentsmith/memory/bad-type.md"] =
            "---\nname: bad-type\ndescription: d\nmetadata:\n  type: nonsense\n---\nbody";

        var listed = await _sut.ListAsync(CancellationToken.None);

        listed.Should().ContainSingle().Which.Name.Should().Be("good");
    }

    [Fact]
    public async Task ListAsync_AbsentStore_ReturnsEmpty()
    {
        (await _sut.ListAsync(CancellationToken.None)).Should().BeEmpty();
        (await _sut.ReadIndexAsync(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_IndexFileItself_IsNeverParsedAsEntry()
    {
        await _sut.UpsertAsync(
            new MemoryEntry("one", "single entry", MemoryEntryType.Project, "body"),
            CancellationToken.None);

        var listed = await _sut.ListAsync(CancellationToken.None);

        listed.Should().ContainSingle();
    }
}
