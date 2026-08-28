using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using AgentSmith.Server.Models;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Archive;

/// <summary>
/// 2026-08-28-3793: the two endpoints on the REAL composition, over real HTTP, against a
/// real migrated database — because every claim this phase makes is about what a running
/// server does with a file, and none of it can be proven by calling a handler.
/// <para>
/// The refusal cases are the point. The literal emptiness rule the CLI applies cannot hold
/// here: a live server has already written rows about itself. So each case seeds exactly
/// what the review predicted a server would have — an observed caller, a migrated role
/// mapping, a recorded run — and asserts which of them refuses a restore and which do not.
/// </para>
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class DataArchiveServedTests : IDisposable
{
    private const string ExportRoute = "/api/archive/export";
    private const string ImportRoute = "/api/archive/import";
    private const string PreviewRoute = "/api/archive/preview";

    private readonly List<string> _temporary = [];
    private readonly SqliteConnection _source = MigratedStoreTemplate.OpenCopy();
    private readonly DataArchiveHarness _archive = new();

    [Fact]
    public async Task Import_AnInstallationThatHasRunNothing_IsAccepted()
    {
        await using var server = await BootAsync(MigratedDatabase());
        using var archive = await ArchiveOfASeededInstallationAsync();

        var response = await PostAsync(server, archive);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await response.Content.ReadFromJsonAsync<ArchiveRestoreResponse>();
        restored!.TotalRows.Should().BeGreaterThan(0);
        restored.SchemaHead.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Import_AnInstallationWithASignedInCaller_IsStillAccepted()
    {
        // The whole reason the server states its own rule: this is what an installation
        // looks like the moment the operator who wants to restore has signed in.
        var database = MigratedDatabase();
        await ArchiveStore.WriteOwnBookkeepingAsync(database);
        await using var server = await BootAsync(database);
        using var archive = await ArchiveOfASeededInstallationAsync();

        var response = await PostAsync(server, archive);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an observed caller and a migrated role mapping are the server's own "
            + "bookkeeping, not work a restore would collide with");
    }

    [Fact]
    public async Task Import_AnInstallationThatHasRecordedARun_IsRefusedWithTheCause()
    {
        var database = MigratedDatabase();
        await ArchiveStore.RecordARunAsync(database);
        await using var server = await BootAsync(database);
        using var archive = await ArchiveOfASeededInstallationAsync();

        var response = await PostAsync(server, archive);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await RefusalOf(response)).Should()
            .Contain("already recorded 1 run").And.Contain("Nothing was written");
    }

    [Fact]
    public async Task Import_ASchemaHeadThatDiffers_IsRefusedWithTheCause()
    {
        await using var server = await BootAsync(MigratedDatabase());
        using var taken = await ArchiveOfASeededInstallationAsync();
        using var tampered = RewrittenArchive.WithManifest(
            taken, m => m with { SchemaHead = "ASchemaThisBuildHasNeverSeen" });

        var response = await PostAsync(server, tampered);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await RefusalOf(response)).Should()
            .Contain("ASchemaThisBuildHasNeverSeen").And.Contain("database migrate");
    }

    [Fact]
    public async Task Import_AfterARestore_TheConfigStoreIsReloadedAndTheEpochSignalled()
    {
        await using var server = await BootAsync(MigratedDatabase());
        var store = server.Services.GetRequiredService<IConfigStore>();
        var signal = server.Services.GetRequiredService<IConfigReloadSignal>();
        // Reading first is what makes this a real test: the store caches its catalog, so a
        // restore that skipped the reload would still be serving THIS empty answer.
        store.GetAgents().Should().BeEmpty();
        var epoch = await signal.CurrentEpochAsync(CancellationToken.None);
        using var archive = await ArchiveOfASeededInstallationAsync();

        (await PostAsync(server, archive)).StatusCode.Should().Be(HttpStatusCode.OK);

        store.GetAgents().Select(a => a.Id).Should().Contain(ArchiveStore.AgentId);
        (await signal.CurrentEpochAsync(CancellationToken.None)).Should().BeGreaterThan(epoch);
    }

    [Fact]
    public async Task Export_IsWrittenAsItIsProduced_NotBufferedWhole()
    {
        await using var server = await BootAsync(MigratedDatabase());

        var response = await server.Client.GetAsync(
            ExportRoute, HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentLength.Should().BeNull(
            "a length can only be stated by something that held the whole file first");
        response.Headers.TransferEncodingChunked.Should().BeTrue();
        await using var body = await response.Content.ReadAsStreamAsync();
        FirstEntryOf(body).Should().Be(DataArchiveFormat.ManifestEntry);
    }

    [Fact]
    public async Task Export_TheArchiveItStreams_RestoresIntoAFreshInstallation()
    {
        // The round trip over HTTP: what the export endpoint writes is what the import
        // endpoint takes, with no file touched by anything else in between.
        await using var exporting = await BootAsync(await SeededDatabaseAsync());
        await using var importing = await BootAsync(MigratedDatabase());
        using var taken = new MemoryStream(
            await exporting.Client.GetByteArrayAsync(ExportRoute));

        var response = await PostAsync(importing, taken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        importing.Services.GetRequiredService<IConfigStore>()
            .GetAgents().Select(a => a.Id).Should().Contain(ArchiveStore.AgentId);
    }

    [Fact]
    public async Task Preview_BeforeTheDownload_NamesEveryTableAndItsRowCount()
    {
        await using var server = await BootAsync(await SeededDatabaseAsync());

        var preview = await server.Client.GetFromJsonAsync<ArchivePreviewResponse>(PreviewRoute);

        preview!.Tables.Should().HaveCountGreaterThan(20);
        preview.Tables.Should().Contain(t => t.Table == "Runs");
        preview.TotalRows.Should().Be(preview.Tables.Sum(t => t.Rows)).And.BeGreaterThan(0);
        preview.SchemaHead.Should().NotBeEmpty();
        preview.Provider.Should().Contain("Sqlite");
    }

    private static async Task<string> RefusalOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ArchiveRefusalResponse>())!.Refusal;

    private static string FirstEntryOf(Stream body)
    {
        using var buffered = new MemoryStream();
        body.CopyTo(buffered);
        buffered.Position = 0;
        using var zip = new ZipArchive(buffered, ZipArchiveMode.Read);
        return zip.Entries[0].FullName;
    }

    private static Task<HttpResponseMessage> PostAsync(BootedServer server, Stream archive)
    {
        archive.Position = 0;
        return server.Client.PostAsync(ImportRoute, new StreamContent(archive));
    }

    private async Task<MemoryStream> ArchiveOfASeededInstallationAsync()
    {
        await using var db = MigratedStoreTemplate.Context(_source);
        await new FullStoreSeed().SeedAsync(db);
        await ArchiveStore.WriteConfigAsync(db);
        return await _archive.ExportAsync(db);
    }

    /// <summary>A migrated database on disk that already holds an installation's rows.</summary>
    private async Task<string> SeededDatabaseAsync()
    {
        var path = MigratedDatabase();
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        await using var db = MigratedStoreTemplate.Context(connection);
        await new FullStoreSeed().SeedAsync(db);
        await ArchiveStore.WriteConfigAsync(db);
        return path;
    }

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
            Path.GetTempPath(), $"agentsmith-archive-{Guid.NewGuid():N}.{extension}");
        _temporary.Add(path);
        return path;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _source.Dispose();
        foreach (var path in _temporary.Where(File.Exists)) File.Delete(path);
    }
}
