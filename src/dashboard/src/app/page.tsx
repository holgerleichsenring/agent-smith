import { OverviewCardGrid } from "@/components/jobs/OverviewCardGrid";

export default function JobsPage() {
  return (
    <main className="mx-auto max-w-6xl space-y-6 p-8">
      <header className="space-y-1">
        <h1 className="text-3xl font-medium tracking-tight">agent-smith</h1>
        <p className="text-sm text-stone-500">runs &mdash; live</p>
      </header>
      <OverviewCardGrid />
    </main>
  );
}
