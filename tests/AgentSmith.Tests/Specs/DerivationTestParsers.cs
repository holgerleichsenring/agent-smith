using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;

namespace AgentSmith.Tests.Specs;

/// <summary>The real derivation parser over the real validator, renderer and reader —
/// the composition a test wants when it asserts what the reply is turned INTO.</summary>
internal static class DerivationTestParsers
{
    public static SpecDerivationParser Real()
    {
        var envelope = new SpecDerivationEnvelope();
        return new SpecDerivationParser(
            new DerivedSpecSequence(
                new DerivedSpecBuilder(
                    new SpecDraftValidator(new SpecSchemaProvider()), new SpecDraftReader(),
                    new DerivedSpecYamlRenderer(), envelope, new FactResolver()),
                new DerivedSpecRepos(envelope, new SpecReposRule())),
            envelope, AgentSmith.Tests.TestSupport.SeriesContracts.Reader());
    }
}
