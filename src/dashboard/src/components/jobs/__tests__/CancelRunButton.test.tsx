import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { CancelRunButton } from "../CancelRunButton";

describe("CancelRunButton", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 202,
    } as Response);
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it("CancelRunButton_FirstClickArms_SecondClickPosts_AndRendersCancelling", async () => {
    // p0372: cancel is destructive-equivalent to delete — the first click only
    // ARMS the button (same two-click confirm as DeleteRunButton); nothing is
    // posted until the operator confirms with a second click.
    render(<CancelRunButton runId="2026-06-02T10-00-00-aaaa" cancelRequested={false} />);

    const button = screen.getByTestId("cancel-run-2026-06-02T10-00-00-aaaa");
    expect(button).toHaveTextContent("cancel");

    fireEvent.click(button);
    expect(button).toHaveTextContent("confirm cancel");
    expect(globalThis.fetch).not.toHaveBeenCalled();

    fireEvent.click(button);
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/runs/2026-06-02T10-00-00-aaaa/cancel",
      expect.objectContaining({ method: "POST" }),
    );
    await waitFor(() => expect(screen.getByTestId("cancel-run-2026-06-02T10-00-00-aaaa"))
      .toHaveTextContent("cancelling…"));
  });

  it("CancelRunButton_MouseLeaveWhileArmed_Disarms", () => {
    render(<CancelRunButton runId="r2" cancelRequested={false} />);
    const button = screen.getByTestId("cancel-run-r2");
    fireEvent.click(button);
    expect(button).toHaveTextContent("confirm cancel");
    fireEvent.mouseLeave(button);
    expect(button).not.toHaveTextContent("confirm cancel");
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it("CancelRunButton_AlreadyCancelRequested_RendersDisabled", () => {
    render(<CancelRunButton runId="r1" cancelRequested={true} />);
    const button = screen.getByTestId("cancel-run-r1");
    expect(button).toHaveTextContent("cancelling…");
    expect(button).toBeDisabled();
  });
});
