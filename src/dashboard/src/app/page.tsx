export default function JobsPage() {
  return (
    <main className="mx-auto max-w-6xl space-y-6 p-8">
      <header className="space-y-1">
        <h1 className="text-3xl font-medium tracking-tight">agent-smith</h1>
        <p className="text-sm text-stone-500">placeholder · p0169e backbone ships, UI lands in p0169f</p>
      </header>
      <p className="text-sm text-stone-500">
        The event-sourcing backbone is live server-side: the SignalR hub at{" "}
        <code className="font-mono">/hub/jobs</code> serves OverviewSnapshot /
        RunEvent / SandboxEvent, and runs publish into{" "}
        <code className="font-mono">run:{"{runId}"}:events</code>. This page
        does <strong>not</strong> connect yet — the dashboard client wiring
        (TopologyCard, useJobsHub, SandboxBox) lands in p0169f.
      </p>
      <p className="text-sm text-stone-500">
        Verify the backbone with{" "}
        <code className="font-mono">redis-cli SMEMBERS agentsmith:runs:active</code>{" "}
        and{" "}
        <code className="font-mono">redis-cli XREVRANGE run:{"<id>"}:events + - COUNT 20</code>.
      </p>
    </main>
  );
}
