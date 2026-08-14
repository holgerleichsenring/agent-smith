using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0420: the phase is judged by what the branch shows against its ratified criteria.
/// <para>
/// Run c96d was recorded FAILED — "this fix/feature run produced no code changes" —
/// while its branch carried nine updated project files, an adapted call site and a
/// green build in both repositories. The old gate measured the run's activity; this one
/// measures the delivery, and every "satisfied" has to point at a file the diff really
/// touched.
/// </para>
/// </summary>
public sealed class SpecAccountingTests
{
    private const string Diff = """
        diff --git a/src/Api/Api.csproj b/src/Api/Api.csproj
        --- a/src/Api/Api.csproj
        +++ b/src/Api/Api.csproj
        @@ -8,7 +8,7 @@
        -    <PackageReference Include="Sample.Messaging" Version="1.0.446" />
        +    <PackageReference Include="Sample.Messaging" Version="1.1.26" />
        diff --git a/src/Api/Extensions/SwaggerExtensions.cs b/src/Api/Extensions/SwaggerExtensions.cs
        --- a/src/Api/Extensions/SwaggerExtensions.cs
        +++ b/src/Api/Extensions/SwaggerExtensions.cs
        @@ -40,6 +40,7 @@
        +        options.UseNewApi();
        """;

    [Fact]
    public void DiffIndex_KnowsWhatTheBranchTouched_AndWhatItDidNot()
    {
        var index = new DiffFileIndex(Diff);

        index.Contains("src/Api/Api.csproj").Should().BeTrue();
        index.Contains("SwaggerExtensions.cs").Should().BeTrue("a reviewer cites the file, not the full path");
        index.Contains("SwaggerExtensions.cs:44").Should().BeTrue("a line number is still a citation of that file");
        index.Contains("src/Api/NeverTouched.cs").Should().BeFalse();
        index.Contains(null).Should().BeFalse();
    }

    [Fact]
    public async Task CriterionCitingAFileOutsideTheDiff_IsNotSatisfied()
    {
        var account = await Account(
            ["""[{"criterion":"packages updated","satisfied":true,"citation":"src/Api/Imagined.cs"}]"""],
            criteria: ["packages updated"]);

        account.Delivered.Should().BeFalse(
            "a criterion cannot be satisfied by a file the phase never touched");
        account.Outstanding.Should().ContainSingle()
            .Which.Note.Should().Contain("which the diff does not touch");
    }

    [Fact]
    public async Task CriterionWithARealCitation_IsSatisfied_AndKeepsThePointer()
    {
        var account = await Account(
            ["""[{"criterion":"packages updated","satisfied":true,"citation":"src/Api/Api.csproj","note":"1.0.446 → 1.1.26"}]"""],
            criteria: ["packages updated"]);

        account.Delivered.Should().BeTrue();
        account.Criteria.Should().ContainSingle()
            .Which.Citation.Should().Be("src/Api/Api.csproj");
    }

    [Fact]
    public async Task CriterionTheAccountNeverAddressed_CountsAsOutstanding()
    {
        var account = await Account(
            ["""[{"criterion":"packages updated","satisfied":true,"citation":"src/Api/Api.csproj"}]"""],
            criteria: ["packages updated", "call sites adapted"]);

        account.Delivered.Should().BeFalse("silence about a criterion is not agreement");
        account.Outstanding.Should().ContainSingle()
            .Which.Criterion.Should().Be("call sites adapted");
    }

    [Fact]
    public async Task AnUnreadableAccount_IsNotAPass()
    {
        var account = await Account(["I had a look and it all seems fine to me."],
            criteria: ["packages updated"]);

        account.Delivered.Should().BeFalse(
            "an account that could not be taken must never read as a delivery");
        account.Problem.Should().NotBeNull();
    }

    [Fact]
    public async Task ThePromptCarriesTheDiff_AndAsksWhatIsMissing()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText("""[{"criterion":"packages updated","satisfied":false}]""");
        await Accountant(client).AccountAsync(
            "sample-repo", ["packages updated"], Diff, new AgentConfig(), Tracker(), CancellationToken.None);

        var prompt = client.Prompts.Single();
        prompt.Should().Contain("Sample.Messaging", "the account is taken against the diff itself");
        prompt.Should().Contain("MISSING", "asked negatively, because 'all done' is the cheap answer");
    }

    private static async Task<SpecAccount> Account(
        IReadOnlyList<string> answers, IReadOnlyList<string> criteria)
    {
        var client = new ScriptedChatClient();
        foreach (var answer in answers) client.EnqueueText(answer);
        return await Accountant(client).AccountAsync(
            "sample-repo", criteria, Diff, new AgentConfig(), Tracker(), CancellationToken.None);
    }

    private static SpecAccountant Accountant(IChatClient client) =>
        new(new StubChatClientFactory(client),
            new AsyncLocalRunContextAccessor(),
            NullLogger<SpecAccountant>.Instance);

    private static PipelineCostTracker Tracker() =>
        PipelineCostTracker.GetOrCreate(new PipelineContext());

    private sealed class StubChatClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }

    /// <summary>Answers a scripted line and remembers what it was asked.</summary>
    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly Queue<string> answers = new();

        public List<string> Prompts { get; } = [];

        public void EnqueueText(string text) => answers.Enqueue(text);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
            var text = answers.Count > 0 ? answers.Dequeue() : "[]";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
