namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0413: the SHAPE of a ticket's work, beside its size. Size decides what the
/// run may spend; shape decides how the work is cut — how many phases the
/// derivation emits and how their steps are phrased.
/// <para>
/// The distinction is structural, never a property of any ecosystem, toolchain
/// or language: it is stated by the model on the scope-classification reply and
/// this code only carries it.
/// </para>
/// </summary>
public enum WorkShape
{
    /// <summary>No shape was stated (absent / unrecognised) — the derivation is
    /// told nothing and cuts exactly as it did before (fail-safe).</summary>
    Unknown = 0,

    /// <summary>Once the facts are known the change is mechanical: the same edit
    /// over a known set, the kind of operation a codebase's own toolchain already
    /// performs in one go. Cut into the fewest phases the deliverable allows.</summary>
    Deterministic = 1,

    /// <summary>Diagnosis, design, weighing alternatives, exceptions — the work
    /// where a phase boundary buys a real stopping point and the loop earns its
    /// cost.</summary>
    Judgement = 2,

    /// <summary>Mostly one shape with a bounded pocket of the other. Cut along
    /// that seam: the mechanical sweep as one phase, the decided cases apart.</summary>
    Mixed = 3,
}
