using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DemoSeed;

internal static partial class Story
{
    /// <summary>
    /// What the pull-request sweep and the landing record would hold by now: each opened pull request
    /// with its CI state, merged ones landed, and #46 waiting on #45.
    /// </summary>
    public static async Task RecordsAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>();
        var runs = await db.Runs.GroupBy(r => r.TicketId).Select(g => new { g.Key, Id = g.Max(r => r.Id) }).ToDictionaryAsync(r => r.Key, r => r.Id!, ct);
        Pr(db, "41", runs["41"], 57, "merged", Anchor.AddMinutes(41));
        Pr(db, "44", runs["44"], 58, "merged", Anchor.AddMinutes(170));
        Pr(db, "39", runs["39"], 55, "merged", Anchor.AddMinutes(-120));
        foreach (var (ticket, at) in new[] { ("41", Anchor.AddMinutes(41)), ("44", Anchor.AddMinutes(170)), ("39", Anchor.AddMinutes(-120)) })
            db.Add(new LandedTicket
            {
                Tracker = "acme-github", TicketKey = $"github-{ticket}", TicketId = ticket, Project = "shop",
                Kind = "merged", LandedAt = at, By = "codeably (CI green)",
                PrUrl = $"https://github.com/acme/shop-api/pull/{(ticket == "41" ? 57 : ticket == "44" ? 58 : 55)}",
            });
        db.Add(new TicketRequirement
        {
            Tracker = "acme-github", TicketKey = "github-46", TicketId = "46", Project = "shop",
            RequiredTicketKey = "github-45", RequiredTicketId = "45", StartsWhenMet = true, FiledInConversation = SeriesConversation,
        });
        await db.SaveChangesAsync(ct);

        // Stored secrets: envelope-encrypted under the rig's throwaway master key; the values are not real.
        var secrets = scope.ServiceProvider.GetRequiredService<AgentSmith.Infrastructure.Persistence.Repositories.StoredSecretRepository>();
        await secrets.SetAsync("anthropic_api_key", new AgentSmith.Contracts.Models.SecretInput("demo-not-a-key"), "Sam Sample", ct);
        await secrets.SetAsync("jira_token", new AgentSmith.Contracts.Models.SecretInput("demo-not-a-token"), "Sam Sample", ct);
    }

    private static void Pr(AgentSmithDbContext db, string ticket, string runId, int number, string state, DateTimeOffset at)
    {
        var url = $"https://github.com/acme/shop-api/pull/{number}";
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "shop-api", RepoKey = "acme/shop-api", Number = number.ToString(),
            Project = "shop", TicketId = ticket, RunId = runId, State = state,
            StateAtTicks = at.UtcTicks, CheckedAtTicks = at.UtcTicks, HeadSha = "4f2c9e1",
            CiState = CiStates.Green, CiHeadSha = "4f2c9e1", CiAtTicks = at.AddMinutes(-4).UtcTicks,
        });
    }
}
