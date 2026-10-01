import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DialogTranscript } from "../DialogTranscript";
import type { DialogEntry } from "@/hooks/useSpecDialog";

// 2026-10-01-aeb6b: a document in an agent turn is a card whose Copy writes the raw text.
describe("DialogTranscript, with a document in an agent turn", () => {
  const document = "# Hand-off prompt\n\nContinue the **parser** work.\n```bash\ndotnet test\n```";
  const entry: DialogEntry = {
    key: "a-1",
    kind: "agent",
    text: `Here is the prompt.\n\n\`\`\`\`document\n${document}\n\`\`\`\`\n\nWant changes?`,
    at: "2026-10-01T10:00:00Z",
  };

  it("renders a document as a card whose Copy writes the raw text", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });

    render(<DialogTranscript entries={[entry]} onInspect={() => {}} />);

    expect(screen.getByTestId("dialog-document")).toHaveTextContent("Hand-off prompt");
    expect(screen.getByText("Here is the prompt.")).toBeInTheDocument();
    expect(screen.getByText("Want changes?")).toBeInTheDocument();
    fireEvent.click(screen.getByTestId("dialog-document-copy"));
    await waitFor(() => expect(writeText).toHaveBeenCalledWith(document));
    expect(await screen.findByText("Copied")).toBeInTheDocument();
  });
});
