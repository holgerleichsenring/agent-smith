using AgentSmith.Contracts.Runs;

namespace DemoSeed;

/// <summary>The specs of the demo story, as the ticket branch and the run record carry them.</summary>
internal static partial class Story
{
    public sealed record DemoSpec(string Id, string Label, string Goal, string Repo, string[] Steps, string[] Done);

    public static readonly DemoSpec[] VolumeSpecs =
    [
        new(SpecA, "pricing-volume-tiers",
            "A cart line of 10 or more units of one product is priced at the tier rate from the price list.",
            "shop-api",
            ["VolumeTier record and tier lookup in PriceList", "PriceCalculator applies the tier before rounding", "tests for the 9/10/50 unit boundaries"],
            ["dotnet test passes", "a 10-unit line is priced at the tier rate"]),
        new(SpecB, "cart-discount-line",
            "The cart total shows the tier discount as its own line, and coupons apply after it.",
            "shop-api",
            ["CartTotals returns a discount line", "coupon application runs after the tier discount", "tests for a cart with tier + coupon"],
            ["the cart total response carries a 'Volume discount' line", "a coupon still applies after the tier discount"]),
    ];

    public static readonly DemoSpec[] SavedSpecs =
    [
        new("2026-11-18-9c4fa", "cart-saved-items-store",
            "Items moved to 'saved for later' are stored per customer and survive a new session.", "shop-api",
            ["SavedItems table and repository", "POST/DELETE /cart/saved endpoints", "tests for save, restore and a second session"],
            ["a saved item is still there after signing in again", "restoring an item puts it back into the cart"]),
        new("2026-11-18-9c4fb", "cart-saved-items-page",
            "The cart page lists saved items under the cart with 'Move to cart' and 'Remove'.", "shop-web",
            ["SavedItems list under the cart", "move and remove actions", "component tests"],
            ["the list shows every saved item", "'Move to cart' moves it in one click"]),
    ];

    public static readonly DemoSpec[] ReorderSpecs =
    [
        new("2026-11-18-9c4fc", "orders-reorder-endpoint",
            "POST /orders/{id}/reorder puts every still-available line of a past order into the cart.", "shop-api",
            ["reorder endpoint", "skip discontinued products and say which", "tests"],
            ["available lines land in the cart", "a discontinued product is named in the response"]),
        new("2026-11-18-9c4fd", "orders-reorder-button",
            "Each order in the order history has a 'Reorder' button that calls the endpoint.", "shop-web",
            ["Reorder button on the order row", "toast naming skipped products", "component tests"],
            ["one click fills the cart", "skipped products are named"]),
    ];

    public static readonly DemoSpec[] ExportSpecs =
    [
        new("2026-11-18-9c4fe", "orders-csv-export",
            "Customers can download their order history as CSV from the order history page.", "shop-api",
            ["GET /orders/export.csv", "columns: date, order number, items, total", "tests"],
            ["the file opens in a spreadsheet with one row per order"]),
    ];

    public static string SpecYaml(DemoSpec spec, string ticket) =>
        $"""
        spec: {spec.Id}
        goal: "{spec.Goal}"
        applies_to: "{spec.Repo}"
        ticket: "{ticket}"
        steps:
        {string.Join("\n", spec.Steps.Select((s, i) => $"  - id: s{i + 1}\n    action: \"{s}\""))}
        done:
        {string.Join("\n", spec.Done.Select(d => $"  - \"{d}\""))}

        """;

    public static string AcceptanceJson(params (string Text, string Status, string Reason, string? Citation)[] rows) =>
        ReportJson(rows, [], []);

    /// <summary>2026-11-18-c8e2d: a spec's master report — its rows, what it fixed in passing, the follow-ups it proposes.</summary>
    public static string ReportJson(
        (string Text, string Status, string Reason, string? Citation)[] rows, string[] fixedInPassing, FollowUpView[] followUps) =>
        RunStoryJson.Serialize(new AcceptanceView(
            [.. rows.Select(r => new AcceptanceCriterionView(r.Text, r.Status, r.Reason, r.Citation))],
            "verbatim", "the ratified spec", AcceptanceSources.MasterVerification,
            FixedInPassing: fixedInPassing, FollowUps: followUps));

    public static string LedgerJson(params (string Activity, string Status, string? Target)[] items) =>
        RunStoryJson.Serialize(items.Select((x, i) => new ProgressLedgerItemView($"p{i + 1}", x.Activity, x.Status, x.Target)).ToList());
}
