using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0267: the discrete CollectMasterFindings step routes the api-security-master's
/// TRIAGED observation-array answer into ContextKeys.SkillObservations (the channel
/// DeliverFindings reads). Gated on the master's declared output_schema == observation
/// so a coding master (code + verdict) is never scraped as findings.
/// </summary>
public sealed class CollectMasterFindingsHandlerTests
{
    private const string Master = "api-security-master";

    [Fact]
    public async Task CollectMasterFindings_ObservationJsonAnswer_AppendsSkillObservations()
    {
        var answer = """
            [
              {"concern":"security","severity":"high","category":"authz",
               "description":"GET /orders/{id}: IDOR — id is not ownership-checked","api_path":"/orders/{id}",
               "evidence_mode":"potential","suggestion":"Check ownership before returning the order."},
              {"concern":"security","severity":"medium","category":"config",
               "description":"CORS allows any origin on /admin","api_path":"/admin",
               "evidence_mode":"potential","suggestion":"Restrict allowed origins."}
            ]
            """;
        var pipeline = PipelineWith(Master, answer);

        var result = await Build("observation").ExecuteAsync(
            new CollectMasterFindingsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs)
            .Should().BeTrue();
        obs!.Should().HaveCount(2);
        obs.Should().Contain(o => o.Severity == ObservationSeverity.High && o.ApiPath == "/orders/{id}");
        obs.Should().OnlyContain(o => o.Concern == ObservationConcern.Security);
    }

    [Fact]
    public async Task CollectMasterFindings_NoParseableJson_AppendsNothing_DeliversZeroHonestly()
    {
        var pipeline = PipelineWith(Master, "I reviewed the scanners and found no actionable issues.");

        var result = await Build("observation").ExecuteAsync(
            new CollectMasterFindingsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out _)
            .Should().BeFalse("a non-JSON answer must not invent a fallback finding");
    }

    [Fact]
    public async Task CollectMasterFindings_CodingMasterOutputSchema_SkipsScrape()
    {
        // A coding master emits code + verdict (output_schema diff), not observations.
        // Even when its answer happens to contain a JSON array, the gate must skip it.
        var answer = """[{"concern":"security","severity":"high","description":"should not be scraped"}]""";
        var pipeline = PipelineWith("coding-agent-master", answer);

        var result = await Build("diff").ExecuteAsync(
            new CollectMasterFindingsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out _)
            .Should().BeFalse("only output_schema == observation masters are scraped");
    }

    private static PipelineContext PipelineWith(string masterSkill, string answer)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterSkillName, masterSkill);
        pipeline.Set(ContextKeys.MasterAnswer, answer);
        return pipeline;
    }

    private static CollectMasterFindingsHandler Build(string? resolvedSchema) =>
        new(new StubSchemaResolver(resolvedSchema),
            TolerantJsonParserFactory.CreateObservation(),
            NullLogger<CollectMasterFindingsHandler>.Instance);

    private sealed class StubSchemaResolver(string? schema) : IMasterOutputSchemaResolver
    {
        public string? Resolve(string masterSkillName) => schema;
    }
}
