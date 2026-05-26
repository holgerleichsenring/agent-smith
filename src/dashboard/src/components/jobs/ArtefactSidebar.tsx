import type { RunArtefact } from "@/lib/api";

export function ArtefactSidebar({
  runId,
  artefacts,
}: {
  runId: string;
  artefacts: RunArtefact[];
}) {
  if (artefacts.length === 0) {
    return <aside className="text-sm text-stone-500">No artefacts.</aside>;
  }
  return (
    <aside data-testid="artefact-sidebar" className="space-y-1 text-sm">
      <h2 className="text-xs uppercase tracking-wider text-stone-500">Artefacts</h2>
      <ul className="divide-y rounded-md border">
        {artefacts.map((a) => (
          <li key={a.filename} className="flex items-center justify-between px-3 py-2">
            <a
              href={`/api/jobs/${encodeURIComponent(runId)}/files/${encodeURIComponent(a.filename)}`}
              className="font-mono text-xs hover:underline"
              target="_blank"
              rel="noreferrer"
            >
              {a.filename}
            </a>
            <span className="text-xs text-stone-500">{formatBytes(a.sizeBytes)}</span>
          </li>
        ))}
      </ul>
    </aside>
  );
}

function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / (1024 * 1024)).toFixed(1)} MB`;
}
