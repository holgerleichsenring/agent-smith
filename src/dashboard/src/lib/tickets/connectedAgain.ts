import { HubConnectionState } from "@microsoft/signalr";

// 2026-11-23-1e76a: what a Connected means to a ticket read. After a Reconnecting or a Disconnected — or when the hub
// was reconnecting as the listener was made — nudges were missed, so everything is read again. The first connect of a
// cold page only closes the gap between the page's own reads and joining the overview group, so it costs less.

export interface ConnectedReads {
  /** The first Connected this listener sees, with no drop before it. */
  first: () => void;
  /** A Connected after a drop. */
  again: () => void;
}

export function connectedListener(atStart: HubConnectionState, reads: ConnectedReads): (state: HubConnectionState) => void {
  let dropped = atStart === HubConnectionState.Reconnecting;
  let seen = false;
  return (state) => {
    if (state === HubConnectionState.Reconnecting || state === HubConnectionState.Disconnected) {
      dropped = true;
      return;
    }
    if (state !== HubConnectionState.Connected) return;
    if (dropped) reads.again();
    else if (!seen) reads.first();
    dropped = false;
    seen = true;
  };
}
