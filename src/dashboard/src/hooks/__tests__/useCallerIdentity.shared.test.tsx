import { render, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

// 2026-11-22-9719c: every gated act on a page asks for the identity; they share one read.
const fetchIdentity = vi.fn();
vi.mock("@/lib/identityApi", () => ({ fetchIdentity: (...a: unknown[]) => fetchIdentity(...a) }));
vi.mock("../useAccessToken", () => ({ useAccessToken: () => "token-1" }));
const { useCallerIdentity } = await import("../useCallerIdentity");

function Probe() {
  const { identity } = useCallerIdentity();
  return <span data-testid="probe">{identity ? "known" : "…"}</span>;
}

describe("useCallerIdentity", () => {
  it("useCallerIdentity_FiftyCallersOnOnePage_ShareOneRead", async () => {
    fetchIdentity.mockResolvedValue({ subject: "s", roles: [], permissions: [], findings: [] });
    const { getAllByTestId } = render(<>{Array.from({ length: 50 }, (_, i) => <Probe key={i} />)}</>);
    await waitFor(() => expect(getAllByTestId("probe").every((p) => p.textContent === "known")).toBe(true));
    expect(fetchIdentity).toHaveBeenCalledTimes(1);
  });

  it("useCallerIdentity_AFailedRead_IsNotKept", async () => {
    fetchIdentity.mockReset();
    fetchIdentity.mockRejectedValueOnce(new Error("500")).mockResolvedValue({ subject: "s", roles: [], permissions: [], findings: [] });
    const first = render(<Probe />);
    await waitFor(() => expect(fetchIdentity).toHaveBeenCalledTimes(1));
    first.unmount();
    await new Promise((r) => setTimeout(r, 0));
    const second = render(<Probe />);
    await waitFor(() => expect(second.getByTestId("probe")).toHaveTextContent("known"));
    expect(fetchIdentity).toHaveBeenCalledTimes(2);
  });
});
