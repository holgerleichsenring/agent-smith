"use client";

import { SPEC_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59r: a spec node's way to its document — the Specs tab with that spec selected.

export function OpenSpecButton({ specId }: { specId: string }) {
  const address = useTicketTabGo();
  return (
    <p className="open-spec">
      <button type="button" className="btn sm" data-testid="node-open-spec"
        onClick={() => address.go("specs", { [SPEC_PARAM]: specId })}>Open the spec</button>
    </p>
  );
}
