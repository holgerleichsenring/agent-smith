import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { DeleteRunButton } from "../DeleteRunButton";

describe("DeleteRunButton", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 204 } as Response);
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it("DeleteRunButton_FirstClickArms_SecondClickDeletes_AndCallsOnDeleted", async () => {
    const onDeleted = vi.fn();
    render(<DeleteRunButton runId="2026-07-14T10-00-00-aaaa" onDeleted={onDeleted} />);
    const button = screen.getByTestId("delete-run-2026-07-14T10-00-00-aaaa");
    expect(button).toHaveTextContent("delete");

    // First click only arms the confirm — no request yet.
    fireEvent.click(button);
    expect(button).toHaveTextContent("confirm delete");
    expect(globalThis.fetch).not.toHaveBeenCalled();

    // Second click fires the DELETE.
    fireEvent.click(button);
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/runs/2026-07-14T10-00-00-aaaa",
      expect.objectContaining({ method: "DELETE" }),
    );
    await waitFor(() => expect(onDeleted).toHaveBeenCalledOnce());
  });

  it("DeleteRunButton_Disarms_OnMouseLeaveBeforeConfirm", () => {
    render(<DeleteRunButton runId="r1" />);
    const button = screen.getByTestId("delete-run-r1");
    fireEvent.click(button);
    expect(button).toHaveTextContent("confirm delete");
    fireEvent.mouseLeave(button);
    expect(button).toHaveTextContent("delete");
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });
});
