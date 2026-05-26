import { listJobs } from "@/lib/api";
import { JobListTable } from "@/components/jobs/JobListTable";

export default async function JobsPage() {
  const { jobs, total } = await listJobs();
  return (
    <main className="mx-auto max-w-6xl space-y-6 p-8">
      <header className="space-y-1">
        <h1 className="text-3xl font-medium tracking-tight">agent-smith</h1>
        <p className="text-sm text-stone-500">
          {total === 0 ? "No runs recorded yet." : `${total} run${total === 1 ? "" : "s"}`}
        </p>
      </header>
      <JobListTable jobs={jobs} />
    </main>
  );
}
