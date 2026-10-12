using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-3a36b: the second reading of a contradiction, through the real function-invoking
/// client over a look on its own terms. The claim this exists for was made from one grep line;
/// a read of the file it names says otherwise, and only a verdict citing that read refutes.
/// </summary>
public sealed class ContradictionCheckerTests
{
    private const string RequestPath = "Server/SubmitAssetsRequest.cs";

    private static readonly SpecHandback Claim = new(
        SpecHandbackCase.RequirementsContradictRepository,
        "SubmitAssetsRequest contains only TruckPlate and TrailerPlate");

    [Fact]
    public async Task ContradictionChecker_ReasonContradictedByFile_Refutes()
    {
        var provider = new ReadingProvider(RequestPath,
            """[{"verdict": "refuted", "why": "the request also declares ShipmentId", "cites": "C1"}]""");

        var check = await Check(provider);

        check.IsRefuted.Should().BeTrue();
        check.Evidence.Should().StartWith("[C1] ").And.Contain($"read {RequestPath}");
        provider.Prompt.Should().Contain(Claim.Reason).And.Contain("[L1] ");
    }

    [Fact]
    public async Task ContradictionChecker_RefutationCitingNoOwnLook_LetsTheClaimStand()
    {
        var provider = new ReadingProvider(RequestPath,
            """[{"verdict": "refuted", "why": "I believe it is wrong", "cites": "L1"}]""");

        var check = await Check(provider);

        check.IsRefuted.Should().BeFalse("an L-id is the deriver's look, not one this check took");
    }

    [Fact]
    public async Task ContradictionChecker_UnreadableAnswer_LetsTheClaimStand()
    {
        var check = await Check(new ReadingProvider(RequestPath, "no json here"));

        check.IsRefuted.Should().BeFalse();
        check.Why.Should().Contain("nothing readable");
    }

    private static async Task<ContradictionCheck> Check(ReadingProvider provider)
    {
        var files = new InMemorySandboxFileReader();
        files.Files[$"/work/{RequestPath}"] = "record SubmitAssetsRequest(string TruckPlate, string ShipmentId);";
        var look = new DerivationLook(
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) },
            new FixedReaderFactory(files), new PackageEcosystemDetector(), NullLogger.Instance,
            terms: DerivationLookTerms.ContradictionCheck);
        var cited = new EvidenceLook("L1", Repo, "grep SubmitAssetsRequest", 0, true,
            $"[L1] {Repo}: the derivation ran 'grep SubmitAssetsRequest' exited 0", EvidenceRecord.Search);
        var checker = new ContradictionChecker(
            new CutReviewTestDoubles.CappingFactory(provider), new AsyncLocalRunContextAccessor(),
            NullLogger<ContradictionChecker>.Instance);
        return await checker.CheckAsync(Claim, [cited], look, "azdo-1", new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);
    }

    /// <summary>Reads one file, then answers.</summary>
    private sealed class ReadingProvider(string path, string answer) : IChatClient
    {
        private int _turn;

        public string Prompt { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            if (_turn++ == 0)
            {
                Prompt = messages.First(m => m.Role == ChatRole.User).Text;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent("read-1", RepositoryFileReadTool.Name,
                        new Dictionary<string, object?> { ["repository"] = Repo, ["path"] = path }),
                ])));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
