# ACTS web UI

Angular 22 (standalone, zoneless, OnPush), Angular Material and NgRx SignalStore. Setup, run and test commands are in the [repository README](../README.md#run-the-web-ui).

- `src/app/core`: API models, the auth store, guards and the HTTP interceptor
- `src/app/features`: route-level pages (lazy-loaded)
- `e2e`: Playwright checks against the real API through the dev proxy
