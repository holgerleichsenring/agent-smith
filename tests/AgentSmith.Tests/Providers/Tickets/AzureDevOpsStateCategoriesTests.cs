using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>2026-10-13-9a59k: a work item is closed when its type files its state under Completed or
/// Removed. The cache is static, so every test reads its own (organization, project, type).</summary>
public sealed class AzureDevOpsStateCategoriesTests
{
    private static readonly List<WorkItemStateColor> AgileBug =
    [
        new() { Name = "New", Category = "Proposed" },
        new() { Name = "Active", Category = "InProgress" },
        new() { Name = "Resolved", Category = "Resolved" },
        new() { Name = "Closed", Category = "Completed" },
        new() { Name = "Removed", Category = "Removed" },
    ];

    private static Dictionary<string, object> Fields(string type, string state) =>
        new() { ["System.WorkItemType"] = type, ["System.State"] = state };

    private static (AzureDevOpsStateCategories Categories, Func<int> Reads) Over(
        string organization, Func<List<WorkItemStateColor>> answer)
    {
        var reads = 0;
        var categories = new AzureDevOpsStateCategories($"https://dev.azure.com/{organization}", "proj", (_, _) =>
        {
            reads++;
            return Task.FromResult(answer());
        });
        return (categories, () => reads);
    }

    [Fact]
    public async Task AzureDevOpsStateCategories_CompletedOrRemoved_IsClosed_ResolvedIsNot()
    {
        var (categories, _) = Over(Guid.NewGuid().ToString("N"), () => AgileBug);

        (await categories.IsClosedAsync(Fields("Bug", "Closed"), CancellationToken.None)).Should().BeTrue();
        (await categories.IsClosedAsync(Fields("Bug", "Removed"), CancellationToken.None)).Should().BeTrue();
        (await categories.IsClosedAsync(Fields("Bug", "Resolved"), CancellationToken.None)).Should().BeFalse();
        (await categories.IsClosedAsync(Fields("Bug", "Active"), CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task AzureDevOpsStateCategories_SecondTicketOfSameType_ReadsStatesOnce()
    {
        var (categories, reads) = Over(Guid.NewGuid().ToString("N"), () => AgileBug);

        await categories.IsClosedAsync(Fields("Bug", "Closed"), CancellationToken.None);
        await categories.IsClosedAsync(Fields("Bug", "Active"), CancellationToken.None);
        await categories.IsClosedAsync(Fields("Task", "Active"), CancellationToken.None);

        reads().Should().Be(2, "one read per work item type, not per item");
    }

    [Fact]
    public async Task AzureDevOpsStateCategories_ReadFails_Throws()
    {
        var (categories, _) = Over(Guid.NewGuid().ToString("N"), () => throw new HttpRequestException("states endpoint down"));

        var read = () => categories.IsClosedAsync(Fields("Bug", "Closed"), CancellationToken.None);

        await read.Should().ThrowAsync<HttpRequestException>("an unknown category is never read as open");
    }
}
