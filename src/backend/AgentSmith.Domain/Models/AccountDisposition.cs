namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-25-9749: how the delivery account disposed of ONE ratified criterion.
/// <para>
/// A bool had only two answers and a conditional needs three. A criterion that applies
/// only where something was already the case — "every host that already configures X …" —
/// has to be squeezed into "not satisfied" when the base never had that thing, which is
/// not merely unhelpful but untrue: the outstanding list then goes back to the coding
/// master as an instruction to BUILD what another criterion of the same phase forbids.
/// </para>
/// <para>
/// The declaration ORDER is the merge precedence across diff windows, and the members are
/// numbered so it cannot drift. Satisfied is positive evidence and monotone. Not
/// applicable is a proof about the BASE, which no window's slice of the branch can
/// contradict, so it outranks one slice's failure to find a file. Not satisfied is the
/// absence of evidence and is the floor — which is what keeps the default right for the
/// positive claims that are the common case.
/// </para>
/// </summary>
public enum AccountDisposition
{
    /// <summary>No evidence ties the branch to this criterion. The floor, and the default
    /// for everything the account could not settle.</summary>
    NotSatisfied = 0,

    /// <summary>The criterion's antecedent is disproved by the base, so there is nothing
    /// for it to apply to. Neither outstanding nor, on its own, a pass.</summary>
    NotApplicable = 1,

    /// <summary>The branch satisfies it, at the evidence the row cites.</summary>
    Satisfied = 2,
}
