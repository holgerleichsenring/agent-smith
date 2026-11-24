using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: a PoC-keyed route reads its project from the PoC row named by {pocId} — the owner's poc- project —
/// so a foreign PoC answers the empty 404 of 4a4d; an unknown or malformed id is missing.
/// </summary>
public sealed class PocProjectSourceTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;

    public PocProjectSourceTests() => _db = MigratedStoreTemplate.Context(_connection);

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ResolveAsync_APocId_IsItsRowsProject_UnknownOrMalformedIsMissing()
    {
        var poc = await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-ann"));
        var resolver = new ProjectSourceResolver(new ResourceProjectReader(_db));

        var found = await resolver.ResolveAsync(ProjectFrom.Poc, Request(poc.Id.ToString()));
        (found.Exists, found.Names.Single()).Should().Be((true, "poc-ann"));
        (await resolver.ResolveAsync(ProjectFrom.Poc, Request((poc.Id + 9).ToString()))).Exists.Should().BeFalse();
        (await resolver.ResolveAsync(ProjectFrom.Poc, Request("todo"))).Exists.Should().BeFalse("PoC routes are keyed by id");
        ProjectFrom.Poc.Wire.Should().Be("poc");
    }

    private static HttpContext Request(string pocId)
    {
        var http = new DefaultHttpContext();
        http.Request.RouteValues["pocId"] = pocId;
        return http;
    }
}
