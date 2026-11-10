using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-10-864f: a run carries an approved upload as files inside its carrying repository; its review reads them
/// under the same <c>reference:&lt;name&gt;</c> address the design turn cited them by.
/// </summary>
public sealed class ReviewReferencesTests
{
    [Fact]
    public async Task ReviewReferences_CarriedSet_ResolvesACitationAtItsCopy()
    {
        var repository = new EvidenceTreeSandbox(new Dictionary<string, string>
        {
            [".agentsmith/references/s1/spec-doc/prd.md"] = "1\n2\n3\n",
        });
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
        {
            ["repo-a"] = repository,
        });
        pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets,
            [new CarriedReferenceSet("s1", "requirements", "reference:requirements", "repo-a", ".agentsmith/references/s1", "c1", 1)]);

        var references = ReviewReferences.For(pipeline);
        var probe = new SandboxEvidenceProbe(new Dictionary<string, ISandbox> { ["repo-a"] = repository },
            TurnActivityRecorder.Silent(), references);
        var problems = await new EvidenceCheck(new EvidenceReferences()).CheckAsync(
            "reference:requirements/spec-doc/prd.md:3", EvidencePolicy.Product, probe, CancellationToken.None);

        references.Keys.Should().Equal(["reference:requirements"]);
        problems.Should().BeEmpty("the citation resolves in the carried copy");
        repository.Ran.Should().Contain(step => step.Path == "/work/.agentsmith/references/s1/spec-doc/prd.md");
    }
}
