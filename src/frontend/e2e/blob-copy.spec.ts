import { test, expect } from '@playwright/test';
import type { Page, BrowserContext } from '@playwright/test';

// Configure test to run on Chrome, Firefox, and Safari
test.describe.parallel('Blob Copy E2E Tests', () => {
  let page: Page;

  test.beforeEach(async ({ page: testPage }: { page: Page }) => {
    page = testPage;
    // Navigate to the application
    await page.goto('http://localhost:3000');
  });

  test.afterEach(async () => {
    await page.close();
  });

  // Scenario 1: Form Validation
  test('should validate form before copy', async () => {
    // Try to submit empty form
    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Should show validation errors
    const errors = page.getByTestId('validation-errors');
    await expect(errors).toBeVisible();
    await expect(errors).toContainText('Source URI is required');
    await expect(errors).toContainText('Destination URI is required');

    // Fill only source URI
    const sourceInput = page.getByTestId('source-uri-input');
    await sourceInput.fill('https://account.blob.core.windows.net/source/blob');
    await startButton.click();

    // Should still show destination required error
    await expect(errors).toContainText('Destination URI is required');

    // Fill destination with same URI as source
    const destInput = page.getByTestId('destination-uri-input');
    await destInput.fill('https://account.blob.core.windows.net/source/blob');
    await startButton.click();

    // Should show different URIs error
    await expect(errors).toContainText(
      'Source and destination URIs must be different'
    );
  });

  // Scenario 2: Successful Copy Workflow (Start to Finish)
  test('should complete successful copy workflow', async () => {
    // Fill in valid URIs
    const sourceUri = 'https://account.blob.core.windows.net/source/largefile.vhd';
    const destUri = 'https://account.blob.core.windows.net/dest/largefile-copy.vhd';

    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill(sourceUri);
    await destInput.fill(destUri);

    // Start copy
    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Form should hide
    await expect(sourceInput).not.toBeVisible();

    // Progress display should appear
    const progressDisplay = page.getByTestId('progress-display');
    await expect(progressDisplay).toBeVisible();

    // Should show operation ID
    await expect(progressDisplay).toContainText('Operation ID:');

    // Progress should update over time
    const progressPercentage = page.getByTestId('progress-percentage');
    const initialText = await progressPercentage.textContent();

    // Wait for progress to update
    await page.waitForTimeout(2000);

    // Eventually operation completes
    const resultDisplay = page.getByTestId('result-display');
    await expect(resultDisplay).toBeVisible(
      { timeout: 30000 } // Wait up to 30 seconds for completion
    );

    // Verify success message
    await expect(resultDisplay).toContainText('✅ Copy Completed Successfully');
    await expect(resultDisplay).toContainText(sourceUri);
    await expect(resultDisplay).toContainText(destUri);
  });

  // Scenario 3: Monitor Progress Updates
  test('should display progress updates in real-time', async () => {
    // Start a copy operation
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill('https://account.blob.core.windows.net/source/file.vhd');
    await destInput.fill('https://account.blob.core.windows.net/dest/file-copy.vhd');

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for progress display
    const progressDisplay = page.getByTestId('progress-display');
    await expect(progressDisplay).toBeVisible();

    // Monitor progress percentage
    const progressPercentage = page.getByTestId('progress-percentage');
    const bytesTransferred = page.getByTestId('bytes-transferred');
    const transferRate = page.getByTestId('transfer-rate');

    // Collect progress snapshots
    const progressSnapshots: string[] = [];

    for (let i = 0; i < 10; i++) {
      const percentText = await progressPercentage.textContent();
      progressSnapshots.push(percentText || '');

      // Wait before checking again
      await page.waitForTimeout(1000);

      // Stop if completed
      const resultDisplay = page.locator('[data-testid="result-display"]');
      if (await resultDisplay.isVisible().catch(() => false)) {
        break;
      }
    }

    // Progress should have been shown (not just "--")
    const actualProgress = progressSnapshots.filter(
      (p) => p !== '--:--' && p !== 'Calculating...'
    );
    expect(actualProgress.length).toBeGreaterThan(0);
  });

  // Scenario 4: Cancel Copy Mid-Operation
  test('should cancel copy operation mid-way', async () => {
    // Start a copy operation
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill('https://account.blob.core.windows.net/source/huge-file.vhd');
    await destInput.fill('https://account.blob.core.windows.net/dest/huge-file-copy.vhd');

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for progress to show
    const progressDisplay = page.getByTestId('progress-display');
    await expect(progressDisplay).toBeVisible();

    // Give it a moment to start copying
    await page.waitForTimeout(2000);

    // Click cancel button
    const cancelButton = page.getByTestId('cancel-button');
    await expect(cancelButton).toBeVisible();
    await cancelButton.click();

    // Button should show cancelling state
    await expect(cancelButton).toContainText('Cancelling...');

    // Eventually should show cancelled result
    const resultDisplay = page.getByTestId('result-display');
    await expect(resultDisplay).toBeVisible({ timeout: 10000 });
    await expect(resultDisplay).toContainText('⚠️ Copy Cancelled');
  });

  // Scenario 5: Handle Copy Failures
  test('should display error message on copy failure', async () => {
    // Use invalid source URI to trigger failure
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill('https://account.blob.core.windows.net/nonexistent/blob');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Should show error (either immediately or after validation)
    const errors = page.getByTestId('server-error');
    await expect(errors).toBeVisible({ timeout: 5000 });

    // Or show failure result
    const resultDisplay = page.locator('[data-testid="result-display"]');
    if (await resultDisplay.isVisible().catch(() => false)) {
      await expect(resultDisplay).toContainText('❌ Copy Failed');
      const errorList = page.getByTestId('error-list');
      await expect(errorList).toBeVisible();
    }
  });

  // Scenario 6: Health Check Endpoint
  test('should verify API health before operations', async () => {
    // The app should have connected to a healthy API
    // We can verify by checking if form is enabled

    const startButton = page.getByTestId('start-copy-button');
    await expect(startButton).toBeEnabled();

    // Optionally check API response directly
    const response = await page.request.get('http://localhost:5000/health');
    expect(response.status()).toBe(200);

    const json = await response.json();
    expect(json).toHaveProperty('status');
  });

  // Scenario 7: Browser Compatibility
  test('should work across browsers', async ({ page: testPage, browserName }: { page: Page; browserName: string }) => {
    page = testPage;
    // This test runs on all configured browsers (Chrome, Firefox, Safari)
    console.log(`Running on: ${browserName}`);

    // Verify UI is accessible
    const form = page.getByTestId('copy-form');
    await expect(form).toBeVisible();

    // Verify form elements are interactive
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');
    const startButton = page.getByTestId('start-copy-button');

    await sourceInput.fill('https://account.blob.core.windows.net/source/blob');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');

    await startButton.click();

    // Progress display should appear on all browsers
    const progressDisplay = page.getByTestId('progress-display');
    await expect(progressDisplay).toBeVisible({ timeout: 5000 });
  });

  // Additional: Form Reset After Completion
  test('should reset form after operation completes', async () => {
    // Complete an operation
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill('https://account.blob.core.windows.net/source/blob');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for completion
    const resultDisplay = page.getByTestId('result-display');
    await expect(resultDisplay).toBeVisible({ timeout: 30000 });

    // Should see completion message
    await expect(resultDisplay).toContainText('Copy Completed Successfully');

    // Should be able to start a new operation
    // (Form should be available again or reset button should appear)
    const newSourceInput = page.getByTestId('source-uri-input');
    await expect(newSourceInput).toBeVisible();
  });

  // Edge case: Network Error Handling
  test('should handle network errors gracefully', async ({ context }: { context: BrowserContext }) => {
    // Simulate network error by disconnecting
    await context.setOffline(true);

    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill('https://account.blob.core.windows.net/source/blob');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');

    // Wait to reconnect
    await context.setOffline(false);

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Application should handle gracefully
    // Either show error or retry
    const errorOrProgress = page.locator('[data-testid="server-error"], [data-testid="progress-display"]');
    await expect(errorOrProgress).toBeVisible({ timeout: 10000 });
  });
});
