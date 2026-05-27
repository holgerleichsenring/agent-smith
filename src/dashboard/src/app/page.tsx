export default function JobsPage() {
  return (
    <main className="mx-auto max-w-6xl space-y-6 p-8">
      <header className="space-y-1">
        <h1 className="text-3xl font-medium tracking-tight">agent-smith</h1>
        <p className="text-sm text-stone-500">connecting…</p>
      </header>
      <p className="text-sm text-stone-500">
        Job-Viewer UI lands in p0169f. The dashboard now connects to the
        SignalR hub at <code className="font-mono">/hub/jobs</code>.
      </p>
    </main>
  );
}
