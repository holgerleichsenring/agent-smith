using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0422: the CUT is reviewed before a token is spent building it.
/// <para>
/// Ticket 19106 was cut into one phase carrying both the ticket's own "Step 1 —
/// Inventory (before touching any code)" and its migration steps, so the phase demanded
/// "no production source file is modified" AND "MediatR appears nowhere". Every shape
/// rule passed. The master worked for two hours, found the contradiction itself and
/// parked the run on a question the operator should never have had to answer.
/// </para>
/// </summary>
public sealed class SpecCutReviewTests
{
    private const string Contradiction = """
        [{"phase_id":"p19106a","criterion":"No production source file has been modified",
          "problem":"contradiction","why":"the migration criteria require modifying source",
          "conflicts_with":"MediatR appears nowhere in the sources"}]
        """;

    [Fact]
    public async Task APhaseWhoseCriteriaContradict_IsReportedAsNotDeliverable()
    {
        var review = await Review(Contradiction);

        review.Deliverable.Should().BeFalse();
        review.Findings.Should().ContainSingle()
            .Which.ConflictsWith.Should().Be("MediatR appears nowhere in the sources");
    }

    [Fact]
    public async Task ADeliverableCut_PassesWithoutComment()
    {
        var review = await Review("[]");

        review.Deliverable.Should().BeTrue();
    }

    /// <summary>
    /// The same anti-invention rule the delivery account has: a finding must quote a
    /// criterion the phase really states. A reviewer that invents its objection blocks a
    /// cut nobody can find the fault in, which is worse than no review at all.
    /// </summary>
    [Fact]
    public async Task AFindingQuotingACriterionThePhaseNeverStated_IsDiscarded()
    {
        var review = await Review("""
            [{"phase_id":"p19106a","criterion":"the moon is a balloon",
              "problem":"uncheckable","why":"invented"}]
            """);

        review.Deliverable.Should().BeTrue("a fault nobody can point at is not a fault");
    }

    [Fact]
    public async Task AReviewThatCouldNotBeTaken_BlocksNothing()
    {
        var review = await Review("I could not read the phases.");

        review.Deliverable.Should().BeTrue(
            "a failed review is not evidence of a fault — it must not block a cut");
        review.Problem.Should().NotBeNull();
    }

    [Fact]
    public async Task ThePromptCarriesTheCriteriaAndTheTicket()
    {
        var client = new RecordingChatClient("[]");
        await Reviewer(client).ReviewAsync(
            Set(), "Step 1 — Inventory (before touching any code)", new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

        var prompt = client.Prompts.Single();
        prompt.Should().Contain("No production source file has been modified");
        prompt.Should().Contain("before touching any code");
        prompt.Should().Contain("CANNOT BE DELIVERED",
            "asked adversarially, because 'the cut is fine' is the cheap answer");
    }

    private static async Task<SpecCutReview> Review(string answer) =>
        await Reviewer(new RecordingChatClient(answer)).ReviewAsync(
            Set(), "the ticket", new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

    private static SpecCutReviewer Reviewer(IChatClient client) =>
        new(new SingleClientFactory(client),
            new AsyncLocalRunContextAccessor(),
            NullLogger<SpecCutReviewer>.Instance);

    private static SpecSet Set() =>
        new("azuredevops-19106",
            [new SpecPhase(
                new PhaseDraft("p19106a", "migrate the libraries", "goal: migrate", [])
                {
                    Done = [
                        "No production source file has been modified",
                        "MediatR appears nowhere in the sources",
                    ],
                },
                "migrate", "# migrate", [])],
            SpecAccounting.Empty, [], SpecSource.Derived);

    private sealed class SingleClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }

    private sealed class RecordingChatClient(string answer) : IChatClient
    {
        public List<string> Prompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
