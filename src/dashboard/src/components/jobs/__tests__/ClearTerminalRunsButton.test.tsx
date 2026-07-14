import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { ClearTerminalRunsButton } from "../ClearTerminalRunsButton";

describe("ClearTerminalRunsButton", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 200 } as Response);
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it("ClearTerminalRunsButton_Confirmed_DeletesTerminalOnly", async () => {
    render(<ClearTerminalRunsButton />);
    const button = screen.getByTestId("clear-terminal-runs");
    expect(button).toHaveTextContent("clear finished");

    fireEvent.click(button);
    expect(button).toHaveTextContent("confirm clear");
    expect(globalThis.fetch).not.toHaveBeenCalled();

    fireEvent.click(button);
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "/api/runs?state=terminal",
      expect.objectContaining({ method: "DELETE" }),
    );
    await waitFor(() => expect(button).toHaveTextContent("clear finished"));
  });
});
