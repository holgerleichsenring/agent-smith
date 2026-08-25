"use client";

import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "./RefusalSurface";

// 2026-08-25-39ab: what a render failure looks like. A blank page is worse than
// a wrong number, and it is also worse than an ugly one: an operator watching a
// run needs to know WHICH surface stopped and what it said, so the panel names
// the surface and prints the message verbatim instead of apologising in the
// abstract. Rendered by every boundary — the route one, the global one, and the
// per-surface ones — so a failure reads the same wherever it lands.

interface Props {
  /** The surface that failed, in the words an operator would use for it. */
  surface: string;
  error: Error | null;
  /** Present when the caller can ask React to render the surface again. */
  onRetry?: () => void;
}

export function FailedSurface({ surface, error, onRetry }: Props) {
  // 2026-08-25-4530: a refusal is not a failure of this surface, and "could not
  // be rendered" is the wrong sentence for it. Branching here rather than in each
  // boundary is what makes every boundary — route, global and per-surface — say
  // the right thing about a signed-out or under-permissioned caller.
  const refusal = refusalIn(error);
  if (refusal) return <RefusalSurface refusal={refusal} surface={surface} />;

  return (
    <div
      role="alert"
      data-testid="failed-surface"
      data-surface={surface}
      className="rounded-xl border border-amber-200 bg-amber-50 p-5 text-left"
    >
      <p className="text-sm font-semibold text-amber-900">
        The {surface} could not be rendered.
      </p>
      <p className="mt-1 text-xs text-amber-800">
        The rest of the page is unaffected. This is usually a payload this build does
        not recognise.
      </p>
      {error?.message && (
        <p
          className="mt-2 break-words font-mono text-xs text-amber-900/80"
          data-testid="failed-surface-message"
        >
          {error.message}
        </p>
      )}
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          data-testid="failed-surface-retry"
          className="mt-3 rounded border border-amber-300 bg-white px-2.5 py-1 text-xs font-medium text-amber-900 hover:bg-amber-100"
        >
          Try again
        </button>
      )}
    </div>
  );
}
