// 2026-10-01-283de: runs only when module scripts load from the local origin.
document.querySelector('h1').style.color = 'rgb(1, 2, 3)';

// A loopback websocket — the egress guard must refuse it.
try { new WebSocket('ws://127.0.0.1:6383/probe'); } catch { /* refused */ }

// A service worker that would recolour the h2 if it ever became active; workers are blocked.
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('/sw.js')
    .then(() => navigator.serviceWorker.ready)
    .then(() => { document.querySelector('h2').style.color = 'rgb(255, 0, 0)'; })
    .catch(() => { /* blocked */ });
}
