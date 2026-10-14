using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762j: a series contract to and from the manifest's <c>contract:</c> block. No contract writes no
/// block, so a manifest of a series that declared none is the manifest it always was.
/// </summary>
public static class SeriesContractYaml
{
    public static SeriesContractDocument? Write(SeriesContract? contract) => contract is null || contract.IsEmpty ? null : new()
    {
        Names = [.. contract.Names.Select(n => new SeriesContractNameEntry
            { Name = n.Name, Kind = n.Kind, Number = n.Number, Owner = n.Owner, Writers = n.Writers?.ToList() })],
        Data = [.. contract.Data.Select(d => new SeriesContractDatumEntry
            { Datum = d.Datum, LivesIn = d.LivesIn, Owner = d.Owner, Writers = d.Writers?.ToList() })],
        Areas = [.. contract.Areas.Select(a => new SeriesContractAreaEntry { Spec = a.Spec, Paths = [.. a.Paths] })],
        Budgets = [.. contract.Budgets.Select(b => new SeriesContractBudgetEntry { Path = b.Path, Spec = b.Spec, MaxLines = b.MaxLines })],
    };

    public static SeriesContract? Read(SeriesContractDocument? document)
    {
        if (document is null) return null;
        var contract = new SeriesContract(
            [.. (document.Names ?? []).Select(n => new SeriesContractName(n.Name, n.Kind, n.Number, n.Owner, n.Writers))],
            [.. (document.Data ?? []).Select(d => new SeriesContractDatum(d.Datum, d.LivesIn, d.Owner, d.Writers))],
            [.. (document.Areas ?? []).Select(a => new SeriesContractArea(a.Spec, a.Paths ?? []))],
            [.. (document.Budgets ?? []).Select(b => new SeriesContractBudget(b.Path, b.Spec, b.MaxLines))]);
        return contract.IsEmpty ? null : contract;
    }
}
