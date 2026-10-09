using System.Reflection;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-09-7f48: the phase gate runs this assembly as several processes, chosen by the
/// <c>TestProcess</c> trait, because a test filter sees traits and not collections.
/// <para>
/// A collection is what keeps a process-wide resource safe: the environment-mutating classes
/// share one and run one after another, the classes in a collection without parallelisation
/// run alone. A process is the stronger fence — environment variables are per process — so
/// the environment collection can be split over three processes that run at once, and the
/// collections that run alone get a process of their own. The trait and the collection must
/// therefore say the same thing, and a class that names one without the other FAILS here:
/// left untraited, an environment class would land in the body process beside nothing that
/// fences it, which is exactly the overlap the collection exists to prevent.
/// </para>
/// <para>
/// The body process is the complement — every class carrying no <c>TestProcess</c> trait — so
/// a class nobody tagged is still run, by the process with no fence to break.
/// </para>
/// </summary>
public sealed class TestProcessTraitRuleTests
{
    internal const string TraitName = "TestProcess";
    internal const string Serial = "serial";
    internal static readonly string[] EnvShards = ["env-1", "env-2", "env-3"];

    [Fact]
    public void TestProcessTrait_EveryShardedCollectionClass_CarriesExactlyOneMatchingTrait()
    {
        var types = typeof(TestProcessTraitRuleTests).Assembly.GetTypes()
            .Where(t => t.DeclaringType != typeof(TestProcessTraitRuleTests));

        Violations(types).Should().BeEmpty(
            "the gate selects processes by this trait; see TestProcessTraitRuleTests");
    }

    [Fact]
    public void TestProcessTrait_TheValuesInUse_AreExactlyTheGatesFourProcesses()
    {
        // phase-gate.sh names these four values in its filters; a fifth would run in the body
        // (the complement), and a value no class carries is a process the gate starts for nothing.
        var used = typeof(TestProcessTraitRuleTests).Assembly.GetTypes()
            .Where(t => t.DeclaringType != typeof(TestProcessTraitRuleTests))
            .SelectMany(t => t.GetCustomAttributesData())
            .Where(a => a.AttributeType == typeof(TraitAttribute)
                && (string)a.ConstructorArguments[0].Value! == TraitName)
            .Select(a => (string)a.ConstructorArguments[1].Value!)
            .ToHashSet();

        used.Should().BeEquivalentTo([.. EnvShards, Serial]);
    }

    [Fact]
    public void TestProcessTrait_AnUntraitedEnvironmentClass_Fails()
    {
        Violations([typeof(UntraitedEnvironmentClass)]).Should().ContainSingle()
            .Which.Should().Contain(nameof(UntraitedEnvironmentClass));
    }

    [Fact]
    public void TestProcessTrait_AnUntraitedSerialClass_Fails()
    {
        Violations([typeof(UntraitedSerialClass)]).Should().ContainSingle();
    }

    [Fact]
    public void TestProcessTrait_ATraitOutsideThoseCollections_Fails()
    {
        Violations([typeof(TraitWithoutCollection)]).Should().ContainSingle();
    }

    [Fact]
    public void TestProcessTrait_TheWrongShardKind_Fails()
    {
        Violations([typeof(EnvironmentClassMarkedSerial), typeof(TwoShards)]).Should().HaveCount(2);
    }

    [Fact]
    public void TestProcessTrait_MatchingTraits_Pass()
    {
        Violations([typeof(TracedEnvironmentClass), typeof(TracedSerialClass)]).Should().BeEmpty();
    }

    internal static IReadOnlyList<string> Violations(IEnumerable<Type> types)
    {
        var serialCollections = typeof(TestProcessTraitRuleTests).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributesData())
            .Where(a => a.AttributeType == typeof(CollectionDefinitionAttribute)
                && a.NamedArguments.Any(n => n.MemberName == nameof(CollectionDefinitionAttribute.DisableParallelization)
                    && n.TypedValue.Value is true))
            .Select(a => (string)a.ConstructorArguments[0].Value!)
            .ToHashSet();

        var violations = new List<string>();
        foreach (var type in types)
        {
            var collection = type.GetCustomAttributesData()
                .Where(a => a.AttributeType == typeof(CollectionAttribute))
                .Select(a => (string)a.ConstructorArguments[0].Value!)
                .FirstOrDefault();
            var traits = type.GetCustomAttributesData()
                .Where(a => a.AttributeType == typeof(TraitAttribute)
                    && (string)a.ConstructorArguments[0].Value! == TraitName)
                .Select(a => (string)a.ConstructorArguments[1].Value!)
                .ToList();

            string[]? expected = collection == TestSupport.EnvVarCollection.Name ? EnvShards
                : collection is not null && serialCollections.Contains(collection) ? [Serial]
                : null;

            if (expected is null)
            {
                if (traits.Count > 0)
                    violations.Add($"{type.Name}: carries {TraitName}={string.Join(",", traits)} but its collection '{collection}' is not sharded");
                continue;
            }
            if (traits.Count != 1 || !expected.Contains(traits[0]))
                violations.Add($"{type.Name}: in collection '{collection}' needs exactly one {TraitName} of [{string.Join(", ", expected)}], has [{string.Join(", ", traits)}]");
        }
        return violations;
    }

    [Collection(TestSupport.EnvVarCollection.Name)]
    private sealed class UntraitedEnvironmentClass;

    [Collection(ExternalProcessCollection.Name)]
    private sealed class UntraitedSerialClass;

    [Trait(TraitName, "env-1")]
    private sealed class TraitWithoutCollection;

    [Trait(TraitName, Serial)]
    [Collection(TestSupport.EnvVarCollection.Name)]
    private sealed class EnvironmentClassMarkedSerial;

    [Trait(TraitName, "env-1")]
    [Trait(TraitName, "env-2")]
    [Collection(TestSupport.EnvVarCollection.Name)]
    private sealed class TwoShards;

    [Trait(TraitName, "env-2")]
    [Collection(TestSupport.EnvVarCollection.Name)]
    private sealed class TracedEnvironmentClass;

    [Trait(TraitName, Serial)]
    [Collection(ExternalProcessCollection.Name)]
    private sealed class TracedSerialClass;
}
