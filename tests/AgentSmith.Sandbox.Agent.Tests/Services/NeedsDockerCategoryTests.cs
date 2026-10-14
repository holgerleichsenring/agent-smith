using System.Reflection;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// A sandbox has no Docker daemon, so the run's declared suite excludes Category NeedsDocker.
/// Every class of the Testcontainers collection must carry it, or the declared suite fails
/// with certainty in every sandbox.
/// </summary>
public sealed class NeedsDockerCategoryTests
{
    [Fact]
    public void RedisJobBusCollection_EveryClass_CarriesTheNeedsDockerCategory()
    {
        var members = typeof(NeedsDockerCategoryTests).Assembly.GetTypes()
            .Where(t => Arguments(t, typeof(CollectionAttribute))
                .Any(a => Equals(a[0], nameof(RedisJobBusCollection))))
            .ToList();

        members.Should().NotBeEmpty();
        var untagged = members
            .Where(t => !Arguments(t, typeof(TraitAttribute))
                .Any(a => Equals(a[0], "Category") && Equals(a[1], "NeedsDocker")))
            .Select(t => t.Name);
        untagged.Should().BeEmpty(
            "a sandbox has no Docker daemon and the run's declared suite excludes Category NeedsDocker");
    }

    private static IEnumerable<object?[]> Arguments(Type type, Type attribute) =>
        CustomAttributeData.GetCustomAttributes(type)
            .Where(d => d.AttributeType == attribute)
            .Select(d => d.ConstructorArguments.Select(a => a.Value).ToArray());
}
