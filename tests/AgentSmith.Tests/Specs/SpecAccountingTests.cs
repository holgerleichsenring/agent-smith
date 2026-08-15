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
        // p0422, found in run 17: an account cites for a HUMAN, so it writes asides.
        // Refusing a real file over the parenthesis after it turns evidence into invention.
        index.Contains("src/Api/Api.csproj (both repositories)").Should().BeTrue();
        index.Contains("`SwaggerExtensions.cs`, lines 40-58").Should().BeTrue();
        index.Contains("(nothing in particular)").Should().BeFalse();
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
            .Which.Note.Should().Contain("neither in the diff nor a command that ran");
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

    /// <summary>
    /// p0421, found in run 13: every "the build exits 0" criterion came back OUTSTANDING
    /// with "no build log appears in the diff" — over builds that had actually gone green.
    /// A build result is not in a diff and never will be; the commands that RAN are the
    /// other half of the evidence, and a criterion they cover is answered by them.
    /// </summary>
    [Fact]
    public async Task ACriterionAboutABuild_IsAnsweredByTheCommandThatRan()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText(
            """[{"criterion":"the build exits 0","satisfied":true,"citation":"build 'dotnet build'"}]""");

        var account = await Accountant(client).AccountAsync(
            "sample-repo", ["the build exits 0"], Diff, Commands,
            new AgentConfig(), Tracker(), CancellationToken.None);

        account.Delivered.Should().BeTrue();
        account.Criteria.Single().Mechanical.Should().BeTrue(
            "a green build is evidence of a different kind, and the account says which");
    }

    [Fact]
    public async Task ACitationNamingNeitherAFileNorACommandThatRan_IsStillRefused()
    {
        var account = await Account(
            ["""[{"criterion":"the build exits 0","satisfied":true,"citation":"dotnet nonsense"}]"""],
            criteria: ["the build exits 0"]);

        account.Delivered.Should().BeFalse("invention stays refused on both halves of the evidence");
    }

    [Fact]
    public async Task ThePromptCarriesTheDiff_AndAsksWhatIsMissing()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText("""[{"criterion":"packages updated","satisfied":false}]""");
        await Accountant(client).AccountAsync(
            "sample-repo", ["packages updated"], Diff, Commands, new AgentConfig(), Tracker(), CancellationToken.None);

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
            "sample-repo", criteria, Diff, Commands, new AgentConfig(), Tracker(), CancellationToken.None);
    }

    /// <summary>
    /// p0422: 245 real worker calls in one day ranged from 3.7k to 5.2M characters, median
    /// 205k — the 60k cut I had put on the diff was 1.2% of what actually goes through, and
    /// cutting dropped whole repositories. A diff too large for one window is SPLIT at file
    /// boundaries, because evidence is monotone: what a window shows, it shows.
    /// </summary>
    [Fact]
    public void ADiffLargerThanAWindow_IsSplitAtFileBoundaries_NeverMidFile()
    {
        var big = string.Concat(Enumerable.Range(0, 6).Select(i =>
            $"diff --git a/src/File{i}.cs b/src/File{i}.cs\n--- a/src/File{i}.cs\n+++ b/src/File{i}.cs\n"
            + new string('x', 400) + "\n"));

        var windows = DiffWindows.Split(big, budgetChars: 900);

        windows.Should().HaveCountGreaterThan(1);
        windows.Should().OnlyContain(w => w.StartsWith("diff --git ", StringComparison.Ordinal),
            "a window begins at a file header — a hunk cut in half belongs to nobody");
        string.Concat(windows).Should().Be(big, "splitting loses nothing");
    }

    [Fact]
    public async Task ACriterionSatisfiedInOnlyOneWindow_CountsAsSatisfied()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText(
            """[{"criterion":"the greeting is localised","satisfied":false,"note":"not in this slice"}]""");
        client.EnqueueText(
            """[{"criterion":"the greeting is localised","satisfied":true,"citation":"src/Api/Api.csproj"}]""");

        var account = await Accountant(client).AccountAsync(
            "sample-repo", ["the greeting is localised"], TwoWindowDiff(), Commands,
            new AgentConfig(), Tracker(), CancellationToken.None);

        account.Delivered.Should().BeTrue(
            "evidence in one window is evidence — the windows are slices, not verdicts");
        client.Prompts.Should().HaveCount(2);
    }

    private static string TwoWindowDiff() =>
        Diff + "\n" + string.Concat(Enumerable.Range(0, 2000).Select(i =>
            $"diff --git a/src/Pad{i}.cs b/src/Pad{i}.cs\n--- a/src/Pad{i}.cs\n+++ b/src/Pad{i}.cs\n"
            + new string('y', 300) + "\n"));

    /// <summary>
    /// p0422, found in run 18 after 5.5 hours of work: a criterion about BOTH repositories
    /// is cited by both commands, joined in one string — and refusing it because no single
    /// command contains the whole citation turned a green build in each repo into an
    /// outstanding criterion. Every part must resolve; one real and one invented still fails.
    /// </summary>
    [Fact]
    public async Task ACitationNamingSeveralCommands_ResolvesWhenEachOneRan()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText(
            """
            [{"criterion":"the build exits 0 in both repositories","satisfied":true,
              "citation":"api: build 'dotnet build' exited 0; worker: build 'dotnet build' exited 0"}]
            """);

        var account = await Accountant(client).AccountAsync(
            "both", ["the build exits 0 in both repositories"], Diff,
            ["api: build 'dotnet build' exited 0", "worker: build 'dotnet build' exited 0"],
            new AgentConfig(), Tracker(), CancellationToken.None);

        account.Delivered.Should().BeTrue();
        account.Criteria.Single().Mechanical.Should().BeTrue();
    }

    [Fact]
    public async Task ACitationMixingARealCommandWithAnInventedOne_IsRefused()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText(
            """
            [{"criterion":"the build exits 0 in both repositories","satisfied":true,
              "citation":"api: build 'dotnet build' exited 0; worker: build 'never ran' exited 0"}]
            """);

        var account = await Accountant(client).AccountAsync(
            "both", ["the build exits 0 in both repositories"], Diff,
            ["api: build 'dotnet build' exited 0"],
            new AgentConfig(), Tracker(), CancellationToken.None);

        account.Delivered.Should().BeFalse("half a citation is not evidence for the whole claim");
    }

    private static readonly IReadOnlyList<string> Commands =
        ["sample-repo: build 'dotnet build' exited 0"];

    private static SpecAccountant Accountant(IChatClient client)
    {
        var factory = new StubChatClientFactory(client);
        return new SpecAccountant(
            factory,
            new SpecAccountCall(factory, new AsyncLocalRunContextAccessor(),
                NullLogger<SpecAccountCall>.Instance),
            NullLogger<SpecAccountant>.Instance);
    }

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
