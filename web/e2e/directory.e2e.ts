import { expect, Page, Request, test } from '@playwright/test';

const adminPassword = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin!2026';

/** Records every list request the page makes from now on. */
function trackListRequests(page: Page): URL[] {
  const seen: URL[] = [];
  page.on('request', (request: Request) => {
    const url = new URL(request.url());
    if (url.pathname === '/api/people') seen.push(url);
  });
  return seen;
}

test.beforeEach(async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password', { exact: true }).fill(adminPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/people$/);
  await expect(page.getByRole('status').filter({ hasText: /people/ })).toContainText('showing 1–');
});

test('typing alone sends no request; Enter applies the search once and the URL keeps it', async ({
  page,
}) => {
  const requests = trackListRequests(page);
  const search = page.getByLabel('Name');

  await search.pressSequentially('Gra', { delay: 80 });
  await page.waitForTimeout(500);
  expect(requests).toHaveLength(0);

  await search.press('Enter');
  await expect(page).toHaveURL(/\/people\?name=Gra$/);
  await expect(page.getByRole('link', { name: 'Grace Okonkwo', exact: true })).toBeVisible();
  expect(requests.map((u) => u.searchParams.get('name'))).toEqual(['Gra']);
  expect(requests[0].searchParams.get('page')).toBe('1');

  // The applied search survives a reload: the URL is the source of truth.
  await page.reload();
  await expect(page.getByLabel('Name')).toHaveValue('Gra');
  await expect(page.getByRole('link', { name: 'Grace Okonkwo', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Clear search' }).first().click();
  await expect(page).toHaveURL(/\/people$/);
  await expect(page.getByLabel('Name')).toHaveValue('');
});

test('a search with no matches says so and offers to clear it', async ({ page }) => {
  await page.getByLabel('Name').fill('Zzyzx');
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await expect(page.getByText('No people have names starting “Zzyzx”.')).toBeVisible();
  await page.getByRole('button', { name: 'Clear search' }).last().click();
  await expect(page).toHaveURL(/\/people$/);
});

test('paging requests one page at a time and Back restores the previous page', async ({ page }) => {
  const requests = trackListRequests(page);
  const firstName = await page.getByRole('cell').locator('a.person-name').first().innerText();

  await page.getByRole('button', { name: 'Next page' }).click();
  await expect(page).toHaveURL(/\/people\?page=2$/);
  await expect(page.getByRole('status').filter({ hasText: /people/ })).toContainText('showing 26–');
  expect(requests).toHaveLength(1);
  expect(requests[0].searchParams.get('page')).toBe('2');

  await page.goBack();
  await expect(page).toHaveURL(/\/people$/);
  await expect(page.getByRole('cell').locator('a.person-name').first()).toHaveText(firstName);
});

test('namesakes stay separate rows that can be told apart', async ({ page }) => {
  await page.getByLabel('Name').fill('Alex Morgan');
  await page.getByLabel('Name').press('Enter');
  const rows = page.getByRole('row').filter({ has: page.getByRole('link', { name: 'Alex Morgan', exact: true }) });
  await expect(rows).toHaveCount(2);
  const hints = await rows.locator('.namesake-id').allInnerTexts();
  expect(hints).toHaveLength(2);
  expect(hints[0]).not.toBe(hints[1]);
});
