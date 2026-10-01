// 2026-10-01-283de: never becomes active — the browser blocks service workers.
self.addEventListener('install', () => self.skipWaiting());
