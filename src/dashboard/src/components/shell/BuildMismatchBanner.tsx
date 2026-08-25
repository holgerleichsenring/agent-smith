"use client";

import { BUILD_SUBSYSTEM } from "@/lib/buildIdentity";
import { useFindings } from "@/lib/useFindings";

/**
 * 2026-08-25-8c97: says that this page and the server it is talking to came from different
 * builds, and offers the one thing that fixes it.
 *
 * Advisory on purpose. The two halves are separate deployments, separately pinned, rolling
 * two replicas each — coexistence is what an upgrade looks like from the inside, and the
 * server withholds this finding for as long as its own rollout could still be in flight.
 * The tab is also still running the bundle it downloaded, so refusing to render here would
 * recreate the blank page the previous phase abolished. It reports a DIFFERENCE: whether
 * two builds can talk is a property of the contract between them, which nothing generates
 * from the server yet.
 */
export function BuildMismatchBanner() {
  const findings = useFindings();
  const mismatch = findings?.findings.find((f) => f.subsystem === BUILD_SUBSYSTEM);
  if (!mismatch) return null;

  return (
    <aside
      role="status"
      data-testid="build-mismatch-banner"
      className="flex flex-wrap items-center gap-3 border-b border-sky-300 bg-sky-50 px-4 py-3 text-sm text-sky-900"
    >
      <p className="flex-1">{mismatch.reason}</p>
      <button
        type="button"
        onClick={() => window.location.reload()}
        className="rounded border border-sky-400 bg-white px-3 py-1 font-medium text-sky-900 hover:bg-sky-100"
      >
        Reload
      </button>
    </aside>
  );
}
