using System.Text;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-01-283da: an import flushes at five hundred rows OR at 64 MB of content, so a table
/// of five-megabyte reference files is never held in the change tracker five hundred at a time.
/// </summary>
public sealed class ArchiveTableImporterTests
{
    [Fact]
    public async Task ArchiveTableImporter_LargeRows_FlushesBy64MegabytesOfContent()
    {
        using var connection = MigratedStoreTemplate.OpenCopy();
        var saves = new SaveSizes();
        await using var db = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite(connection).AddInterceptors(saves).Options);
        var type = db.Model.FindEntityType(typeof(ReferenceFile))!;
        var content = new byte[(ArchiveTableImporter.BatchContentBytes / 5) + 1];

        var imported = await new ArchiveTableImporter(new ArchiveRowCodec(), new GeneratedKeyProperty())
            .ImportAsync(db, type, Lines(type, content, rows: 6), CancellationToken.None);

        imported.Rows.Should().Be(6);
        saves.Rows.Should().Equal([5, 1], "five rows cross 64 MB, far below five hundred rows");
    }

    private static MemoryStream Lines(Microsoft.EntityFrameworkCore.Metadata.IEntityType type, byte[] content, int rows)
    {
        var codec = new ArchiveRowCodec();
        var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
            for (var i = 1; i <= rows; i++)
                writer.WriteLine(codec.Encode(type, new ReferenceFile
                {
                    Id = i, SessionId = "s-1", SetId = "set-1", Kind = ReferenceFileKind.Site,
                    RelativePath = $"f{i}.bin", MediaType = "application/octet-stream",
                    Length = content.Length, Content = content,
                }));
        stream.Position = 0;
        return stream;
    }

    private sealed class SaveSizes : SaveChangesInterceptor
    {
        public List<int> Rows { get; } = [];

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (result > 0) Rows.Add(result);
            return ValueTask.FromResult(result);
        }
    }
}
