import { expect, test } from '@playwright/test';

const adminPassword = process.env['E2E_ADMIN_PASSWORD'] ?? '';

test.skip(!adminPassword, 'E2E_ADMIN_PASSWORD is not set');

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

  // A mutating same-origin request with the XSRF header succeeds, so the read has a name.
  const status = await page.evaluate(async () => {
    const token = document.cookie
      .split('; ')
      .find((c) => c.startsWith('XSRF-TOKEN='))
      ?.slice('XSRF-TOKEN='.length);
    const response = await fetch('/api/people', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': decodeURIComponent(token ?? ''),
      },
      body: JSON.stringify({ name: 'Ada Okafor' }),
    });
    return response.status;
  });
  expect(status).toBe(201);

  // The protected read renders names.
  const read = page.waitForResponse((r) => r.url().includes('/api/people?'));
  await page.reload();
  expect((await read).status()).toBe(200);
  await expect(page.locator('.people-list li').first()).toHaveText('Ada Okafor');

  // Sign-out revokes server-side; a replayed copy of the old session cookie is rejected
  // and the app returns to sign-in instead of showing protected data.
  await page.getByRole('button', { name: 'Log out' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await context.addCookies([session!]);
  const replayed = page.waitForResponse((r) => r.url().includes('/auth/me'));
  await page.goto('/people');
  expect((await replayed).status()).toBe(401);
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fpeople$/);
});
