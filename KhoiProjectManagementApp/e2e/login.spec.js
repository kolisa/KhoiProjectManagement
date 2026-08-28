// e2e/login.spec.js - critical journey #1: login. Requires the real API + Postgres running (see
// playwright.config.js's prerequisites comment) and the seeded users DatabaseSeeder creates.
import { test, expect } from '@playwright/test';

const ADMIN_EMAIL = 'kholisa@khoitech.Africa';
const ADMIN_PASSWORD = 'admin123';

test.describe('Login', () => {
  test('shows an inline error and stays on the login screen for bad credentials', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('heading', { name: /sign in to khoi pro/i })).toBeVisible();
    await page.getByPlaceholder('Email address').fill(ADMIN_EMAIL);
    await page.getByPlaceholder('Password').fill('definitely-the-wrong-password');
    await page.getByRole('button', { name: /sign in/i }).click();

    await expect(page.getByText(/invalid email or password/i)).toBeVisible();
    await expect(page.getByRole('heading', { name: /sign in to khoi pro/i })).toBeVisible();
  });

  test('reaches the authenticated dashboard with valid credentials', async ({ page }) => {
    await page.goto('/');

    await page.getByPlaceholder('Email address').fill(ADMIN_EMAIL);
    await page.getByPlaceholder('Password').fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: /sign in/i }).click();

    // The Dashboard tab's own heading is a time-of-day greeting ("Good morning, ..."), not the
    // literal word "Dashboard" - see App.jsx's Dashboard Tab section.
    await expect(page.getByRole('heading', { name: /good (morning|afternoon|evening)/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /sign in to khoi pro/i })).not.toBeVisible();
  });
});
