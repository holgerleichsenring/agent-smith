import { MissionControl } from "@/components/jobs/MissionControl";

// p0343: the home screen is mission control — tickets worked as jobs, ranked by
// what needs the operator (Needs-you first). Supersedes the flat RunsList table.
export default function JobsPage() {
  return (
    <main className="content-shell space-y-6">
      <header className="space-y-1">
        <h1 className="dsh-h1 font-semibold tracking-tight text-stone-900">Runs</h1>
        <p className="dsh-body text-stone-400">tickets, worked as jobs &mdash; what needs you, first</p>
      </header>
      <MissionControl />
    </main>
  );
}
