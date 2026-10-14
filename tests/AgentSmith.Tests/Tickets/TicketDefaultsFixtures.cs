using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-8762p: rows and calls the defaults endpoint tests share.</summary>
internal static class TicketDefaultsFixtures
{
    public const string R1 = "2026-10-13T09-00-00-0001";
    public const string R2 = "2026-10-13T10-00-00-0002";
    public const string Scan = "2026-10-13T11-00-00-0003";
    public const string DesignRun = "2026-10-13T08-00-00-0000";
    public const string Session = "s-1";

    public static RunDefaultDecision Default(
        long id, string runId, int minutes, bool holdsSeries = false, string verdict = DefaultDecisionVerdicts.Pending,
        string? conversation = null, string kind = DefaultDecisionKinds.Decision, string? specId = "2026-10-13-1234a") => new()
        {
            Id = id, RunId = runId, SpecId = specId, Question = $"Question {id}?", Chosen = $"choice {id}",
            AlternativesJson = "[\"other\"]", Why = $"why {id}", HoldsSeries = holdsSeries, Kind = kind,
            PointerPath = kind == DefaultDecisionKinds.MemoryProposal ? "shop/.agentsmith/memory/ask-first.md" : null,
            TakenAt = TicketStoreHarness.T.AddMinutes(minutes), Verdict = verdict, Conversation = conversation,
            VerdictBy = verdict == DefaultDecisionVerdicts.Pending ? null : "earlier",
            VerdictAt = verdict == DefaultDecisionVerdicts.Pending ? null : TicketStoreHarness.T.AddMinutes(minutes + 1),
        };

    public static HttpContext Context(bool enforce = false, string principal = "ana", params string[] permissions)
    {
        var auth = new TokenAuthorityConfig { Enforce = enforce };
        var services = new ServiceCollection().AddSingleton(auth).AddSingleton(ResolverUnderTest.With(auth));
        var claims = permissions.Select(p => (PermissionClaims.Type, p)).Append(("sub", principal)).ToArray();
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = ResolverUnderTest.Caller(auth, claims),
        };
    }

    public static async Task<TicketDefaultsView> ReadAsync(TicketStoreHarness store, HttpContext? ctx = null) =>
        (await TicketDefaultsEndpoints.DefaultsAsync("alpha", "42", ctx ?? Context(), store.Defaults, CancellationToken.None))
            .Should().BeOfType<Ok<TicketDefaultsView>>().Subject.Value!;

    public static Task<IResult> PostAsync(TicketStoreHarness store, string principal, params DefaultVerdictRequest[] items) =>
        TicketDefaultsEndpoints.VerdictsAsync("alpha", "42", [.. items], Context(principal: principal), store.Verdicts,
            CancellationToken.None);

    public static async Task<DefaultVerdictResult> DecideAsync(
        TicketStoreHarness store, string principal, params DefaultVerdictRequest[] items) =>
        (await PostAsync(store, principal, items)).Should().BeOfType<Ok<DefaultVerdictResult>>().Subject.Value!;

    public static string Refusal(IResult result) =>
        result.Should().BeOfType<BadRequest<string>>().Subject.Value!;

    public static DefaultVerdictRequest Ratify(long id, string? note = null) => new(id, DefaultDecisionVerdicts.Ratified, note);

    public static DefaultVerdictRequest Overrule(long id, string? note = null) => new(id, DefaultDecisionVerdicts.Overruled, note);

    public static Task SeedTwoAttemptsAsync(TicketStoreHarness store, params object[] rows) =>
        store.AddAsync([TicketStoreHarness.Run(R1, status: "failed"), TicketStoreHarness.Step(R1),
            TicketStoreHarness.Run(R2), TicketStoreHarness.Step(R2), .. rows]);
}
