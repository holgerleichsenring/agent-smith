import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";

vi.mock("@/hooks/useSandboxEvents", () => ({
  useSandboxEvents: () => ({ command: null, outputs: [], result: null }),
}));
vi.mock("@/lib/EventFilterContext", () => ({
  useEventFilter: () => ({ state: { l1: new Set(), l2: new Set(), l3: new Set() }, toggle: vi.fn() }),
}));

import { TopologyDetail } from "../TopologyDetail";

describe("TopologyDetail", () => {
  it("no selection renders empty-state copy", () => {
    render(<TopologyDetail runId="r" selected={null} />);
    expect(screen.getByTestId("topology-detail-empty")).toBeInTheDocument();
  });

  it("selected repo renders the SandboxBox for that repo", () => {
    render(<TopologyDetail runId="r" selected="server" />);
    expect(screen.getByTestId("topology-detail")).toBeInTheDocument();
    expect(screen.getByTestId("sandbox-box-server")).toBeInTheDocument();
  });
});
