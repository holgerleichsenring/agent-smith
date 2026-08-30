using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Surface;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.Verification;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Surface;

/// <summary>
/// 2026-08-30-c6ec: the operator's question — values changeable through the API although
/// only the UI should ever set them — is carried by no verification standard, because the
/// intent lives in the CLIENT. Its machine-readable form is a difference: what the served
/// description offers that no first-party client sends or calls.
/// <para>
/// The difference is a lower estimate of what is exercised, so these tests hold two lines
/// at once: what it reports, and what it refuses to report when the reading was partial,
/// the consumer unresolvable or an input missing.
/// </para>
/// </summary>
public sealed class SurfaceDifferenceTests
{
    private const string ServedInterface = "Orders";
    private const string ConsumerRepo = "storefront-web";
    private const string CatalogueVersion = "5.0";

    [Fact]
    public async Task Surface_AnOperationNoClientCalls_IsReportedAsUnexercised()
    {
        var report = await ComputeAsync(Usage(Call("POST /orders", ["customerId"], ["id"])));

        report.Computed.Should().BeTrue();
        report.Differences.Should().ContainSingle(d =>
            d.Kind == SurfaceDifferenceKind.UnexercisedOperation && d.Operation == "DELETE /orders/{id}")
            .Which.RequirementId.Should().Be(SurfaceRequirements.UnexercisedOperation,
                "an unexercised operation matters when function-level access to it is unrestricted");
        report.Differences.Should().NotContain(d =>
            d.Kind != SurfaceDifferenceKind.UnexercisedOperation && d.Operation == "DELETE /orders/{id}",
            "an operation nobody calls is one observation, not one per field it declares");
    }

    [Fact]
    public async Task Surface_APropertyAcceptedAndNeverSent_IsReportedAsOverExposed()
    {
        var report = await ComputeAsync(Usage(
            Call("POST /orders", ["customerId"], ["id"]),
            Call("DELETE /orders/{id}", [], [])));

        var overExposed = report.Differences.Should().ContainSingle(d =>
            d.Kind == SurfaceDifferenceKind.UnsentAcceptedProperty).Which;
        overExposed.Operation.Should().Be("POST /orders");
        overExposed.Property.Should().Be("discountPercent",
            "the body declares it, the client never sends it — the server is wider than its client");
        overExposed.RequirementId.Should().Be(SurfaceRequirements.UnsentAcceptedProperty);

        report.Differences.Should().ContainSingle(d =>
            d.Kind == SurfaceDifferenceKind.UnreadReturnedProperty && d.Property == "internalNote",
            "a field returned and never read is the other half of the same question");
    }

    [Fact]
    public async Task Surface_AClientFileThatCouldNotBeDecided_DegradesTheClaimNotTheExercisedSet()
    {
        var calls = new[] { Call("POST /orders", ["customerId", "discountPercent"], ["id", "internalNote"]) };
        var decided = await ComputeAsync(Usage(calls));
        var undecided = await ComputeAsync(new ClientUsageReport(
            calls,
            new ClientExtractionAccount(
                ["storefront-web/src/orders.ts", "storefront-web/src/generated.ts"],
                [new UndecidedClientFile("storefront-web/src/generated.ts", "the client is generated")],
                calls.Length)));

        undecided.Differences.Should().BeEquivalentTo(decided.Differences,
            "an undecided file may not narrow what the clients are known to exercise");
        undecided.Degraded.Should().BeTrue("the claim is bounded by what the reading could not decide");
        undecided.Account.FilesNotDecided.Should().ContainSingle()
            .Which.Why.Should().Be("the client is generated");
        decided.Degraded.Should().BeFalse();
    }

    [Fact]
    public async Task Surface_AnUnresolvableConsumerName_FailsTheRun()
    {
        var pipeline = Pipeline(consumes: "Invoices");

        var result = await RunAsync(pipeline, Usage(Call("POST /orders", [], [])));

        result.IsSuccess.Should().BeFalse(
            "a difference computed over a subset the operator did not choose reads as a clean bill");
        result.Message.Should().Contain("Invoices").And.Contain(ServedInterface);
        pipeline.TryGet<SurfaceDifferenceReport>(ContextKeys.SurfaceDifference, out _).Should().BeFalse(
            "a failed run states no difference at all");
    }

