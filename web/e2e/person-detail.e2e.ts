import { expect, test } from '@playwright/test';

const adminPassword = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin!2026';

test.beforeEach(async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password', { exact: true }).fill(adminPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/people$/);
});

const historyCard = (page: import('@playwright/test').Page) => page.locator('.history-card');
const summaryCard = (page: import('@playwright/test').Page) => page.locator('.summary-card');

test('a reactivated career keeps its retirement in history but has no current career end', async ({
  page,
}) => {
  await page.getByLabel('Name').fill('Gra');
  await page.getByLabel('Name').press('Enter');
  await page.getByRole('link', { name: 'Grace Okonkwo', exact: true }).click();

  await expect(page.getByRole('heading', { level: 1, name: 'Grace Okonkwo' })).toBeVisible();
  await expect(page).toHaveTitle('Grace Okonkwo · ACTS');
  await expect(summaryCard(page)).toContainText('Flight Director');
  await expect(summaryCard(page)).toContainText('First service date');
  await expect(summaryCard(page)).not.toContainText('Career end');

  const entries = historyCard(page).getByRole('listitem');
  await expect(entries).toHaveCount(4);
  await expect(entries.nth(0)).toContainText('Flight Director');
  await expect(entries.nth(0)).toContainText('Current');
  await expect(entries.nth(0)).toContainText('Returned to active duty');
  await expect(entries.nth(1)).toContainText('Retired');
  // The visual dash is hidden from screen readers, which read "to" instead.
  await expect(entries.nth(1)).toContainText(/Sep 1, 2020\s*–\s*to Feb 11, 2024/);

  // Back returns to the search the user came from.
  await page.getByRole('link', { name: 'Back to people' }).click();
  await expect(page).toHaveURL(/\/people\?name=Gra$/);
  await expect(page.getByLabel('Name')).toHaveValue('Gra');
});

test('a retired person shows the career end; a deep link survives a reload', async ({ page }) => {
  await page.getByLabel('Name').fill('Harold');
  await page.getByLabel('Name').press('Enter');
  await page.getByRole('link', { name: 'Harold Jensen', exact: true }).click();
  await expect(summaryCard(page)).toContainText('Career end');
  await expect(summaryCard(page)).toContainText('Dec 31, 2022');

  await page.reload();
  await expect(page.getByRole('heading', { level: 1, name: 'Harold Jensen' })).toBeVisible();
  // Without navigation state (a deep link), Back goes to the plain directory.
  await page.evaluate(() => history.replaceState(null, ''));
  await page.reload();
  await page.getByRole('link', { name: 'Back to people' }).click();
  await expect(page).toHaveURL(/\/people$/);
});

test('Back restores the directory page the user came from', async ({ page }) => {
  await page.getByRole('button', { name: 'Next page' }).click();
  await expect(page).toHaveURL(/\/people\?page=2$/);
  await page.locator('a.person-name').first().click();
  await expect(page.locator('.person-header')).toBeVisible();
  await page.getByRole('link', { name: 'Back to people' }).click();
  await expect(page).toHaveURL(/\/people\?page=2$/);
});

test('an unassigned person has an explanatory summary and no fabricated dates', async ({ page }) => {
  await page.getByLabel('Name').fill('Priya');
  await page.getByLabel('Name').press('Enter');
  await page.getByRole('link', { name: 'Priya Raman', exact: true }).click();
  await expect(summaryCard(page)).toContainText('No astronaut assignment yet');
  await expect(summaryCard(page)).not.toContainText('First service date');
  await expect(historyCard(page)).toContainText('No duties recorded.');
});

test('unknown and malformed IDs show not found; a malformed ID makes no request', async ({ page }) => {
  await page.goto('/people/019a2b3c-4d5e-7f60-8a71-b2c3d4e5f607');
  await expect(page.getByRole('heading', { name: 'Person not found' })).toBeVisible();

  const detailRequests: string[] = [];
  page.on('request', (r) => {
    if (r.url().includes('/duties')) detailRequests.push(r.url());
  });
  await page.goto('/people/not-a-real-id');
  await expect(page.getByRole('heading', { name: 'Person not found' })).toBeVisible();
  expect(detailRequests).toEqual([]);
});
