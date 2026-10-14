import { expect, Page, test } from '@playwright/test';

const adminPassword = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin!2026';

test.beforeEach(async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password', { exact: true }).fill(adminPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/people$/);
});

const dialog = (page: Page) => page.getByRole('dialog');
const history = (page: Page) => page.locator('.history-card li');

/** Creates a uniquely named person through the UI and waits on their record. */
async function createPerson(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: 'Add person' }).click();
  await dialog(page).getByLabel('Full name').fill(name);
  await dialog(page).getByRole('button', { name: 'Create person' }).click();
  await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
  await expect(page.getByText('Person created')).toBeVisible();
}

async function addDuty(page: Page, entry: { rank: string; title?: string; start: string }): Promise<void> {
  await page.getByRole('button', { name: 'Add duty' }).click();
  await dialog(page).getByLabel('Rank').fill(entry.rank);
  if (entry.title) await dialog(page).getByLabel('Duty title').fill(entry.title);
  else await dialog(page).getByRole('radio', { name: 'Retire' }).check();
  await dialog(page).getByLabel('Start date').fill(entry.start);
}

test('an admin creates a person, records a career with a retirement, and renames them', async ({ page }) => {
  const name = `E2E Admin ${Date.now()}`;
  await createPerson(page, name);
  await expect(page.locator('.summary-card')).toContainText('No astronaut assignment yet');

  await addDuty(page, { rank: 'Ensign', title: 'Pilot', start: '3/1/2020' });
  await expect(dialog(page).locator('.preview')).toContainText('First service date: Mar 1, 2020.');
  await dialog(page).getByRole('button', { name: 'Add duty' }).click();
  await expect(dialog(page)).toHaveCount(0);
  await expect(page.getByText('Duty added')).toBeVisible();
  await expect(history(page)).toHaveCount(1);

  await addDuty(page, { rank: 'Lieutenant', start: '1/1/2024' });
  await expect(dialog(page).locator('.preview')).toContainText('Pilot ends Dec 31, 2023.');
  await expect(dialog(page).locator('.preview')).toContainText('Career ends Dec 31, 2023.');
  await dialog(page).getByRole('button', { name: 'Record retirement' }).click();
  await expect(page.getByText('Retirement recorded')).toBeVisible();
  await expect(page.locator('.summary-card')).toContainText('Career end');
  await expect(history(page).first()).toContainText('Retired');

  // Already retired: retirement is not offered again.
  await page.getByRole('button', { name: 'Add duty' }).click();
  await expect(dialog(page).getByRole('radio')).toHaveCount(0);
  await dialog(page).getByRole('button', { name: 'Cancel' }).click();
  await expect(dialog(page)).toHaveCount(0);

  await page.getByRole('button', { name: 'Rename' }).click();
  await dialog(page).getByLabel('Full name').fill(`${name} Renamed`);
  await dialog(page).getByRole('button', { name: 'Save name' }).click();
  await expect(page.getByRole('heading', { level: 1, name: `${name} Renamed` })).toBeVisible();
  await expect(page.getByText('Name updated')).toBeVisible();
  // The ID and history are unchanged by a rename.
  await expect(history(page)).toHaveCount(2);

  // The directory shows the change on return.
  await page.getByRole('link', { name: 'Back to people' }).first().click();
  await page.getByLabel('Name').fill(`${name} Renamed`);
  await page.getByLabel('Name').press('Enter');
  await expect(page.getByRole('link', { name: `${name} Renamed`, exact: true })).toBeVisible();
});

test('a change made elsewhere stops a stale save until the admin reloads and saves again', async ({ page }) => {
  const name = `E2E Stale ${Date.now()}`;
  await createPerson(page, name);
  const id = page.url().split('/').pop()!;

  await addDuty(page, { rank: 'Ensign', title: 'Pilot', start: '3/1/2020' });

  // Someone else renames the person while the dialog is open (a new version).
  const status = await page.evaluate(async ({ id, name }) => {
    const current = await (await fetch(`/api/people/${id}`)).json();
    const token = decodeURIComponent(document.cookie.split('; ').find((c) => c.startsWith('XSRF-TOKEN='))!.slice(11));
    const response = await fetch(`/api/people/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
      body: JSON.stringify({ name: `${name} Elsewhere`, version: current.version }),
    });
    return response.status;
  }, { id, name });
  expect(status).toBe(200);

  await dialog(page).getByRole('button', { name: 'Add duty' }).click();
  await expect(dialog(page).getByRole('alert')).toContainText('Someone else changed this person');
  // The draft is kept and nothing was written.
  await expect(dialog(page).getByLabel('Duty title')).toHaveValue('Pilot');

  await dialog(page).getByRole('button', { name: 'Reload' }).click();
  // The record behind the dialog (aria-hidden while it is open) now shows the other change.
  await expect(page.locator('.person-header h1')).toHaveText(`${name} Elsewhere`);
  await dialog(page).getByRole('button', { name: 'Add duty' }).click();
  await expect(page.getByText('Duty added')).toBeVisible();
  await expect(history(page)).toHaveCount(1);
});
