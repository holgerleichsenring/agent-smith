import Link from "next/link";
import type { RunMeta } from "@/lib/api";
import { StatusBadge } from "./StatusBadge";
import { RepoModeBadge } from "./RepoModeBadge";

function formatRelative(iso: string | null): string {
  if (!iso) return "—";
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return iso;
  const diffMs = Date.now() - then;
  const minutes = Math.round(diffMs / 60_000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.round(hours / 24);
  return `${days} d ago`;
}

function formatDuration(seconds: number): string {
  if (seconds <= 0) return "—";
  if (seconds < 60) return `${seconds}s`;
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return s === 0 ? `${m}m` : `${m}m ${s}s`;
}

export function JobListTable({ jobs }: { jobs: RunMeta[] }) {
  if (jobs.length === 0) {
    return (
      <div data-testid="empty-state" className="rounded-md border p-8 text-center text-sm">
        No jobs yet. Trigger a pipeline run and refresh.
      </div>
    );
  }

  return (
    <table data-testid="job-list-table" className="w-full text-sm">
      <thead className="text-left text-xs uppercase tracking-wider">
        <tr>
          <th className="px-3 py-2">Run</th>
          <th className="px-3 py-2">Pipeline</th>
          <th className="px-3 py-2">Repos</th>
          <th className="px-3 py-2">Sandboxes</th>
          <th className="px-3 py-2">Started</th>
          <th className="px-3 py-2">Duration</th>
          <th className="px-3 py-2">Status</th>
        </tr>
      </thead>
      <tbody>
        {jobs.map((j) => (
          <tr key={j.runId} className="border-t" data-testid="job-row">
            <td className="px-3 py-2 font-mono text-xs">
              <Link href={`/jobs/${encodeURIComponent(j.runId)}`}>{j.runId}</Link>
            </td>
            <td className="px-3 py-2">{j.pipelineName}</td>
            <td className="px-3 py-2">
              <RepoModeBadge mode={j.repoMode} repos={j.repos} />
            </td>
            <td className="px-3 py-2">{j.sandboxCount > 0 ? j.sandboxCount : "—"}</td>
            <td className="px-3 py-2">{formatRelative(j.startedAt)}</td>
            <td className="px-3 py-2">{formatDuration(j.durationSeconds)}</td>
            <td className="px-3 py-2">
              <StatusBadge status={j.status} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
