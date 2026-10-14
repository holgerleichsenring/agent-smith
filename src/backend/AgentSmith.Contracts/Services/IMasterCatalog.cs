namespace AgentSmith.Contracts.Services;

/// <summary>Whether the loaded skill catalog provides a master of a given name — asked before a run picks a
/// master whose catalog may not carry it yet, so it can keep one that is there instead of failing.</summary>
public interface IMasterCatalog
{
    bool HasMaster(string masterSkillName);
}
