using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>2026-11-04-96a0c: M counts the files read_file can open, by read_file's own rule — not by media type.</summary>
public sealed class ReferenceCoverageReaderTests
{
    [Fact]
    public async Task ReferenceCoverageReader_NonUtf8OrOversizedFiles_AreOutsideMAndCountedApart()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var set = await sets.AddAsync("s-1", [
            File("docs/a.md", "# a"u8.ToArray()),
            File("docs/b.pdf", [0x25, 0x50, 0x44, 0x46, 0xFF, 0xFE, 0x00]),
            File("docs/big.txt", Enumerable.Repeat((byte)'a', (int)SizeLimits.ReadFileMaxBytes + 1).ToArray()),
        ], default);

        var coverage = (await ReferenceReads.Coverage(db).ForAsync("s-1", await sets.ListAsync("s-1", default), default))[set.SetId];

        coverage.Readable.Should().BeEquivalentTo(["docs/a.md"]);
        coverage.Unreadable.Should().Be(2);
        coverage.Unopened.Should().Equal("docs/a.md");
    }

    [Fact]
    public async Task ReferenceCoverageReader_OctetStreamSourceFile_CountsAsReadable()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var set = await sets.AddAsync("s-1", [
            File("app/Program.cs", "﻿class Program { }"u8.ToArray(), "application/octet-stream"),
        ], default);
        await new ReferenceReadRepository(db).AddAsync("s-1", set.SetId, ["app/Program.cs"], default);

        var coverage = (await ReferenceReads.Coverage(db).ForAsync("s-1", await sets.ListAsync("s-1", default), default))[set.SetId];

        coverage.Files.Should().Be(1, "source code is stored as octet-stream and read_file opens it");
        coverage.Opened.Should().BeEquivalentTo(["app/Program.cs"]);
        coverage.UnopenedCount.Should().Be(0);
    }

    private static ReferenceFile File(string path, byte[] content, string mediaType = "text/plain") =>
        new() { RelativePath = path, MediaType = mediaType, Content = content };
}
