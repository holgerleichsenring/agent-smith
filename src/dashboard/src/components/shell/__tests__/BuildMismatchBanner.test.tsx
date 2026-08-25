import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { BuildMismatchBanner } from "../BuildMismatchBanner";
import type { StartupFinding, StartupFindings } from "@/lib/findingsApi";

// 2026-08-25-8c97: the operator-visible half of "both halves say which build they are".
// A difference between the two builds is advisory and the only action offered is a reload —
// a hard failure here would recreate the blank page the previous phase abolished.
const findingsRequest = vi.fn();
vi.mock("@/lib/findingsApi", () => ({
  fetchFindings: () => findingsRequest(),
}));

const reload = vi.fn();

const finding = (over: Partial<StartupFinding> = {}): StartupFinding => ({
  subsystem: "build",
  severity: "advisory",
  reason:
    "This page came from build bbbbbbbbbbbb; the server is running 0.129.0 (aaaaaaaaaaaa). "
    + "They are different builds — that is not by itself a fault, and nothing has been "
    + "refused. Reload to pick up the build this server serves.",
  project: null,
  trigger: null,
  field: null,
  ...over,
});

const findings = (...list: StartupFinding[]): StartupFindings => ({
  degraded: false,
  blocking: 0,
  advisory: list.length,
  findings: list,
});

describe("BuildMismatchBanner", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock itself as
  // the teardown hook, which then calls it after the test.
  beforeEach(() => {
    findingsRequest.mockReset();
    reload.mockReset();
    Object.defineProperty(window, "location", {
      configurable: true,
      value: { ...window.location, reload },
    });
  });

  it("Banner_AMismatch_OffersAReload", async () => {
    findingsRequest.mockResolvedValue(findings(finding()));

    render(<BuildMismatchBanner />);

    const banner = await screen.findByTestId("build-mismatch-banner");
    expect(banner).toHaveTextContent("different builds");
    fireEvent.click(screen.getByRole("button", { name: "Reload" }));
    expect(reload).toHaveBeenCalledOnce();
  });

  it("names both builds and never claims the two are incompatible", async () => {
    findingsRequest.mockResolvedValue(findings(finding()));

    render(<BuildMismatchBanner />);
    const banner = await screen.findByTestId("build-mismatch-banner");

    expect(banner).toHaveTextContent("bbbbbbbbbbbb");
    expect(banner).toHaveTextContent("aaaaaaaaaaaa");
    expect(banner.textContent?.toLowerCase()).not.toContain("incompat");
  });

  it("renders nothing when the two halves agree", async () => {
    findingsRequest.mockResolvedValue(findings());

    render(<BuildMismatchBanner />);

    await waitFor(() => expect(findingsRequest).toHaveBeenCalled());
    expect(screen.queryByTestId("build-mismatch-banner")).toBeNull();
  });

  it("ignores findings about anything other than the build", async () => {
    findingsRequest.mockResolvedValue(
      findings(finding({ subsystem: "redis", severity: "blocking" })),
    );

    render(<BuildMismatchBanner />);

    await waitFor(() => expect(findingsRequest).toHaveBeenCalled());
    expect(screen.queryByTestId("build-mismatch-banner")).toBeNull();
  });

  it("renders nothing when the findings endpoint cannot be reached", async () => {
    findingsRequest.mockImplementation(() => {
      throw new Error("HTTP 500");
    });

    render(<BuildMismatchBanner />);

    await waitFor(() => expect(findingsRequest).toHaveBeenCalled());
    expect(screen.queryByTestId("build-mismatch-banner")).toBeNull();
  });
});
