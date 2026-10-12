namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-10-12-967aa: a name a READER accepts beside the declared one — on a JSON property, or on
/// an enum member read back by name. Writers never see it: the declared name is what is written.
/// <para>
/// It exists for an expand/contract rename across replicas. Several servers share one store and
/// one Redis during a rolling deploy, so a release that writes a new name has to land only after
/// every running replica can read it. The attribute is that "can read it", stated where the member
/// is declared so the type stays one model rather than an old and a new copy of it.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public sealed class AcceptedNameAttribute(string name) : Attribute
{
    /// <summary>The alias in declaration casing ("SpecId"); a naming policy is applied on read.</summary>
    public string Name { get; } = name;
}
