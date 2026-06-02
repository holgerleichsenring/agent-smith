namespace Fixture;

/// <summary>
/// Trivial production class so the fixture's test project has a target
/// under-test. Keeps the fixture realistic without dragging in business
/// logic that would obscure the harness's intent.
/// </summary>
public static class Calculator
{
    public static int Add(int a, int b) => a + b;
}
