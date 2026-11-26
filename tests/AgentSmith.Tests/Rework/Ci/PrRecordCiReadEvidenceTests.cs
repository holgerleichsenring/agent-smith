using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Ci;

/// <summary>
/// 2026-11-26-49d3a: a head already stored red is read again without its logs; the evidence its first read stored stands, so
/// the series master's ci read, which refreshes first, never loses the failing test.
/// </summary>
public sealed class PrRecordCiReadEvidenceTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
    private const string Url = "https://github.com/example/sample-server/pull/7";
    private static readonly CiFailure Stored = new("PR build", "https://ci.example/812", "test failed: OrderImportTests.Imports_the_batch");
    private readonly Mock<IPullRequestCiReader> _host = new();

    [Fact]
    public async Task ARedHeadReadAgain_KeepsTheStoredEvidence()
    {
        Host(CiStates.Red, "abc", new CiFailure("PR build", "https://ci.example/812", "names only"));
        var record = Record("abc");

        await Read(record, StoredRed("abc"));

        PrRecordCiJson.View(record)!.Failures.Should().Equal(Stored);
        _host.Verify(h => h.ReadCiAsync(Url, It.Is<CiReadRequest>(r => r.FailuresRead == 0), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ARerunOnTheSameHead_KeepsOnlyTheChecksStillFailing()
    {
        var lint = new CiFailure("lint", null, "2 warnings treated as errors");
        Host(CiStates.Red, "abc", lint);
        var record = Record("abc");

        await Read(record, StoredRed("abc"));

        PrRecordCiJson.View(record)!.Failures.Should().Equal(lint);
    }

    [Fact]
    public async Task ANewHeadRed_StoresItsOwnRead()
    {
        var fresh = new CiFailure("PR build", "https://ci.example/813", "test failed: OtherTests.Breaks");
        Host(CiStates.Red, "def", fresh);
        var record = Record("def");

        await Read(record, StoredRed("abc"));

        PrRecordCiJson.View(record)!.Failures.Should().Equal(fresh);
    }

    private Task Read(PrReviewRecord record, PrReviewRecordView stored) =>
        new PrRecordCiRead(NullLogger<PrRecordCiRead>.Instance).ApplyAsync(new ResolvedProject { Name = "p" }, _host.Object, record, stored, default);

    private void Host(string state, string head, CiFailure failure) =>
        _host.Setup(h => h.ReadCiAsync(Url, It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestCi(state, head, T, [failure]));

    private static PrReviewRecord Record(string head) =>
        new() { Url = Url, State = PrReviewStates.Open, HeadSha = head, CheckedAtTicks = T.UtcTicks };

    private static PrReviewRecordView StoredRed(string head) =>
        new("k", Url, "api", "7", "p", "7", "r", PrReviewStates.Open, T, T, null, [])
        { HeadSha = head, Ci = new PrRecordCiView(CiStates.Red, head, T, [Stored]) };
}
