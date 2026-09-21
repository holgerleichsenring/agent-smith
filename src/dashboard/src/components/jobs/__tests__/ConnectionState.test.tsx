import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { ConnectionState } from "../ConnectionState";
import { ApiRefusal } from "@/lib/apiResponse";

// 2026-09-21-291b: "offline" is a claim about the SERVER. Under enforcement the
// hub's negotiate is a 401 from a server that is running and answering, and an
// operator reading "offline" goes looking for a process that is up.

const signedOut = new ApiRefusal("/api/runs", 401, "sign-in", []);
const notAllowed = new ApiRefusal("/api/runs", 403, "permission", ["runs.read"]);

describe("ConnectionState", () => {
  it("ConnectionState_RefusedForSignIn_DoesNotSayOffline", () => {
    render(<ConnectionState state={HubConnectionState.Disconnected} refusal={signedOut} />);

    expect(screen.getByTestId("hub-connection-state")).toHaveTextContent("signed out");
    expect(screen.getByTestId("hub-connection-state")).not.toHaveTextContent("offline");
  });

  it("ConnectionState_RefusedForAPermission_SaysNotAllowed", () => {
    render(<ConnectionState state={HubConnectionState.Disconnected} refusal={notAllowed} />);

    expect(screen.getByTestId("hub-connection-state")).toHaveTextContent("not allowed");
  });

  it("ConnectionState_NoRefusal_StillSaysOffline", () => {
    render(<ConnectionState state={HubConnectionState.Disconnected} />);

    expect(screen.getByTestId("hub-connection-state")).toHaveTextContent("offline");
  });

  it("ConnectionState_ConnectedDespiteARefusal_SaysConnected", () => {
    // The transport is genuinely up; the refusal then belongs to whichever
    // surface could not read its own data, not to the connection.
    render(<ConnectionState state={HubConnectionState.Connected} refusal={notAllowed} />);

    expect(screen.getByTestId("hub-connection-state")).toHaveTextContent("connected");
  });
});
