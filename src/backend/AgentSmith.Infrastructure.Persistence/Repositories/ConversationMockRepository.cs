using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-8762r: writes a design conversation's HTML mocks (<see cref="MockRows"/>); reads are
/// <see cref="ConversationMockReadRepository"/>'s.
/// <para>
/// THE VERSION NUMBER IS NEVER STORED. It is the rank by id of the spec's page rows in the
/// conversation, so two writes that overlap still get two numbers once both are committed.
/// </para>
/// </summary>
public sealed class ConversationMockRepository(IUnitOfWork unitOfWork, DialogueGlobalConfig caps)
{
    public async Task<(MockVersion? Version, string? Refusal)> AddAsync(
        string sessionId, string specId, string html, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (await CapAsync(sessionId, specId, html, ct) is { } refusal) return (null, refusal);
        var path = MockRows.PathOf(specId);
        var bytes = Encoding.UTF8.GetBytes(html);
        var row = new ReferenceFile
        {
            SessionId = sessionId, SetId = $"mock-{Guid.NewGuid():N}", Kind = ReferenceFileKind.Mock,
            RelativePath = path, MediaType = MockRows.PageType, Content = bytes, Length = bytes.LongLength,
        };
        unitOfWork.Add(row);
        await unitOfWork.SaveChangesAsync(ct);
        var version = await Pages(sessionId).CountAsync(f => f.RelativePath == path && f.Id <= row.Id, ct);
        return (new MockVersion(row.SetId, specId, version, MockVersion.NameOf(specId, version), row.CreatedAt), null);
    }

    public async Task<bool> SetShotAsync(string sessionId, string setId, byte[] jpeg, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        var page = await Pages(sessionId).Where(f => f.SetId == setId).Select(f => f.RelativePath).FirstOrDefaultAsync(ct);
        if (page is null) return false;
        foreach (var old in await unitOfWork.Set<ReferenceFile>().Where(f => f.SessionId == sessionId && f.SetId == setId
                     && f.Kind == ReferenceFileKind.Mock && f.MediaType == MockRows.ShotType).ToListAsync(ct))
            unitOfWork.Remove(old);
        unitOfWork.Add(new ReferenceFile
        {
            SessionId = sessionId, SetId = setId, Kind = ReferenceFileKind.Mock, RelativePath = MockRows.SpecOf(page) + ".jpg",
            MediaType = MockRows.ShotType, Content = jpeg, Length = jpeg.LongLength,
        });
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string?> CapAsync(string sessionId, string specId, string html, CancellationToken ct)
    {
        if (html.Length > caps.MockMaxChars)
            return $"the mock is {html.Length} characters, over dialogue.mock_max_chars = {caps.MockMaxChars}, the cap that keeps "
                + "a mock readable back whole";
        var pages = Pages(sessionId);
        if (await pages.CountAsync(f => f.RelativePath == MockRows.PathOf(specId), ct) >= caps.MockVersionsPerSpec)
            return $"{specId} already has {caps.MockVersionsPerSpec} versions, dialogue.mock_versions_per_spec; revise the "
                + "newest instead of adding one";
        return await pages.CountAsync(ct) >= caps.MockVersionsPerConversation
            ? $"this conversation already holds {caps.MockVersionsPerConversation} mock versions, "
                + "dialogue.mock_versions_per_conversation, the bound on what one conversation stores"
            : null;
    }

    private IQueryable<ReferenceFile> Pages(string sessionId) =>
        unitOfWork.Set<ReferenceFile>().AsNoTracking().Where(MockRows.Page(sessionId));
}