    [Fact]
    public async Task Surface_NoServedDescription_ReportsTheDifferenceAsNotComputed()
    {
        var pipeline = Pipeline(consumes: ServedInterface);
        pipeline.Remove(ContextKeys.SwaggerSpec);

        var result = await RunAsync(pipeline, Usage(Call("POST /orders", [], [])));

        result.IsSuccess.Should().BeTrue("a missing input is a stated reason, not a failure");
        var report = Report(pipeline);
        report.Computed.Should().BeFalse();
        report.NotComputedReason.Should().Contain("no served description");
        report.Differences.Should().BeEmpty("an empty difference and an uncomputed one must not read alike");
    }

    [Fact]
    public async Task Surface_NoConsumerDeclared_ReportsTheDifferenceAsNotComputed()
    {
        var pipeline = Pipeline(consumes: null);

        await RunAsync(pipeline, Usage(Call("POST /orders", [], [])));

        Report(pipeline).NotComputedReason.Should().Contain("no repository declares");
    }

    [Fact]
    public async Task Surface_TheReadingProducedNoReadableReport_ReportsTheDifferenceAsNotComputed()
    {
        var pipeline = Pipeline(consumes: ServedInterface);

        await RunAsync(pipeline, usage: null);

        Report(pipeline).NotComputedReason.Should().Contain("no readable report",
            "silence from the reading is not a claim that the clients call nothing");
    }

    [Fact]
    public async Task Surface_ADifference_IsRecordedAsAnObservationNotRaisedAsAFinding()
    {
        var pipeline = Pipeline(consumes: ServedInterface);

        var result = await RunAsync(pipeline, Usage(Call("POST /orders", ["customerId"], ["id"])));

        result.IsSuccess.Should().BeTrue();
        Report(pipeline).Differences.Should().NotBeEmpty();
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out _).Should().BeFalse(
            "a difference is evidence; the finding is the pairing with a requirement somebody decided");
        SurfaceDifferencePromptSection.Render(pipeline).Should()
            .Contain("LOWER estimate", "the reviewer must see the bound before the entries")
            .And.Contain(SurfaceRequirements.UnexercisedOperation,
                "each entry names the requirement that would decide whether it matters");
    }

    [Fact]
    public async Task Surface_TheReadingIsAskedAboutTheDeclaredConsumerCheckouts()
    {
        var pipeline = Pipeline(consumes: ServedInterface);
        var reader = new StubClientSurfaceReader(Usage(Call("POST /orders", [], [])));

        await Handler(reader).ExecuteAsync(
            new AccountSurfaceDifferenceContext(pipeline, new AgentConfig()), CancellationToken.None);

        reader.Asked.Should().NotBeNull();
        reader.Asked!.ConsumerRepos.Should().Equal(ConsumerRepo);
        reader.Asked.DefaultKey.Should().Be(ConsumerRepo,
            "an unprefixed path must land in a consumer's checkout, not the served repo's");
        reader.Asked.Served.Should().HaveCount(2);
    }

    private static async Task<SurfaceDifferenceReport> ComputeAsync(ClientUsageReport usage)
    {
        var pipeline = Pipeline(consumes: ServedInterface);
        await RunAsync(pipeline, usage);
        return Report(pipeline);
    }

    private static async Task<CommandResult> RunAsync(PipelineContext pipeline, ClientUsageReport? usage) =>
        await Handler(new StubClientSurfaceReader(usage)).ExecuteAsync(
            new AccountSurfaceDifferenceContext(pipeline, new AgentConfig()), CancellationToken.None);

    private static AccountSurfaceDifferenceHandler Handler(StubClientSurfaceReader reader) =>
        new(new ServedSurfaceReader(), reader, new SurfaceDifferenceCalculator(),
            new StubVerificationCatalogue(CatalogueVersion, []),
            NullLogger<AccountSurfaceDifferenceHandler>.Instance);

    private static SurfaceDifferenceReport Report(PipelineContext pipeline) =>
        pipeline.Get<SurfaceDifferenceReport>(ContextKeys.SurfaceDifference);

    private static ClientCallSite Call(string operation, string[] sends, string[] reads) =>
        new($"{ConsumerRepo}/src/orders.ts", operation, sends, reads);

    private static ClientUsageReport Usage(params ClientCallSite[] callSites) =>
        new(callSites, new ClientExtractionAccount(
            [$"{ConsumerRepo}/src/orders.ts"], [], callSites.Length));

    private static PipelineContext Pipeline(string? consumes)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SwaggerSpec, OrdersSpec.Spec());
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = ConsumerRepo, Consumes = consumes }]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { [ConsumerRepo] = new StubSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { [ConsumerRepo] = ConsumerRepo });
        return pipeline;
    }
}
