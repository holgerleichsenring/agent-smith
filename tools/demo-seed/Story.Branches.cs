using AgentSmith.Contracts.Specs;

namespace DemoSeed;

internal static partial class Story
{
    public const string Conversation = "c-demo-volume";

    /// <summary>The approved series of #41 on its ticket branch, and the pointer filing records for it.</summary>
    public static async Task BranchesAsync(IServiceProvider services, string demoRoot, CancellationToken ct)
    {
        var sha = DemoRepos.WriteSeries(demoRoot, "shop-api", "41", "2026-11-18-7b2e",
            "Volume discounts in the cart total", VolumeSpecs, "Sam Sample", Conversation, Anchor.AddMinutes(-30));
        var pointers = services.GetRequiredService<ISpecSetPointerStore>();
        await pointers.SaveAsync("shop", new SpecSetPointer("github-41", "shop-api", sha, 1, SeriesId: "2026-11-18-7b2e"), ct);
        foreach (var (ticket, goal, specs) in new[]
        {
            ("44", "Saved items survive a new session", SavedSpecs),
            ("45", "Reorder a past order in one click", ReorderSpecs),
            ("46", "Remind the customer of items saved for a week", ReminderSpecs),
        })
        {
            var at = Anchor.AddMinutes(105);
            var head = DemoRepos.WriteSeries(demoRoot, "shop-api", ticket, "2026-11-18-9c4f", goal, specs, "Sam Sample", SeriesConversation, at);
            await pointers.SaveAsync("shop", new SpecSetPointer($"github-{ticket}", "shop-api", head, 1, SeriesId: "2026-11-18-9c4f"), ct);
        }
    }
}
