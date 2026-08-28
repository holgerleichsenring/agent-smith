using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace AgentSmith.Tests.Server.Archive;

/// <summary>
/// 2026-08-28-3793: the thirty-megabyte case, built deliberately.
/// <para>
/// Kestrel's default request-body ceiling is thirty megabytes and nothing in this server
/// raises it, so the import route raises it for itself. A test that happened to upload a
/// small archive would pass whether the raise existed or not — it would prove nothing — so
/// this one asserts the archive really is over the ceiling BEFORE it posts it, and posts
/// the same weight at a route that did not raise it as the control. Same server, same
/// client, same bytes: one route refuses for size, the other does not.
/// </para>
/// <para>
/// The rows are random base64 because a zip is what crosses the wire: compressible content
/// would need hundreds of megabytes in the database to clear thirty on the wire.
/// </para>
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class ArchiveUploadCeilingServedTests : IDisposable
{
    private const long DefaultKestrelCeiling = 30 * 1024 * 1024;
    private const int RowCount = 12;
    // Twelve rows of three megabytes compress to a little over thirty-five — a fifth clear
    // of the ceiling, and no more work than that margin needs. Deflating this is the most
    // expensive thing in the suite, and a suite that starves its own wall-clock assertions
    // is a suite that fails for reasons nobody can act on.
    private const int RandomBytesPerRow = 3 * 1024 * 1024;

    private readonly List<string> _temporary = [];

    [Fact]
    public async Task Import_AnArchiveLargerThanTheDefaultCeiling_IsAccepted()
    {
        var archivePath = await LargeArchiveAsync();
        new FileInfo(archivePath).Length.Should().BeGreaterThan(DefaultKestrelCeiling,
            "an upload under the default ceiling would prove nothing about raising it");
        await using var server = await BootAsync(MigratedDatabase());

        await using var upload = File.OpenRead(archivePath);
        var response = await server.Client.PostAsync("/api/archive/import", new StreamContent(upload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ArchiveRestoreResponse>())!
            .TotalRows.Should().Be(RowCount);
    }

    [Fact]
    public async Task Import_TheSameWeightAtARouteThatRaisedNothing_IsRefusedForItsSize()
    {
        // The control. Without it the case above could pass on a server that has no ceiling
        // at all, and the claim "the import raises it for ITSELF" would be untested.
        await using var server = await BootAsync(MigratedDatabase());

        var outcome = await PostAsync(server, "/api/config/import", DefaultKestrelCeiling + 1024);

        outcome.Should().NotBe("HTTP 200");
        outcome.Should().Match(o => o == "HTTP 413" || o.StartsWith("refused mid-send"),
            "the default ceiling is real and every other route still stands behind it — "
            + "Kestrel either answers 413 or resets the connection under the sender, and "
            + $"both are that refusal. Got: {outcome}");
    }

    /// <summary>
    /// How an upload ENDED, as a sentence — because an over-ceiling body does not always
    /// come back as a status code: Kestrel may reset the connection while the client is
    /// still sending, and the client then never reads a response at all.
    /// </summary>
    private static async Task<string> PostAsync(BootedServer server, string route, long bytes)
    {
        try
        {
            var response = await server.Client.PostAsync(route, new ByteArrayContent(new byte[bytes]));
            return $"HTTP {(int)response.StatusCode}";
        }
        catch (HttpRequestException ex)
        {
            return $"refused mid-send: {ex.InnerException?.GetType().Name ?? ex.GetType().Name}";
        }
    }

    private async Task<string> LargeArchiveAsync()
    {
        using var source = MigratedStoreTemplate.OpenCopy();
        await using (var db = MigratedStoreTemplate.Context(source))
        {
            for (var row = 0; row < RowCount; row++) db.RunArtifacts.Add(Incompressible(row));
            await db.SaveChangesAsync();
        }

        var path = Temporary("zip");
        await using (var file = File.Create(path))
        await using (var db = MigratedStoreTemplate.Context(source))
        {
            using var archive = new DataArchiveHarness();
            await archive.Writer.WriteAsync(db, file);
        }

        return path;
    }

    private static RunArtifact Incompressible(int row) => new()
    {
        Id = 5_000 + row,
        RunId = $"heavy-{row}",
        Kind = "result",
        Content = Convert.ToBase64String(RandomNumberGenerator.GetBytes(RandomBytesPerRow)),
    };

    private string MigratedDatabase()
    {
        var path = Temporary("db");
        _temporary.Add(path + "-wal");
        _temporary.Add(path + "-shm");
        ArchiveStore.Migrate(path);
        return path;
    }

    private Task<BootedServer> BootAsync(string databasePath)
    {
        var configPath = Temporary("yml");
        File.WriteAllText(configPath, $"""
            persistence:
              provider: sqlite
              connection_string: Data Source={databasePath}

            """);
        return BootedServer.StartAsync(configPath);
    }

    private string Temporary(string extension)
    {
        var path = Path.Combine(
            Path.GetTempPath(), $"agentsmith-ceiling-{Guid.NewGuid():N}.{extension}");
        _temporary.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _temporary.Where(File.Exists)) File.Delete(path);
    }
}
