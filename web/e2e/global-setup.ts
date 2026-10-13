import { execFileSync } from 'node:child_process';

/** Ensures the fictional sample people exist (idempotent) so tests have known data to find. */
export default function globalSetup(): void {
  execFileSync(process.execPath, ['scripts/seed-sample-data.mjs'], { stdio: 'inherit' });
}
