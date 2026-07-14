import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CapacityFootprintPanel } from "../CapacityFootprintPanel";
import type { RunFootprintView } from "@/types/hub-events";

const footprint: RunFootprintView = {
  pods: [
    { repo: "server", contexts: ["sdk8"], image: "dotnet", cpuLimit: "1", memLimit: "4Gi" },
    { repo: "orchestrator", contexts: [], image: "orchestrator", cpuLimit: "500m", memLimit: "1Gi" },
  ],
  totalCpuLimit: "1.5",
  totalMemLimit: "5Gi",
  dropped: [{ repo: "server", context: "encrypter", reason: "unaffected by the MassTransit swap" }],
  reason: "2 pods",
  reserved: false,
};

describe("CapacityFootprintPanel", () => {
  it("CapacityFootprintPanel_Queued_ShowsPositionTotalsAndDropped", () => {
    render(<CapacityFootprintPanel footprint={footprint} queuePosition={3} />);
    expect(screen.getByTestId("capacity-footprint-panel")).toBeInTheDocument();
    expect(screen.getByText("queued · #3")).toBeInTheDocument();
    expect(screen.getByText(/2 pods · 5Gi \/ 1.5 cpu/)).toBeInTheDocument();
    expect(screen.getByText(/encrypter/)).toBeInTheDocument();
  });

  it("CapacityFootprintPanel_Reserved_ShowsReservedBadge", () => {
    render(<CapacityFootprintPanel footprint={{ ...footprint, reserved: true }} />);
    expect(screen.getByText("reserved")).toBeInTheDocument();
  });
});
