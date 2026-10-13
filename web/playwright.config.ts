import { defineConfig } from '@playwright/test';

/**
 * End-to-end checks against the real API through the HTTPS dev server and proxy.
 * Prerequisites: the API is running (https launch profile) with the Development demo users
 * seeded, and `npm start` is serving. Set E2E_ADMIN_PASSWORD if you seeded your own password.
 * See the repository README.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'https://localhost:4200',
    channel: 'chrome',
    // The ASP.NET dev certificate is not trusted on every machine; the page is still an
    // HTTPS secure context, so Secure and __Host- cookie rules apply unchanged.
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
  },
});
