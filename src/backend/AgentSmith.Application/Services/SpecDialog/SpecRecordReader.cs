using AgentSmith.Contracts.Models;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// A spec record as a run stored it, read tolerantly: the id is the one the caller's row names, a
/// record keyed with the noun used before spec reads like one keyed <c>spec:</c>, and
/// the account a record carries as comment lines is ignored by the yaml read. A body that still does
/// not read as a spec yields null instead of an exception.
/// </summary>
public sealed class SpecRecordReader(SpecDraftReader drafts)
{
    private static readonly string[] IdKeys = ["spec", "phase"];

    public SpecDraft? Read(string specId, string body)
    {
        try
        {
            var map = OutcomeYamlReader.ReadMap(body);
            if (!IdKeys.Any(map.ContainsKey))
                throw new InvalidOperationException("The record carries no spec id key.");
            return drafts.Read(map, specId, body);
        }
        catch (Exception ex) when (ex is YamlException or InvalidOperationException)
        {
            return null;
        }
    }
}
