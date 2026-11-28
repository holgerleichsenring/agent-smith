using System.Text.Json;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-11-28-9bd1: a settings group page counts each section's differences from the default the
/// server reports. The claim that makes the count honest: an installation nobody configured reads
/// exactly the default, so it shows nothing changed.
/// </summary>
public sealed class SettingsDefaultTests
{
    [Fact]
    public void Default_OfEveryEditableType_EqualsAnUntouchedStoresValue()
    {
        using var h = new DbConfigTestHarness();

        foreach (var type in h.Store.SettingTypes)
            Wire(ConfigSettingsDefaults.Read(type)).Should().Be(Wire(h.Store.GetSetting(type)), $"'{type}' untouched is its default");
    }

    [Fact]
    public void Default_AfterASave_StillReadsTheModelDefault()
    {
        using var h = new DbConfigTestHarness();
        var before = Wire(ConfigSettingsDefaults.Read("orchestrator"));

        h.Store.SaveSetting("orchestrator", JsonDocument.Parse("""{"maxRunWallTimeSeconds":5400}""").RootElement,
            new AgentSmith.Contracts.Models.ConfigStudio.ChangeAttribution("alice"));

        Wire(ConfigSettingsDefaults.Read("orchestrator")).Should().Be(before);
        Wire(h.Store.GetSetting("orchestrator")).Should().NotBe(before);
    }

    private static string Wire(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
