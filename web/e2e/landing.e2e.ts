import { expect, test } from '@playwright/test';

const adminPassword = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin!2026';

test('a visitor sees the introduction, signs in from it, and returns to it after signing out', async ({
  page,
}) => {
  await page.goto('/');
  await expect(page).toHaveTitle('ACTS · Astronaut Career Tracking System');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('career');
  // No credentials are ever shown on a public page.
  await expect(page.locator('body')).not.toContainText('Admin!2026');

  await page.getByRole('link', { name: 'Sign in to ACTS' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password', { exact: true }).fill(adminPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/people$/);

  // Signed in, the introduction is skipped.
  await page.goto('/');
  await expect(page).toHaveURL(/\/people$/);

  await page.getByRole('button', { name: 'Log out' }).click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole('link', { name: 'Sign in to ACTS' })).toBeVisible();
});

test('the sign-in page links back to the introduction', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('link', { name: 'About ACTS' }).click();
  await expect(page).toHaveURL(/\/$/);
});
