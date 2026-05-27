import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { TrailTruncationBanner } from "../TrailTruncationBanner";

describe("TrailTruncationBanner", () => {
  it("renders the MAXLEN truncation note", () => {
    render(<TrailTruncationBanner />);
    expect(screen.getByTestId("trail-truncation-banner")).toBeInTheDocument();
    expect(screen.getByText(/MAXLEN=10000/)).toBeInTheDocument();
  });
});
