using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: the tags a repository lists, bounded. A popular repository lists
/// thousands (sbtscala/scala-sbt: 3130), so the list is read <see cref="PageSize"/> at a time
/// for at most <see cref="MaxPages"/> pages; a registry that ignores the page size (mcr.microsoft.com)
/// answers in one page whose body the transport bounds.
/// </summary>
public sealed class RegistryTagLister(RegistryClient client, RegistryTagPageParser pages)
{
    public const int PageSize = 1000;
    public const int MaxPages = 5;
    private const string Json = "application/json";

    /// <summary>The tags read, or null when not even the first page could be read.</summary>
    public async Task<IReadOnlyList<string>?> ListAsync(
        RegistryImageReference image, string? bearer, CancellationToken cancellationToken)
    {
        var tags = new List<string>();
        Uri? next = image.TagList(PageSize);
        for (var page = 0; page < MaxPages && next is not null; page++)
        {
            var reply = await client.SendAsync(HttpMethod.Get, next, Json, bearer, cancellationToken);
            if (reply.Status != 200 || pages.Tags(reply.Body) is not { } found)
                return page == 0 ? null : tags;
            tags.AddRange(found);
            bearer = reply.Bearer;
            next = pages.Next(next, reply.Link);
        }
        return tags;
    }
}
