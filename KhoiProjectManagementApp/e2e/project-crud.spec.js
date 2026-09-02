// e2e/project-crud.spec.js - critical journey #2: create, edit, and delete a project through the
// real UI/API/DB.
import { test, expect } from '@playwright/test';

const ADMIN_EMAIL = 'kholisa@khoitech.Africa';
const ADMIN_PASSWORD = 'admin123';

test.describe('Project CRUD', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.getByPlaceholder('Email address').fill(ADMIN_EMAIL);
    await page.getByPlaceholder('Password').fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: /sign in/i }).click();
    // The Dashboard tab's own heading is a time-of-day greeting ("Good morning, ..."), not the
    // literal word "Dashboard" - see App.jsx's Dashboard Tab section.
    await expect(page.getByRole('heading', { name: /good (morning|afternoon|evening)/i })).toBeVisible();

    await page.getByRole('button', { name: /^projects$/i }).click();
  });

  test('creates a project, edits it, then deletes it', async ({ page }) => {
    const projectName = `E2E Project ${Date.now()}`;
    const updatedName = `${projectName} (edited)`;

    await page.getByRole('button', { name: /new project/i }).click();
    await page.getByPlaceholder('Project Name').fill(projectName);
    await page.getByPlaceholder('Description').fill('Created by a Playwright E2E test');
    const today = new Date().toISOString().slice(0, 10);
    const nextMonth = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
    await page.locator('input[type="date"]').nth(0).fill(today);
    await page.locator('input[type="date"]').nth(1).fill(nextMonth);
    await page.getByRole('button', { name: /^add project$/i }).click();

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible();

    // Edit it - the card's action area holds Edit3 then Trash2 icon buttons (Admin holds both
    // projects.edit and projects.delete), so the edit button is the first of the two.
    let card = page.locator('div', { has: page.getByRole('heading', { name: projectName }) }).last();
    await card.getByRole('button').first().click();

    await expect(page.getByRole('heading', { name: /edit project/i })).toBeVisible();
    const nameField = page.getByPlaceholder('Project Name');
    await expect(nameField).toHaveValue(projectName);
    await nameField.fill(updatedName);
    await page.getByRole('button', { name: /save changes/i }).click();

    await expect(page.getByRole('heading', { name: updatedName })).toBeVisible();
    await expect(page.getByRole('heading', { name: projectName, exact: true })).not.toBeVisible();

    // Delete it via the trash icon on that (now renamed) card - this opens the app's own confirm
    // modal (ConfirmContext), not a native window.confirm(), so it needs an explicit click rather
    // than Playwright's dialog auto-handling.
    card = page.locator('div', { has: page.getByRole('heading', { name: updatedName }) }).last();
    await card.getByRole('button').last().click();
    await page.getByRole('dialog', { name: /delete project/i }).getByRole('button', { name: /^delete$/i }).click();

    await expect(page.getByRole('heading', { name: updatedName })).not.toBeVisible();
  });
});
