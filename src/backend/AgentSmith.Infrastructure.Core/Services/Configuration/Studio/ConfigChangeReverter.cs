using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: replays the inverse of one recorded config change — the prior doc of that entity
/// saved as a new attributed row, or a delete when the change created it. 2026-10-01-7f7aa:
/// carved out of <see cref="DbConfigStore"/>, which sat at its file-length baseline and could
/// not take the design-source members without giving something up first.
/// </summary>
public sealed class ConfigChangeReverter(IConfigDocumentStore docStore, ConfigDocumentAssembler assembler)
{
    public void Revert(string changeId, ChangeAttribution by)
    {
        var target = docStore.GetVersion(long.Parse(changeId))
            ?? throw new ConfigurationException($"Unknown config change '{changeId}'.");
        var prior = docStore.PriorDoc(target.Type, target.EntityId, target.Version);
        if (prior is null)
        {
            docStore.Delete(target.Type, target.EntityId, by.Actor);
            return;
        }
        docStore.Save(new ConfigDocWrite(
            target.Type, target.EntityId, prior, ExpectedVersion: null,
            assembler.EdgesFor(target.Type, prior), by.Actor, "revert"));
    }
}
