import { test, expect } from '@playwright/test';
import type { Page, Route } from '@playwright/test';

/**
 * E2E Tests for Destination Conflict Resolution (FR-009)
 *
 * Tests the workflow when a user attempts to copy a blob to a destination
 * that already exists. Users should be prompted to choose:
 * - Overwrite: Replace the existing blob
 * - Cancel: Abort the copy operation
 * - Rename: Use a different destination name
 */
test.describe('Destination Conflict Resolution', () => {
  let page: Page;

  test.beforeEach(async ({ page: testPage }) => {
    page = testPage;
    await page.goto('/');
  });

  /**
   * Helper to mock 409 Conflict response from API
   */
  async function mockConflictResponse(destinationUri: string) {
    await page.route('**/api/blobcopy/start', async (route: Route) => {
      const request = route.request();
      const postData = request.postDataJSON();

      // If not requesting overwrite, return 409 Conflict
      if (!postData?.overwriteIfExists && !postData?.newDestinationName) {
        await route.fulfill({
          status: 409,
          contentType: 'application/json',
          body: JSON.stringify({
            code: 'DESTINATION_EXISTS',
            destinationUri: destinationUri,
            message: `Destination blob already exists: ${destinationUri}`,
          }),
        });
      } else {
        // Allow the request to proceed (will need backend or mock success)
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'test-operation-id',
            status: 'Running',
            sourceUri: postData.sourceUri,
            destinationUri: postData.newDestinationName
              ? destinationUri.replace(/[^/]+$/, postData.newDestinationName)
              : destinationUri,
            bytesTransferred: 0,
            totalBytes: 1024000,
            startedAt: new Date().toISOString(),
          }),
        });
      }
    });
  }

  /**
   * Helper to fill in the copy form
   */
  async function fillCopyForm(sourceUri: string, destUri: string) {
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill(sourceUri);
    await destInput.fill(destUri);
  }

  test('should show conflict dialog when destination exists', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Conflict dialog should appear
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible({ timeout: 5000 });

    // Should show the destination URI
    await expect(conflictDialog).toContainText('existing-blob.txt');

    // Should show the dialog title
    await expect(conflictDialog).toContainText('Destination Blob Already Exists');
  });

  test('should display all three resolution options', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Check all three buttons are present
    const overwriteButton = page.getByTestId('overwrite-button');
    const renameButton = page.getByTestId('rename-button');
    const cancelButton = page.getByTestId('cancel-button');

    await expect(overwriteButton).toBeVisible();
    await expect(renameButton).toBeVisible();
    await expect(cancelButton).toBeVisible();
  });

  test('should cancel copy when cancel is selected', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click cancel
    const cancelButton = page.getByTestId('cancel-button');
    await cancelButton.click();

    // Dialog should close
    await expect(conflictDialog).not.toBeVisible();

    // Form should be visible again (user can retry)
    const sourceInput = page.getByTestId('source-uri-input');
    await expect(sourceInput).toBeVisible();
  });

  test('should proceed with overwrite when overwrite is selected', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click overwrite
    const overwriteButton = page.getByTestId('overwrite-button');
    await overwriteButton.click();

    // Dialog should close and copy should start
    await expect(conflictDialog).not.toBeVisible();

    // Progress display should appear (or loading state)
    const progressOrLoading = page.locator('[data-testid="progress-display"], [data-testid="loading-state"]');
    await expect(progressOrLoading).toBeVisible({ timeout: 5000 });
  });

  test('should show rename input when rename is selected', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click rename
    const renameButton = page.getByTestId('rename-button');
    await renameButton.click();

    // Rename input should appear
    const renameInput = page.getByTestId('rename-input');
    await expect(renameInput).toBeVisible();

    // Should have submit and cancel buttons for rename
    const renameSubmitButton = page.getByTestId('rename-submit-button');
    const renameCancelButton = page.getByTestId('rename-cancel-button');

    await expect(renameSubmitButton).toBeVisible();
    await expect(renameCancelButton).toBeVisible();
  });

  test('should proceed with new name when rename is submitted', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click rename
    const renameButton = page.getByTestId('rename-button');
    await renameButton.click();

    // Enter new name
    const renameInput = page.getByTestId('rename-input');
    await renameInput.clear();
    await renameInput.fill('new-blob-name.txt');

    // Submit
    const renameSubmitButton = page.getByTestId('rename-submit-button');
    await renameSubmitButton.click();

    // Dialog should close and copy should start with new name
    await expect(conflictDialog).not.toBeVisible();

    // Progress display should appear
    const progressOrLoading = page.locator('[data-testid="progress-display"], [data-testid="loading-state"]');
    await expect(progressOrLoading).toBeVisible({ timeout: 5000 });
  });

  test('should validate rename input is not empty', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click rename
    const renameButton = page.getByTestId('rename-button');
    await renameButton.click();

    // Clear input and try to submit empty
    const renameInput = page.getByTestId('rename-input');
    await renameInput.clear();

    const renameSubmitButton = page.getByTestId('rename-submit-button');
    await renameSubmitButton.click();

    // Should show error
    await expect(page.getByText('Please enter a valid name')).toBeVisible();

    // Dialog should still be visible
    await expect(conflictDialog).toBeVisible();
  });

  test('should validate rename is different from original', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click rename
    const renameButton = page.getByTestId('rename-button');
    await renameButton.click();

    // Try to submit with same name (pre-filled)
    const renameSubmitButton = page.getByTestId('rename-submit-button');
    await renameSubmitButton.click();

    // Should show error
    await expect(page.getByText('New name must be different from the original')).toBeVisible();
  });

  test('should go back from rename view when cancel is clicked', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click rename
    const renameButton = page.getByTestId('rename-button');
    await renameButton.click();

    // Rename input should be visible
    const renameInput = page.getByTestId('rename-input');
    await expect(renameInput).toBeVisible();

    // Click back/cancel
    const renameCancelButton = page.getByTestId('rename-cancel-button');
    await renameCancelButton.click();

    // Should go back to main options
    await expect(renameInput).not.toBeVisible();
    await expect(renameButton).toBeVisible();
  });

  test('should disable buttons during processing', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';

    // Mock slow response to see processing state
    await page.route('**/api/blobcopy/start', async (route: Route) => {
      const request = route.request();
      const postData = request.postDataJSON();

      if (!postData?.overwriteIfExists) {
        await route.fulfill({
          status: 409,
          contentType: 'application/json',
          body: JSON.stringify({
            code: 'DESTINATION_EXISTS',
            destinationUri: destUri,
          }),
        });
      } else {
        // Delay response to simulate processing
        await new Promise(resolve => setTimeout(resolve, 2000));
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'test-operation-id',
            status: 'Running',
          }),
        });
      }
    });

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Click overwrite (don't await)
    const overwriteButton = page.getByTestId('overwrite-button');
    await overwriteButton.click();

    // Buttons should be disabled during processing
    await expect(overwriteButton).toBeDisabled();
  });

  test('should have proper accessibility attributes on dialog', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Should have aria-modal
    await expect(dialog).toHaveAttribute('aria-modal', 'true');

    // Should have aria-labelledby pointing to title
    await expect(dialog).toHaveAttribute('aria-labelledby', 'conflict-dialog-title');
  });

  test('should support keyboard navigation in dialog', async () => {
    const destUri = 'https://account.blob.core.windows.net/container/existing-blob.txt';
    await mockConflictResponse(destUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      destUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Tab through buttons
    const overwriteButton = page.getByTestId('overwrite-button');
    const renameButton = page.getByTestId('rename-button');
    const cancelButton = page.getByTestId('cancel-button');

    await overwriteButton.focus();
    await expect(overwriteButton).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(renameButton).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(cancelButton).toBeFocused();

    // Enter should activate button
    await page.keyboard.press('Enter');

    // Dialog should close
    await expect(conflictDialog).not.toBeVisible();
  });

  test('should truncate long destination URIs in display', async () => {
    const longDestUri = 'https://verylongstorageaccountname.blob.core.windows.net/container/path/to/very/long/blob/name/that/should/be/truncated.txt';
    await mockConflictResponse(longDestUri);

    await fillCopyForm(
      'https://account.blob.core.windows.net/source/blob.txt',
      longDestUri
    );

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for dialog
    const conflictDialog = page.getByTestId('overwrite-confirm-dialog');
    await expect(conflictDialog).toBeVisible();

    // Should show truncated URI with ellipsis
    const uriElement = page.locator(`[title="${longDestUri}"]`);
    await expect(uriElement).toBeVisible();

    const uriText = await uriElement.textContent();
    expect(uriText).toContain('...');
  });
});
