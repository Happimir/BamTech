# ACTS web UI

Angular 22 (standalone, zoneless, OnPush), Angular Material, NgRx SignalStore and Apollo Angular. Setup, run and test commands are in the [repository README](../README.md#run-the-web-ui).

- `src/app/core`: API models, the GraphQL client and generated operation types (`data-access/graphql`), the auth store, guards and the HTTP interceptor
- `src/app/features`: route-level pages (lazy-loaded); each store's `.graphql` operations sit beside it
- `codegen.ts`: `npm run codegen` regenerates the typed operations from `../src/StargateAPI/schema.graphql`
- `e2e`: Playwright checks against the real API through the dev proxy
