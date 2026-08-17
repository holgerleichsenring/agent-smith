"use client";

import type { RunCommandPoint } from "@/lib/runStoryApi";
import { formatChars, formatMs } from "./callSeries";

// p0423b: the commands a phase ran, WITH their exit codes. "Verification failed" is a
// summary; `dotnet test` returning 1 after 9 seconds and producing 400k characters of
// which 100k reached the model is the evidence — and the evidence is what a diagnosis
// needs. The output itself is never here: sizes are safe to keep forever, content is not.

const CELL: React.CSSProperties = { padding: "4px 8px", textAlign: "right", whiteSpace: "nowrap" };
const HEAD: React.CSSProperties = { ...CELL, color: "var(--ink-3)", fontWeight: 600 };

export function CommandTable({ commands }: { commands: RunCommandPoint[] }) {
  if (commands.length === 0) {
    return (
      <p className="hint" data-testid="command-table-empty">
        This phase ran no sandbox command.
      </p>
    );
  }
  return (
    <div style={{ maxHeight: 260, overflow: "auto" }} data-testid="command-table">
      <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
        <thead>
          <tr>
            <th style={{ ...HEAD, textAlign: "left" }} scope="col">Command</th>
            <th style={HEAD} scope="col">Exit</th>
            <th style={HEAD} scope="col">Took</th>
            <th style={HEAD} scope="col">Output</th>
            <th style={HEAD} scope="col">Delivered</th>
          </tr>
        </thead>
        <tbody>
          {commands.map((command) => (
            <tr
              key={command.index}
              data-testid="command-row"
              data-exit={command.exitCode}
              style={{ borderTop: "1px solid var(--line-2)" }}
            >
              <th
                scope="row"
                className="mono"
                style={{ ...CELL, textAlign: "left", fontWeight: 400, whiteSpace: "normal" }}
              >
                {command.command}
                <small style={{ color: "var(--ink-3)" }}> · {command.repo}</small>
              </th>
              <td style={CELL}>
                <span className={command.exitCode === 0 ? "badge ok" : "badge bad"}>
                  {command.exitCode}
                </span>
              </td>
              <td style={CELL} className="mono">{formatMs(command.durationMs)}</td>
              <td style={CELL} className="mono">{formatChars(command.outputChars)}</td>
              <td style={CELL} className="mono">{formatChars(command.deliveredChars)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
