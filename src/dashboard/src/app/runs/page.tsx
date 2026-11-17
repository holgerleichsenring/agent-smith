import { MissionControl } from "@/components/jobs/MissionControl";
import { InflowPill } from "@/components/jobs/mission/InflowPill";

// p0343: the runs list is mission control — tickets worked as jobs, ranked by what needs the operator (Needs-you
// first). p0343c (pixel identity): the page emits the runs-list.html mock DOM verbatim — .mock-shell/.mock-runs scope
// the ported mock stylesheet, .main/.m-head carry the mock's title row with the inflow pill on the right.
// 2026-11-17-d6fba: its own route, every run of every project like an agent pool's run list; / is the input page.
export default function RunsPage() {
  return (
    <div className="mock-shell mock-runs" data-testid="runs-home">
      <main className="main">
        <div className="m-head">
          <div>
            <h1>Runs</h1>
            <div className="msub">Tickets picked up from your trackers, worked as jobs.</div>
          </div>
          <InflowPill />
        </div>
        <MissionControl />
      </main>
    </div>
  );
}
