using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>2026-10-01-283db: a conversation's websites, stored as sets and listed as summaries.</summary>
public sealed class ReferenceSetRepositoryTests
{
    [Fact]
    public async Task ReferenceSetRepository_Add_StoresOneSetAndListsItByItsFolder()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);

        var added = await sets.AddAsync("s-1", [File("site/index.html", 4), File("site/a.css", 3)], CancellationToken.None);
        await sets.AddAsync("s-1", [File("one.css", 2), File("two.css", 2)], CancellationToken.None);
        await new ReferenceFileRepository(db).AddAsync(LegacyAttachmentCopyTests.Image("s-1"), CancellationToken.None);

        var listed = await sets.ListAsync("s-1", CancellationToken.None);

        listed.Select(s => (s.Name, s.Files, s.Bytes)).Should().Equal(("site", 2, 7L), (ReferenceSetRepository.UnnamedSet, 2, 4L));
        listed[0].SetId.Should().Be(added.SetId, "images are no set of a website");
    }

    [Fact]
    public async Task ReferenceSetRepository_Add_NoFiles_IsRefused()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();

        await new ReferenceSetRepository(db).Invoking(s => s.AddAsync("s-1", [], CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    private static ReferenceFile File(string path, int length) => new()
    {
        RelativePath = path, MediaType = "text/plain", Content = new byte[length],
    };
}
