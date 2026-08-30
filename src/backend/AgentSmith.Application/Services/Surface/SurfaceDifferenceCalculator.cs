using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: the difference between what the interface offers and what its declared
/// clients were found to exercise.
/// <para>
/// The exercised set is a LOWER estimate — the account that travels with the result says
/// how much of the client source the reading decided — so every entry here is at most a
/// difference. An operation no call site named is reported once, as an operation: its
/// properties are unexercised only because it is, and listing them again would turn one
/// observation into as many as the operation has fields.
/// </para>
/// </summary>
public sealed class SurfaceDifferenceCalculator : ISurfaceDifferenceCalculator
{
    public SurfaceDifferenceReport Compute(
        IReadOnlyList<ServedOperation> served, ClientUsageReport usage)
    {
        ArgumentNullException.ThrowIfNull(served);
        ArgumentNullException.ThrowIfNull(usage);
        var exercised = ExercisedSurface.Of(usage.CallSites);
        var differences = served.SelectMany(operation => For(operation, exercised)).ToList();
        return new SurfaceDifferenceReport(
            Computed: true,
            NotComputedReason: null,
            Differences: differences,
            Account: usage.Account);
    }

    private static IEnumerable<SurfaceDifference> For(
        ServedOperation operation, ExercisedSurface exercised)
    {
        if (!exercised.Calls(operation))
            return [Difference(SurfaceDifferenceKind.UnexercisedOperation, operation, property: null)];

        return Unexercised(operation.AcceptedProperties, exercised.Sent(operation))
            .Select(p => Difference(SurfaceDifferenceKind.UnsentAcceptedProperty, operation, p))
            .Concat(Unexercised(operation.ReturnedProperties, exercised.Read(operation))
                .Select(p => Difference(SurfaceDifferenceKind.UnreadReturnedProperty, operation, p)));
    }

    private static IEnumerable<string> Unexercised(
        IReadOnlyList<string> offered, IReadOnlyCollection<string> seen) =>
        offered.Where(p => !seen.Contains(p, StringComparer.OrdinalIgnoreCase));

    private static SurfaceDifference Difference(
        SurfaceDifferenceKind kind, ServedOperation operation, string? property) =>
        new(kind, operation.Signature, property, SurfaceRequirements.For(kind));
}
