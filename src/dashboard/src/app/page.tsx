import { MissionControl } from "@/components/jobs/MissionControl";
import { InflowPill } from "@/components/jobs/mission/InflowPill";

// p0343: the home screen is mission control — tickets worked as jobs, ranked by
// what needs the operator (Needs-you first). Supersedes the flat RunsList table.
// p0343b: the header row carries the mock's inflow pill (tracker liveness +
// last pickup from the newest real run).
export default function JobsPage() {
  return (
    <main className="content-shell space-y-6">
      <header className="flex items-start gap-4">
        <div className="space-y-1">
          <h1 className="dsh-h1 font-semibold tracking-tight text-stone-900">Runs</h1>
          <p className="dsh-body text-stone-400">tickets, worked as jobs &mdash; what needs you, first</p>
        </div>
        <div className="ml-auto pt-1">
          <InflowPill />
        </div>
      </header>
      <MissionControl />
    </main>
  );
}
