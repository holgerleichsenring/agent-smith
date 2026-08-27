import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ProjectInitAction } from "../ProjectInitAction";

// 2026-08-27-7098: what the operator actually reads when a start is refused. The api
// module and the control are exercised together on purpose — the defect was that a
// refusal reached the button as a number, and neither half alone can show that it no
// longer does.

// No authority is configured in a test tab; the sign-in loop is not what is under test.
vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

const fetchMock = vi.fn();

function refusalWith(status: number, body: unknown) {
  fetchMock.mockResolvedValue({ ok: false, status, json: async () => body });
}

async function press(): Promise<HTMLElement> {
  render(<ProjectInitAction project="sample" />);
  fireEvent.click(screen.getByTestId("project-init-sample"));
  return waitFor(() => screen.getByTestId("project-init-refusal-sample"));
}

describe("ProjectInitAction refusals", () => {
  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("Control_ARefusalWithAReason_ShowsTheReason", async () => {
    refusalWith(503, { reason: "no capacity — footprint 4Gi / 1 cpu exceeds the remaining budget" });

    const refusal = await press();

    expect(refusal.textContent).toContain("exceeds the remaining budget");
    // The code stays alongside the reason — it is what an operator quotes asking for help.
    expect(refusal.textContent).toContain("HTTP 503");
  });

  it("Control_ARefusalWithNoReason_StillNamesTheCode", async () => {
    refusalWith(500, {});

    const refusal = await press();

    expect(refusal.textContent).toContain("HTTP 500");
  });

  it("Control_ARefusalWithAnUnreadableBody_StillNamesTheCode", async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 502,
      json: async () => {
        throw new SyntaxError("Unexpected token < in JSON");
      },
    });

    const refusal = await press();

    expect(refusal.textContent).toContain("HTTP 502");
  });
});
