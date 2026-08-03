using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0392 — the guard. The tracker section read sixteen backend fields and the descriptor
/// declared four; the twelve it did not declare included needs_clarification_status, the
/// field whose absence refused a boot on 2026-07-31 and which could not be set in the UI
/// at all, so no amount of operator care would have prevented the outage.
///
/// The defect is not any one missing input — it is that a field could be ADDED to the
/// backend and stay invisible. This test walks the raw read models against the capabilities
/// descriptor, so the next added field fails until the descriptor declares it or the gap is
/// written down here with the phase that closes it.
/// </summary>
public sealed class CapabilityCoverageTests
{
    private static readonly ConfigCapabilities Capabilities = ConfigStudioCapabilities.Build(["claude"]);

    // Raw properties that are NOT form fields: the discriminator the type list already is,
    // and the contract-level blocks the form renders as their own section.
    private static readonly Dictionary<string, string> NotAField = new()
    {
        ["type"] = "the type list itself — every descriptor is keyed by it",
        ["polling"] = "a contract-level block, not per-type; the tracker form renders it as its own section",
    };

    // A raw property whose wire key differs from its camelCased name.
    private static readonly Dictionary<string, string> WireKey = new()
    {
        ["auth"] = "authSecret",   // the studio stores the env-NAME, never a value
        ["owner"] = "organization", // GitHub's org segment; the descriptor's orgLabel renames the label
        ["group"] = "organization", // GitLab's
    };

    [Fact]
    public void Capabilities_EveryBackendReadField_IsDeclared_ForTrackers()
    {
        var declared = Capabilities.TrackerTypes.SelectMany(t => t.Fields).Select(f => f.Key).ToHashSet();

        Undeclared<RawTrackerEntry>(declared).Should().BeEmpty(
            "a tracker field the backend reads but the descriptor does not declare cannot be "
            + "edited in the Config Studio, and its absence only shows up as a failed run or a "
            + "refused boot (p0392)");
    }

    [Fact]
    public void Capabilities_EveryBackendReadField_IsDeclared_ForConnections()
    {
        var declared = Capabilities.ConnectionTypes.SelectMany(t => t.Fields).Select(f => f.Key).ToHashSet();

        Undeclared<RawConnectionEntry>(declared).Should().BeEmpty(
            "a connection field the backend reads but the descriptor does not declare cannot be "
            + "edited in the Config Studio (p0392)");
    }

    [Fact]
    public void Capabilities_DeclaredFields_CarryTheirRequiredness()
    {
        var fields = Capabilities.TrackerTypes.SelectMany(t => t.Fields)
            .Concat(Capabilities.ConnectionTypes.SelectMany(t => t.Fields))
            .ToList();

        fields.Should().NotBeEmpty();
        fields.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Label));
        // Requiredness is only worth declaring if it is enforced: every required field must
        // be one the write-side validator can actually read off the entity.
        foreach (var type in Capabilities.TrackerTypes)
        {
            var required = type.Fields.Where(f => f.Required).Select(f => f.Key).ToList();
            var blank = new TrackerEntity(Id: "t", Type: type.Type, AuthSecret: null);
            var act = () => ConfigStudioCapabilities.ValidateTracker(blank);

            required.Should().NotBeEmpty($"tracker type '{type.Type}' declares an identity it needs");
            act.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>()
                .Which.Message.Should().ContainAll(required);
        }
    }

    [Fact]
    public void Capabilities_EveryDeclaredField_HasAValueShape()
    {
        // The shape is what the form renders from. A list rendered as a text box silently
        // corrupts the stored value, which is why the client no longer decides it.
        var fields = Capabilities.TrackerTypes.SelectMany(t => t.Fields)
            .Concat(Capabilities.ConnectionTypes.SelectMany(t => t.Fields));

        fields.Should().OnlyContain(f => Enum.IsDefined(f.Kind));
    }

    [Fact]
    public void Capabilities_PipelinesOffered_ExcludeRetiredAliases()
    {
        // p0393's distinction, which p0392 must preserve: a stored `fix-bug` keeps
        // validating (IsAcceptedName) and is never presented as a choice (Names).
        Capabilities.Pipelines.Should().NotContain("fix-bug");
        AgentSmith.Contracts.Commands.PipelinePresets.IsAcceptedName("fix-bug").Should().BeTrue();
        Capabilities.Pipelines.Should().BeEquivalentTo(AgentSmith.Contracts.Commands.PipelinePresets.Names);
    }

    private static IReadOnlyList<string> Undeclared<TRaw>(HashSet<string> declared) =>
        typeof(TRaw).GetProperties()
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..])
            .Select(key => WireKey.GetValueOrDefault(key, key))
            .Where(key => !NotAField.ContainsKey(key))
            .Where(key => !declared.Contains(key))
            .Distinct()
            .ToList();
}
