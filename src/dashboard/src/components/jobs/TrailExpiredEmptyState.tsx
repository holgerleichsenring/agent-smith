"use client";

interface Props {
  prUrl: string | null;
}

// p0169j-a: empty-state for the Trail tab when the per-run stream has
// TTL-expired (24h+) but the run snapshot is still in the Recent-50
// LIST. Mirrors ResultTab's empty state — same "live cache → PR
// fallback" mental model.

export function TrailExpiredEmptyState({ prUrl }: Props) {
  return (
    <div
      className="rounded-lg border border-dashed border-stone-300 bg-white p-6 text-sm text-stone-600"
      data-testid="trail-expired-empty"
    >
      <p className="mb-2 font-medium text-stone-800">Trail expired (24h+).</p>
      {prUrl !== null ? (
        <p>
          The full execution detail is in the PR —{" "}
          <a
            href={prUrl}
            target="_blank"
            rel="noreferrer"
            className="text-stone-800 underline hover:text-stone-900"
            data-testid="trail-expired-pr-link"
          >
            view in PR
          </a>
          .
        </p>
      ) : (
        <p>
          Per-event detail is only retained for the live look-back
          window. The PR carries the synthesised result.md if you
          need to refer back.
        </p>
      )}
    </div>
  );
}
