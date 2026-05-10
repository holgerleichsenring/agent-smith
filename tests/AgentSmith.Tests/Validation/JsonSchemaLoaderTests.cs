using AgentSmith.Application.Services.Validation;
using FluentAssertions;
using Json.Schema;

namespace AgentSmith.Tests.Validation;

public sealed class JsonSchemaLoaderTests
{
    [Fact]
    public void Load_AllSchemas_ParseSuccessfully()
    {
        var loader = new JsonSchemaLoader();

        loader.Get(SkillOutputSchema.Plan).Should().NotBeNull();
        loader.Get(SkillOutputSchema.Diff).Should().NotBeNull();
        loader.Get(SkillOutputSchema.Bootstrap).Should().NotBeNull();
        loader.Get(SkillOutputSchema.Observation).Should().NotBeNull();
    }

    [Fact]
    public void Load_PlanSchemaEnforcesStatusOpenQuestionsConstraint()
    {
        var loader = new JsonSchemaLoader();
        var schema = loader.Get(SkillOutputSchema.Plan);

        var rejected = SchemaValidator.Validate(
            PlanFixtures.Build(status: "complete", openQuestionCount: 2),
            schema, "plan");

        rejected.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Load_DiffSchemaRejectsUnknownOperation()
    {
        var loader = new JsonSchemaLoader();
        var schema = loader.Get(SkillOutputSchema.Diff);
        var bad = """
        {"changes":[{"file":"a","operation":"weird","summary":"s","patch":"p"}],
         "tests_added":[], "tests_modified":[], "build_status":"ok", "test_status":"ok"}
        """;

        var result = SchemaValidator.Validate(bad, schema, "diff");

        result.IsValid.Should().BeFalse();
    }
}
