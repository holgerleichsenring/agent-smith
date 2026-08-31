using System.Runtime.CompilerServices;

namespace AgentSmith.PipelineHarness;

/// <summary>
/// 2026-08-30-f590: the test host asks for its worker threads up front.
/// <para>
/// The pool starts at one thread per core and grows by roughly one per second. A CI runner
/// with two cores runs three test projects at once, and any blocking call inside them holds
/// a worker while every async continuation waits behind it — measured here as tests that
/// take 66 ms locally taking 37 seconds there, and as a loop that got no scheduling at all
/// for thirty seconds while asserting that it had progressed. Nothing was broken; the host
/// simply could not run what it had been given.
/// </para>
/// <para>
/// Raising the MINIMUM removes the growth delay only — the pool still creates threads on
/// demand and still reclaims them. It changes no production behaviour: this runs in the test
/// assembly, and the values are a floor rather than a cap.
/// </para>
/// </summary>
internal static class TestHostThreadPool
{
    [ModuleInitializer]
    internal static void Raise()
    {
        var floor = Math.Max(Environment.ProcessorCount * 8, 32);
        ThreadPool.GetMinThreads(out var workers, out var ports);
        ThreadPool.SetMinThreads(Math.Max(workers, floor), Math.Max(ports, floor));
    }
}
