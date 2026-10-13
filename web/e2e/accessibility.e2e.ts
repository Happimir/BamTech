import AxeBuilder from '@axe-core/playwright';
import { expect, Page, test } from '@playwright/test';

const adminPassword = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin!2026';
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

/**
 * Runs axe on the settled page and reports violations readably on failure. Finite animations
 * (e.g. an error message fading in) are allowed to finish first, so axe judges the final
 * colours rather than a mid-transition frame; infinite ones (skeleton pulse) are ignored.
 */
async function expectNoViolations(page: Page, state: string): Promise<void> {
  await page.waitForFunction(() =>
    document
      .getAnimations()
      .every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity),
  );
  const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze();
  const summary = results.violations.map(
    (v) => `${v.id} (${v.impact}): ${v.help}\n    ${v.nodes.map((n) => n.target.join(' ')).join('\n    ')}`,
  );
  expect(summary, `axe violations on ${state}`).toEqual([]);
}

async function signIn(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password', { exact: true }).fill(adminPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/people$/);
}

test('landing page', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('link', { name: 'Sign in to ACTS' })).toBeVisible();
  await expectNoViolations(page, 'landing');

  await page.setViewportSize({ width: 360, height: 800 });
  await expectNoViolations(page, 'landing (mobile)');
});

test('sign-in page and its error state', async ({ page }) => {
  await page.goto('/login');
  await expectNoViolations(page, 'sign-in');

  await page.getByRole('button', { name: 'Sign in' }).click();
  await expectNoViolations(page, 'sign-in with required-field errors');

  // An unknown account gets the same generic error, without counting toward a real
  // account's lockout (5 failures lock it for 5 minutes).
  await page.getByLabel('Username').fill('no-such-user');
  await page.getByLabel('Password', { exact: true }).fill('not-the-password');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByText('Incorrect username or password.')).toBeVisible();
  await expectNoViolations(page, 'sign-in with credentials error');
});

test('directory states', async ({ page }) => {
  await signIn(page);
  await expect(page.locator('.results:not(.stale)')).toBeVisible();
  await expectNoViolations(page, 'directory');

  await page.getByLabel('Name').fill('Zzyzx');
  await page.getByLabel('Name').press('Enter');
  await expect(page.getByText('No people have names starting “Zzyzx”.')).toBeVisible();
  await expectNoViolations(page, 'directory with no matches');

  await page.setViewportSize({ width: 360, height: 800 });
  await page.getByRole('button', { name: 'Clear search' }).last().click();
  await expect(page.locator('.cards')).toBeVisible();
  await expectNoViolations(page, 'directory cards (mobile)');
});

test('person detail states', async ({ page }) => {
  await signIn(page);
  await page.getByLabel('Name').fill('Grace');
  await page.getByLabel('Name').press('Enter');
  await page.getByRole('link', { name: 'Grace Okonkwo', exact: true }).click();
  await expect(page.locator('.history-card')).toBeVisible();
  await expectNoViolations(page, 'person detail');

  await page.goto('/people/019a2b3c-4d5e-7f60-8a71-b2c3d4e5f607');
  await expect(page.getByRole('heading', { name: 'Person not found' })).toBeVisible();
  await expectNoViolations(page, 'person not found');
});

test('status pages', async ({ page }) => {
  await signIn(page);
  await page.goto('/forbidden');
  await expect(page.getByRole('heading', { name: 'Access denied' })).toBeVisible();
  await expectNoViolations(page, 'access denied');

  await page.goto('/no-such-page');
  await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
  await expectNoViolations(page, 'page not found');
});

test('focus moves to the new page heading, but not when only the search or page changes', async ({
  page,
}) => {
  await signIn(page);
  const search = page.getByLabel('Name');
  await search.fill('Grace');
  await search.press('Enter');
  await expect(page).toHaveURL(/name=Grace/);
  await expect(search).toBeFocused();

  await page.getByRole('link', { name: 'Grace Okonkwo', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Grace Okonkwo' })).toBeVisible();
  // The heading if it rendered in time, otherwise the main region; never left on the old link.
  await expect(page.locator('main h1:focus, main#main-content:focus')).toHaveCount(1);
});

test('the skip link is the first tab stop and moves focus into the main content', async ({ page }) => {
  await signIn(page);
  // A fresh load (sign-in itself already moved focus to the People heading).
  await page.reload();
  await expect(page.locator('.results:not(.stale)')).toBeVisible();
  await page.keyboard.press('Tab');
  const skip = page.getByRole('link', { name: 'Skip to main content' });
  await expect(skip).toBeFocused();
  await expect(skip).toBeInViewport();
  await page.keyboard.press('Enter');
  await expect(page.locator('main h1:focus, main#main-content:focus')).toHaveCount(1);
});

test('reflows at 200% zoom without horizontal scrolling', async ({ browser }) => {
  // A 1280x900 window at 200% zoom lays out like 640x450 CSS pixels at device scale 2.
  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    viewport: { width: 640, height: 450 },
    deviceScaleFactor: 2,
  });
  const page = await context.newPage();
  await signIn(page);
  const overflow = () =>
    page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

  await expect(page.locator('.results:not(.stale)')).toBeVisible();
  expect(await overflow()).toBe(0);

  await page.getByLabel('Name').fill('Maria');
  await page.getByLabel('Name').press('Enter');
  await page.getByRole('link', { name: /Maria Fernanda/ }).first().click();
  await expect(page.locator('.history-card')).toBeVisible();
  expect(await overflow()).toBe(0);
  await context.close();
});
