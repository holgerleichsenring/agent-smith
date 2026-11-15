using System.Text.Json;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-15-06fc: an epic's series contract as its file <c>contract.yaml</c> — names, data, areas and budgets as
/// YAML — and back to the JSON the series contract reader checks.
/// </summary>
public static class DraftContractText
{
    public static string Render(SeriesContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var map = new Dictionary<string, object>();
        if (contract.Names.Count > 0) map["names"] = contract.Names.Select(Name).ToList();
        if (contract.Data.Count > 0) map["data"] = contract.Data.Select(Datum).ToList();
        if (contract.Areas.Count > 0)
            map["areas"] = contract.Areas.Select(a => new Dictionary<string, object> { ["spec"] = a.Spec, ["paths"] = a.Paths }).ToList();
        if (contract.Budgets.Count > 0)
            map["budgets"] = contract.Budgets.Select(b => new Dictionary<string, object>
                { ["path"] = b.Path, ["spec"] = b.Spec, ["max_lines"] = b.MaxLines }).ToList();
        return OutcomeYamlReader.ToYaml(map);
    }

    /// <summary>The contract as the JSON object the reader takes, or why the file cannot be read.</summary>
    public static (JsonElement? Contract, string? Error) Read(string text)
    {
        try
        {
            return YamlAsJson.Convert(text ?? string.Empty) is { } node
                ? (JsonSerializer.SerializeToElement(node), null)
                : (null, "contract.yaml is empty — delete it to drop the contract");
        }
        catch (YamlException ex)
        {
            return (null, $"contract.yaml is not valid YAML: {ex.Message}");
        }
    }

    private static Dictionary<string, object> Name(SeriesContractName name)
    {
        var map = new Dictionary<string, object> { ["name"] = name.Name, ["kind"] = name.Kind };
        if (name.Number is { } number) map["number"] = number;
        map["owner"] = name.Owner;
        if (name.Writers is { Count: > 0 } writers) map["writers"] = writers;
        return map;
    }

    private static Dictionary<string, object> Datum(SeriesContractDatum datum)
    {
        var map = new Dictionary<string, object> { ["datum"] = datum.Datum, ["lives_in"] = datum.LivesIn, ["owner"] = datum.Owner };
        if (datum.Writers is { Count: > 0 } writers) map["writers"] = writers;
        return map;
    }
}
