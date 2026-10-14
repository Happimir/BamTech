import { expect, test } from '@playwright/test';

// The API seeds this fixed demo password in Development (see the README); override for other setups.
const adminPassword = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin!2026';

test('sign-in, protected read and sign-out work through the same-origin proxy', async ({
  page,
  context,
}) => {
  // Anonymous visit to a protected route lands on sign-in with a local return URL.
  await page.goto('/people');
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fpeople$/);

  // A wrong password is an inline generic error, not a redirect.
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password', { exact: true }).fill('not-the-password');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByText('Incorrect username or password.')).toBeVisible();
  await expect(page).toHaveURL(/\/login/);

  await page.getByLabel('Password', { exact: true }).fill(adminPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/people$/);
  await expect(page.locator('.shell-bar')).toContainText('admin');

  // The session and antiforgery cookies keep their security attributes through the proxy.
  const cookies = await context.cookies();
  const session = cookies.find((c) => c.name === '__Host-acts');
  const xsrf = cookies.find((c) => c.name === 'XSRF-TOKEN');
  expect(session).toMatchObject({ secure: true, httpOnly: true, sameSite: 'Strict', path: '/' });
  expect(xsrf).toMatchObject({ secure: true, httpOnly: false, sameSite: 'Strict' });
  expect(await page.evaluate(() => document.cookie)).not.toContain('__Host-acts');

  // The protected read renders the names it returned (whoever sorts first: other tests and the
  // demo add people). Sign-in itself already proved the XSRF header on a mutating request:
  // without it, login fails with csrf_failed.
  const read = page.waitForResponse((r) => r.url().includes('/api/people?'));
  await page.reload();
  const response = await read;
  expect(response.status()).toBe(200);
  const firstName: string = (await response.json()).items[0].name;
  await expect(page.getByRole('link', { name: firstName, exact: true }).first()).toBeVisible();

  // Sign-out revokes server-side; a replayed copy of the old session cookie is rejected
  // and the app returns to sign-in instead of showing protected data.
  await page.getByRole('button', { name: 'Log out' }).click();
  await expect(page).toHaveURL(/\/$/);
  await context.addCookies([session!]);
  const replayed = page.waitForResponse((r) => r.url().includes('/auth/me'));
  await page.goto('/people');
  expect((await replayed).status()).toBe(401);
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fpeople$/);
});
