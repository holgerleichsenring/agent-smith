using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-18-b4f0: the work-item kind map's configuration surface — which tracker types
/// declare it, and what an unrelated save does to it.
/// </summary>
public sealed class WorkItemKindConfigTests
{
    /// <summary>
    /// The promise of "changes nothing until you choose". The default is resolved at the
    /// provider and must never be materialised into the operator's configuration: written back
    /// once, it would persist on the first unrelated save and turn an unset map into a set one.
    /// </summary>
    [Fact]
    public void Config_ASaveThatDidNotTouchTheMap_LeavesItUnset()
    {
        var stored = new RawTrackerEntry { Type = TrackerType.AzureDevOps, Project = "proj" };

        var patched = RawConfigPatch.Tracker(
            new TrackerEntity("t", "azure_devops", "AZURE_DEVOPS_TOKEN", Project: "proj")
            {
                DoneStatus = "Closed",
            },
            stored);

        patched.WorkItemKinds.Should().BeNull(
            "an entity that carries no map says 'leave the stored value alone', and there is none");
    }

    [Fact]
    public void Config_ASaveThatCarriesTheMap_StoresIt()
    {
        var patched = RawConfigPatch.Tracker(
            new TrackerEntity("t", "azure_devops", "AZURE_DEVOPS_TOKEN", Project: "proj")
            {
                WorkItemKinds = new Dictionary<string, string> { ["work"] = "Feature" },
            },
            new RawTrackerEntry { Type = TrackerType.AzureDevOps });

        patched.WorkItemKinds.Should().Equal(new Dictionary<string, string> { ["work"] = "Feature" });
    }

    /// <summary>
    /// Declared for the two trackers whose create sends a kind, and for no others — GitHub
    /// applies any status that is not open or closed as a label, GitLab accepts two state
    /// events. A field on their form would be a control that changes nothing.
    /// </summary>
    [Fact]
    public void Descriptor_TheKindMap_IsDeclaredOnlyForTheProvidersThatSendOne()
    {
        var capabilities = ConfigStudioCapabilities.Build(["claude"]);

        Declaring(capabilities).Should().BeEquivalentTo(["azure_devops", "jira"]);
        var field = capabilities.TrackerTypes.Single(t => t.Type == "jira")
            .Fields.Single(f => f.Key == "workItemKinds");
        field.Kind.Should().Be(CapabilityFieldKind.Map, "the lifecycle map beside it renders the same way");
        field.Required.Should().BeFalse(
            "Required is enforced on every upsert and would make every existing tracker unsaveable");
    }

    private static IReadOnlyList<string> Declaring(ConfigCapabilities capabilities) =>
        [.. capabilities.TrackerTypes
            .Where(t => t.Fields.Any(f => f.Key == "workItemKinds"))
            .Select(t => t.Type)];
}
