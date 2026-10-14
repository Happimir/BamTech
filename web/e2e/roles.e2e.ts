import { expect, Page, test } from '@playwright/test';

const readerPassword = process.env['E2E_READER_PASSWORD'] ?? 'Reader!2026';

async function signInAsReader(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('reader');
  await page.getByLabel('Password', { exact: true }).fill(readerPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/people$/);
}

test('a Reader gets the complete read experience with no write controls', async ({ page }) => {
  await signInAsReader(page);
  await expect(page.locator('.shell-bar')).toContainText('reader');
  await expect(page.locator('.shell-bar')).toContainText('Reader');
  await expect(page.locator('.results:not(.stale)')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Add person' })).toHaveCount(0);

  await page.getByLabel('Name').fill('Grace');
  await page.getByLabel('Name').press('Enter');
  await page.getByRole('link', { name: 'Grace Okonkwo', exact: true }).click();
  await expect(page.locator('.history-card').getByRole('listitem')).toHaveCount(4);

  // Read-only UI: the only buttons are navigation, copy and sign-out, never writes.
  const buttons = await page.getByRole('button').allInnerTexts();
  expect(buttons.map((b) => b.trim()).sort()).toEqual(['Copy ID', 'Log out']);
});

test('the API refuses a Reader write sent directly from the browser session', async ({ page }) => {
  await signInAsReader(page);
  const name = `Reader Write Probe ${Date.now()}`;

  const result = await page.evaluate(async (personName) => {
    const token = document.cookie
      .split('; ')
      .find((c) => c.startsWith('XSRF-TOKEN='))
      ?.slice('XSRF-TOKEN='.length);
    const write = await fetch('/api/people', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': decodeURIComponent(token ?? '') },
      body: JSON.stringify({ name: personName }),
    });
    const problem = await write.json();
    const check = await fetch(`/api/people?name=${encodeURIComponent(personName)}`);
    return { status: write.status, code: problem.code, matches: (await check.json()).totalCount };
  }, name);

  // A valid CSRF token and session, but the role lacks People.Write: 403, nothing created.
  expect(result).toEqual({ status: 403, code: 'forbidden', matches: 0 });
});
