using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace DemoSeed;

/// <summary>
/// Three Work it out conversations, written through the server's own session, proposal, filing and
/// series stores: one with a drafted ticket set waiting to be filed, one whose filed tickets run as a
/// series, and an admin conversation with a drafted configuration change.
/// </summary>
internal static partial class Story
{
    private const string Platform = "dashboard";
    private const string Owner = "anonymous"; // the caller of an installation without sign-in
    public const string SeriesConversation = "c-demo-saved-carts";
    private const string DraftConversation = "c-demo-gift-cards";
    private const string AdminConversation = "c-demo-admin";
    private const string Sam = "Sam Sample";

    public static async Task ConversationsAsync(IServiceProvider services, StoryClock clock, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var sessions = sp.GetRequiredService<SpecDialogSessionManager>();
        var proposals = sp.GetRequiredService<SpecDialogProposalState>();
        await SeriesStoryAsync(sp, sessions, clock, ct);
        await AdminStoryAsync(sp, sessions, proposals, clock, ct);
        await DraftStoryAsync(sessions, proposals, clock, ct); // last: the newest conversation
        await SubjectsAsync(sp, ct);
    }

    /// <summary>The short names a first turn mints for a conversation; written here, since no model ran.</summary>
    private static async Task SubjectsAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AgentSmith.Infrastructure.Persistence.AgentSmithDbContext>();
        var subjects = new Dictionary<string, string>
        {
            [DraftConversation] = "Gift cards",
            [SeriesConversation] = "Saved carts and one-click reorder",
            [AdminConversation] = "billing merges on green CI",
        };
        foreach (var session in db.SpecDialogSessions.Where(x => subjects.Keys.Contains(x.ThreadId)))
            session.Subject = subjects[session.ThreadId];
        await db.SaveChangesAsync(ct);
    }

    // ── the drafted set, waiting for File ────────────────────────────────────────────────

    private static async Task DraftStoryAsync(SpecDialogSessionManager sessions, SpecDialogProposalState proposals,
        StoryClock clock, CancellationToken ct)
    {
        clock.Now = DateTimeOffset.UtcNow.AddMinutes(-22);
        _currentThread = DraftConversation;
        await sessions.OpenAsync(Platform, Platform, DraftConversation, Owner,
            new ActiveScope { Project = "shop", Repos = ["shop-api", "shop-web"] }, ct);
        await SayAsync(sessions, clock, TranscriptRole.User,
            "Customers keep asking for gift cards. We want to sell them as a product, send the code by email, and let people pay with a code at checkout. Partial use should leave the rest on the card.", 0);
        await SayAsync(sessions, clock, TranscriptRole.Assistant,
            "I read the checkout and the payment flow in shop-api and the cart page in shop-web. Payments go through `PaymentPlan`, which already supports more than one tender, so a gift card can be a tender rather than a discount. Two questions before I cut this:\n\n"
            + "1. Do gift cards expire? Many countries require at least three years.\n2. Can a customer buy a gift card with a gift card?", 3);
        await SayAsync(sessions, clock, TranscriptRole.User, "Three years, the legal minimum. And no, a gift card can't be paid with a gift card.", 4);
        await SayAsync(sessions, clock, TranscriptRole.Assistant,
            "Here is the cut: three tickets, each with its own reviewed specs. The second needs the first merged before it starts; the third is independent. The review found one gap (a refund of an order paid by gift card) and I added it to the second ticket's specs.\n\n"
            + "```yaml\ntickets: [gift-card-issue, gift-card-pay, order-history-csv]\n```\n\nFile them when you're happy with it.", 6, kind: SpecDialogTurnKind.TicketGraphProposal);

        var graph = new TicketGraphOutcome(
        [
            Graph("gift-card-issue", "Gift cards: issue, email and store codes",
                "Selling a gift card product issues a code, emails it and stores the balance for three years.", [],
                ["shop-api"], GiftIssueSpecs),
            Graph("gift-card-pay", "Gift cards: pay with a code at checkout",
                "A code can pay all or part of an order; the rest stays on the card; a refund goes back to the card.", ["gift-card-issue"],
                ["shop-api", "shop-web"], GiftPaySpecs),
            Graph("order-history-csv", "Order history: export as CSV",
                "Customers download their order history as CSV.", [], ["shop-api"], ExportSpecs),
        ])
        {
            FilingOffer = "Three tickets with five reviewed specs. File them?",
            OfferLed = true,
            ReviewedClean = [],
        };
        await proposals.StoreAwaitingAsync(await StateAsync(sessions, DraftConversation, ct), graph, ct);
    }

    private static readonly DemoSpec[] GiftIssueSpecs =
    [
        new("2026-11-18-b5d1a", "giftcard-issue-store",
            "Buying a gift card product creates a code with its balance and a three-year expiry.", "shop-api",
            ["GiftCard aggregate and repository", "issue on order paid", "tests"], ["a paid order with a gift card product issues one code"]),
        new("2026-11-18-b5d1b", "giftcard-issue-email",
            "The buyer receives the code by email with the amount and the expiry date.", "shop-api",
            ["email template", "send on issue", "tests"], ["the email names amount and expiry"]),
    ];

    private static readonly DemoSpec[] GiftPaySpecs =
    [
        new("2026-11-18-b5d1c", "giftcard-tender",
            "A gift card code is accepted as a tender in PaymentPlan and can pay part of an order.", "shop-api",
            ["GiftCardTender", "partial use leaves the rest", "a gift card cannot pay for a gift card"], ["a 30 EUR card on a 50 EUR order leaves 20 EUR to pay"]),
        new("2026-11-18-b5d1d", "giftcard-refund",
            "Refunding an order paid by gift card puts the amount back on the card.", "shop-api",
            ["refund path for the gift card tender", "tests"], ["a refund restores the card balance"]),
    ];

    private static GraphTicket Graph(string key, string title, string requirement, string[] requires, string[] repos, DemoSpec[] specs) =>
        new(key, title, requirement, [.. specs.SelectMany(s => s.Done)], requires, repos)
        {
            Specs = [.. specs.Select(s => new SpecDraft(s.Id, s.Goal, SpecYaml(s, key), []) { Done = s.Done })],
        };

    // ── the filed set, running as a series ───────────────────────────────────────────────

    private static async Task SeriesStoryAsync(IServiceProvider sp, SpecDialogSessionManager sessions, StoryClock clock, CancellationToken ct)
    {
        clock.Now = Anchor.AddMinutes(95);
        _currentThread = SeriesConversation;
        await sessions.OpenAsync(Platform, Platform, SeriesConversation, Owner,
            new ActiveScope { Project = "shop", Repos = ["shop-api", "shop-web"] }, ct);
        await SayAsync(sessions, clock, TranscriptRole.User,
            "Two things from the support inbox: people lose their cart when they log in on another device, and they want to reorder a past order in one click. Also send a reminder if something sits in 'saved for later' for a week.", 0);
        await SayAsync(sessions, clock, TranscriptRole.Assistant,
            "Three tickets: saved items that survive a new session, one-click reorder, and the reminder email, which needs the saved items first. Each carries its reviewed specs.\n\n```yaml\ntickets: [saved-items, reorder, saved-reminder]\n```", 5, kind: SpecDialogTurnKind.TicketGraphProposal);
        await SayAsync(sessions, clock, TranscriptRole.User, "File them and run them as a series.", 9);
        var state = await StateAsync(sessions, SeriesConversation, ct);

        var filed = new TicketGraphOutcome(
        [
            Graph("saved-items", "Saved for later: keep cart items across sessions", "Saved items survive a new session.", [], ["shop-api", "shop-web"], SavedSpecs),
            Graph("reorder", "Reorder: one click from the order history", "Reorder a past order in one click.", [], ["shop-api", "shop-web"], ReorderSpecs),
            Graph("saved-reminder", "Saved for later: reminder email after 7 days", "Remind the customer of items saved for a week.", ["saved-items"], ["shop-api"], ReminderSpecs),
        ]);
        clock.Now = Anchor.AddMinutes(105);
        var outcomes = sp.GetRequiredService<SpecDialogLatestOutcomeStore>();
        await outcomes.SetFilingAsync(Platform, SeriesConversation, new FilingReport(
        [
            Filed("44", "Saved for later: keep cart items across sessions"),
            Filed("45", "Reorder: one click from the order history"),
            Filed("46", "Saved for later: reminder email after 7 days"),
        ], null), filed, ct);

        var store = sp.GetRequiredService<ISeriesRunStore>();
        var seriesId = $"series-{state.JobId}";
        await store.OpenAsync(new SeriesRunRecord(seriesId, state.JobId, "shop", Owner, true, SeriesRunStates.Running,
        [
            new SeriesTicket("44", "github-44", "Saved for later: keep cart items across sessions", false, Anchor.AddMinutes(105)),
            new SeriesTicket("45", "github-45", "Reorder: one click from the order history", false, Anchor.AddMinutes(105)),
            new SeriesTicket("46", "github-46", "Saved for later: reminder email after 7 days", false, Anchor.AddMinutes(105)),
        ], Anchor.AddMinutes(106))
        { MasterCostUsd = 0.34m, LastStatusAt = DateTimeOffset.UtcNow.AddMinutes(-6) }, ct);

        var series = sp.GetRequiredService<AgentSmith.Contracts.Series.ISeriesConversation>();
        clock.Now = Anchor.AddMinutes(107);
        await series.SayAsync(state.JobId,
            "Filed 3 tickets. #44, #45 started; 1 wait on their prerequisites and start as soon as the ticket they need has a green push. "
            + "I merge each one when its CI is green; a red CI goes back to its run with the failing check and report here on every change and every 15 minutes.", ct);
        clock.Now = Anchor.AddMinutes(171);
        await series.SayAsync(state.JobId,
            "#44 is merged: CI green on acme/shop-api#58, both specs met. #46 waited on it and can start once #45's push is green; #45 is in its second spec.", ct);
        clock.Now = DateTimeOffset.UtcNow.AddMinutes(-6);
        await series.SayAsync(state.JobId,
            $"Status at {clock.Now.UtcDateTime:HH:mm} UTC.\n\n"
            + "| ticket | what | state | cost | time |\n|---|---|---|---|---|\n"
            + "| #44 | Saved for later: keep cart items across sessions | merged, CI green | $1.84 | 34m |\n"
            + "| #45 | Reorder: one click from the order history | running · spec 2 of 2 | $0.62 | 1h 12m |\n"
            + "| #46 | Saved for later: reminder email after 7 days | waits for #45 | $0.00 | – |\n\n"
            + "1 running · 1 landed · 1 held · $2.80 so far", ct);
    }

    private static readonly DemoSpec[] ReminderSpecs =
    [
        new("2026-11-18-9c4ff", "saved-items-reminder",
            "A customer with items saved for 7 days gets one reminder email listing them.", "shop-api",
            ["daily job", "email template", "tests"], ["one email per customer per week at most"]),
    ];

    private static FiledTicket Filed(string id, string title) =>
        new($"https://github.com/acme/shop/issues/{id}", title) { TicketId = id, Project = "shop", Key = $"github-{id}" };

    // ── the admin assistant, with a drafted change ───────────────────────────────────────

    private static async Task AdminStoryAsync(IServiceProvider sp, SpecDialogSessionManager sessions,
        SpecDialogProposalState proposals, StoryClock clock, CancellationToken ct)
    {
        clock.Now = DateTimeOffset.UtcNow.AddMinutes(-48);
        _currentThread = AdminConversation;
        await sessions.OpenAsync(Platform, Platform, AdminConversation, Owner,
            new ActiveScope { Project = "@agent-smith", Repos = [] }, ct);
        await SayAsync(sessions, clock, TranscriptRole.User,
            "billing should merge its pull requests the same way shop does, once CI is green. Keep branch protection on.", 0);
        var store = sp.GetRequiredService<IConfigStore>();
        var kinds = sp.GetRequiredService<ConfigEntityKinds>();
        var before = kinds.Read(store, "projects", "billing") ?? "{}";
        var after = JsonNode.Parse(before)!.AsObject();
        var shopMerge = JsonNode.Parse(kinds.Read(store, "projects", "shop") ?? "{}")!["merge"]!.DeepClone().AsObject();
        shopMerge["respectBranchProtection"] = true;
        after["merge"] = shopMerge;
        await SayAsync(sessions, clock, TranscriptRole.Assistant,
            "I read projects/shop and projects/billing. shop merges on green CI with up to 3 fix runs; billing has no merge block, so its pull requests stay open. "
            + "The change below gives billing the same block and keeps branch protection respected, so a required review still holds the merge. Apply runs it through the Config studio's validators.\n\n```yaml\nchange: projects/billing\n```", 2,
            kind: SpecDialogTurnKind.ConfigChangeProposal);
        var change = new ConfigChangeOutcome([new ConfigChangeItem(ConfigChangeItem.Upsert, "projects", "billing",
            after.ToJsonString(new JsonSerializerOptions { WriteIndented = false }), before)]);
        await proposals.StoreAwaitingAsync(await StateAsync(sessions, AdminConversation, ct), change, ct);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────

    private static async Task SayAsync(SpecDialogSessionManager sessions, StoryClock clock, TranscriptRole role, string text,
        int minutesLater, SpecDialogTurnKind? kind = null)
    {
        clock.Now = clock.Now.AddMinutes(minutesLater == 0 ? 0 : 1);
        var thread = _currentThread ?? throw new InvalidOperationException("no conversation open");
        await sessions.AppendTurnAsync(Platform, thread, role, text, kind ?? (role == TranscriptRole.Assistant ? SpecDialogTurnKind.Answer : null),
            null, CancellationToken.None, author: role == TranscriptRole.User ? Sam : null);
    }

    private static string? _currentThread;

    private static async Task<ConversationState> StateAsync(SpecDialogSessionManager sessions, string thread, CancellationToken ct) =>
        await sessions.GetOpenByThreadAsync(Platform, thread, ct) ?? throw new InvalidOperationException($"{thread} is not open");
}
