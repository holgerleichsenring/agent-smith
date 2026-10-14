using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using System.Linq.Expressions;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-8762r: where a conversation's mock rows are — kind <see cref="ReferenceFileKind.Mock"/>,
/// one set per version holding the page (<c>{specId}.html</c>) and, once rendered, its screenshot
/// (<c>{specId}.jpg</c>). Every read of images, uploads and the byte cap filters by its own kinds, so
/// none of them sees a mock.
/// </summary>
internal static class MockRows
{
    internal const string PageType = "text/html";
    internal const string ShotType = "image/jpeg";

    internal static string PathOf(string specId) => specId + ".html";

    internal static string SpecOf(string path) => path.EndsWith(".html", StringComparison.Ordinal) ? path[..^5] : path;

    internal static Expression<Func<ReferenceFile, bool>> Page(string sessionId) =>
        f => f.SessionId == sessionId && f.Kind == ReferenceFileKind.Mock && f.MediaType == PageType;

    internal static Expression<Func<ReferenceFile, bool>> Shot(string sessionId) =>
        f => f.SessionId == sessionId && f.Kind == ReferenceFileKind.Mock && f.MediaType == ShotType;
}
