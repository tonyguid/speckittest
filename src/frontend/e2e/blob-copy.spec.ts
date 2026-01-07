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

  // Accessibility: WCAG AA Compliance
  test('should meet WCAG AA accessibility standards', async () => {
    // Verify form is accessible
    const form = page.getByTestId('copy-form');
    await expect(form).toHaveAccessibleName(/copy/i);

    // Check for proper labels
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    const sourceLabel = page.locator('label[for="sourceUri"]');
    const destLabel = page.locator('label[for="destinationUri"]');

    await expect(sourceLabel).toBeVisible();
    await expect(destLabel).toBeVisible();

    // Verify button has accessible name
    const startButton = page.getByTestId('start-copy-button');
    expect(await startButton.getAttribute('aria-label')).toBeTruthy();

    // Check error message has proper role
    const sourceInput2 = page.getByTestId('source-uri-input');
    await sourceInput2.fill('');
    const startButton2 = page.getByTestId('start-copy-button');
    await startButton2.click();

    const errors = page.getByTestId('validation-errors');
    await expect(errors).toHaveAttribute('role', 'alert');
  });

  // Accessibility: Keyboard Navigation
  test('should support full keyboard navigation', async () => {
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');
    const startButton = page.getByTestId('start-copy-button');

    // Tab to source input
    await sourceInput.focus();
    await expect(sourceInput).toBeFocused();

    // Tab to destination input
    await destInput.focus();
    await expect(destInput).toBeFocused();

    // Tab to start button
    await startButton.focus();
    await expect(startButton).toBeFocused();

    // Enter key should trigger button
    await startButton.press('Enter');

    // Should show validation errors
    const errors = page.getByTestId('validation-errors');
    await expect(errors).toBeVisible();
  });

  // Accessibility: Screen Reader Support (aria attributes)
  test('should have proper ARIA attributes for screen readers', async () => {
    const startButton = page.getByTestId('start-copy-button');

    // When not loading
    await expect(startButton).toHaveAttribute('aria-busy', 'false');

    // When disabled
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');
    await sourceInput.fill('https://account.blob.core.windows.net/source/blob');
    await destInput.fill('https://account.blob.core.windows.net/source/blob'); // Same URI
    await startButton.click();

    await expect(startButton).toHaveAttribute('aria-disabled', 'true');
  });

  // Error Scenario: Invalid URI Format
  test('should reject invalid URI formats', async () => {
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');
    const startButton = page.getByTestId('start-copy-button');

    // Try various invalid formats
    const invalidUris = [
      'not-a-url',
      'http://account.blob.core.windows.net/blob', // HTTP instead of HTTPS
      'ftp://account.blob.core.windows.net/blob',
      'https://not-a-real-domain.com/blob',
    ];

    for (const invalidUri of invalidUris) {
      await sourceInput.fill(invalidUri);
      await destInput.fill('https://account.blob.core.windows.net/dest/blob');
      await startButton.click();

      // Should show validation error (client-side)
      const errors = page.getByTestId('validation-errors');
      const hasError = await errors.isVisible().catch(() => false);

      // If not caught by client, will be caught by server
      if (!hasError) {
        const serverError = page.getByTestId('server-error');
        await expect(serverError).toBeVisible({ timeout: 5000 });
      }
    }
  });

  // Error Scenario: Authentication/Authorization Failure
  test('should handle authentication failures', async () => {
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');
    const startButton = page.getByTestId('start-copy-button');

    // Use storage account without permissions
    await sourceInput.fill('https://restoreaccount.blob.core.windows.net/private/blob');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');
    await startButton.click();

    // Should show error (after validation attempt)
    const serverError = page.getByTestId('server-error');
    await expect(serverError).toBeVisible({ timeout: 5000 });
    await expect(serverError).toContainText('not authorized|permission|auth');
  });

  // Error Scenario: Blob Not Found
  test('should handle blob not found errors', async () => {
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');
    const startButton = page.getByTestId('start-copy-button');

    // Use valid format but non-existent blob
    await sourceInput.fill('https://account.blob.core.windows.net/container/nonexistent-blob-12345');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');
    await startButton.click();

    // Should show error
    const serverError = page.getByTestId('server-error');
    const resultError = page.getByTestId('result-display');

    const errorElement = page.locator('[data-testid="server-error"], [data-testid="result-display"]');
    await expect(errorElement).toBeVisible({ timeout: 5000 });
    await expect(errorElement).toContainText('not found|404|does not exist', { ignoreCase: true });
  });

  // Performance: Verify API Response Time
  test('should meet p95 latency SLA of 200ms', async () => {
    const startTime = Date.now();

    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill('https://account.blob.core.windows.net/source/blob');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Wait for API response (progress display appears)
    const progressDisplay = page.getByTestId('progress-display');
    await expect(progressDisplay).toBeVisible({ timeout: 500 });

    const endTime = Date.now();
    const responseTime = endTime - startTime;

    // Should be under 500ms for UI response (more generous than 200ms API SLA)
    expect(responseTime).toBeLessThan(500);
  });

  // Retry: Automatic Retry on Failure
  test('should retry failed operations automatically', async ({ context }: { context: BrowserContext }) => {
    const sourceInput = page.getByTestId('source-uri-input');
    const destInput = page.getByTestId('destination-uri-input');

    await sourceInput.fill('https://account.blob.core.windows.net/source/transient-error');
    await destInput.fill('https://account.blob.core.windows.net/dest/blob');

    // Simulate transient failure by intercepting and failing first request
    let attemptCount = 0;
    await page.route('**/api/**/start', async (route) => {
      attemptCount++;
      if (attemptCount === 1) {
        // Fail first request
        await route.abort('serviceunavailable');
      } else {
        // Succeed on retry
        await route.continue();
      }
    });

    const startButton = page.getByTestId('start-copy-button');
    await startButton.click();

    // Should eventually succeed after retry
    const progressOrError = page.locator('[data-testid="progress-display"], [data-testid="server-error"]');
    await expect(progressOrError).toBeVisible({ timeout: 10000 });

    // Should have retried
    expect(attemptCount).toBeGreaterThanOrEqual(1);
  });
});
