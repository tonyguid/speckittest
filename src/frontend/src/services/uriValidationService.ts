/**
 * URI Validation Service
 * 
 * Client-side validation for Azure Blob Storage URIs.
 * Validates format before sending to API to reduce unnecessary network calls.
 */

export interface ValidationResult {
  valid: boolean;
  error?: string;
}

/**
 * Validates an Azure Blob Storage URI format.
 * 
 * Expected format: https://[account].blob.core.windows.net/[container]/[blob]
 * 
 * @param uri - The URI to validate
 * @returns Validation result with error message if invalid
 */
export function validateUri(uri: string): ValidationResult {
  // Check if URI is provided
  if (!uri || uri.trim() === '') {
    return {
      valid: false,
      error: 'URI is required'
    };
  }

  const trimmedUri = uri.trim();

  // Check for HTTPS protocol
  if (!trimmedUri.startsWith('https://')) {
    return {
      valid: false,
      error: 'URI must use HTTPS protocol'
    };
  }

  // Check for valid Azure Blob Storage domain
  if (!trimmedUri.includes('.blob.core.windows.net/')) {
    return {
      valid: false,
      error: 'URI must be a valid Azure Blob Storage URL (*.blob.core.windows.net)'
    };
  }

  // Regex pattern for Azure Blob Storage URI
  // Format: https://[account].blob.core.windows.net/[container]/[blob-path]
  // - account: alphanumeric, lowercase
  // - container: alphanumeric and hyphens, lowercase
  // - blob: any valid blob name (can include path separators)
  const azureBlobUriPattern = /^https:\/\/[a-z0-9]+\.blob\.core\.windows\.net\/[a-z0-9-]+\/.+$/;

  if (!azureBlobUriPattern.test(trimmedUri)) {
    return {
      valid: false,
      error: 'Invalid Azure Blob Storage URI format. Expected: https://[account].blob.core.windows.net/[container]/[blob]'
    };
  }

  // Extract container and blob name for additional validation
  const uriParts = trimmedUri.split('.blob.core.windows.net/')[1];
  if (!uriParts) {
    return {
      valid: false,
      error: 'URI must contain container and blob name'
    };
  }

  const pathParts = uriParts.split('/');
  const containerName = pathParts[0];
  const blobName = pathParts.slice(1).join('/');

  // Validate container name
  if (!containerName || containerName.length < 3 || containerName.length > 63) {
    return {
      valid: false,
      error: 'Container name must be between 3 and 63 characters'
    };
  }

  if (containerName.startsWith('-') || containerName.endsWith('-')) {
    return {
      valid: false,
      error: 'Container name cannot start or end with a hyphen'
    };
  }

  if (!/^[a-z0-9-]+$/.test(containerName)) {
    return {
      valid: false,
      error: 'Container name must contain only lowercase letters, numbers, and hyphens'
    };
  }

  // Validate blob name exists
  if (!blobName || blobName.trim() === '') {
    return {
      valid: false,
      error: 'Blob name is required'
    };
  }

  // Blob name length check (max 1024 characters)
  if (blobName.length > 1024) {
    return {
      valid: false,
      error: 'Blob name cannot exceed 1024 characters'
    };
  }

  return {
    valid: true
  };
}

/**
 * Checks if source and destination URIs are identical.
 * 
 * @param sourceUri - Source blob URI
 * @param destinationUri - Destination blob URI
 * @returns true if URIs are identical (case-insensitive), false otherwise
 */
export function checkIdenticalUris(sourceUri: string, destinationUri: string): boolean {
  if (!sourceUri || !destinationUri) {
    return false;
  }

  // Normalize URIs: trim whitespace and convert to lowercase for comparison
  const normalizedSource = sourceUri.trim().toLowerCase();
  const normalizedDestination = destinationUri.trim().toLowerCase();

  return normalizedSource === normalizedDestination;
}

/**
 * Validates both source and destination URIs and checks for common issues.
 * 
 * @param sourceUri - Source blob URI
 * @param destinationUri - Destination blob URI
 * @returns Object with validation results for both URIs
 */
export function validateCopyRequest(
  sourceUri: string,
  destinationUri: string
): {
  sourceValidation: ValidationResult;
  destinationValidation: ValidationResult;
  identicalUris: boolean;
} {
  const sourceValidation = validateUri(sourceUri);
  const destinationValidation = validateUri(destinationUri);
  const identicalUris = sourceValidation.valid && destinationValidation.valid 
    ? checkIdenticalUris(sourceUri, destinationUri)
    : false;

  return {
    sourceValidation,
    destinationValidation,
    identicalUris
  };
}

/**
 * Extracts the account name from an Azure Blob Storage URI.
 * 
 * @param uri - Azure Blob Storage URI
 * @returns Account name or null if URI is invalid
 */
export function extractAccountName(uri: string): string | null {
  if (!uri) return null;

  const match = uri.match(/^https:\/\/([a-z0-9]+)\.blob\.core\.windows\.net\//);
  return match ? match[1] : null;
}

/**
 * Extracts the container name from an Azure Blob Storage URI.
 * 
 * @param uri - Azure Blob Storage URI
 * @returns Container name or null if URI is invalid
 */
export function extractContainerName(uri: string): string | null {
  if (!uri) return null;

  const uriParts = uri.split('.blob.core.windows.net/')[1];
  if (!uriParts) return null;

  const pathParts = uriParts.split('/');
  return pathParts[0] || null;
}

/**
 * Extracts the blob name (path) from an Azure Blob Storage URI.
 * 
 * @param uri - Azure Blob Storage URI
 * @returns Blob name/path or null if URI is invalid
 */
export function extractBlobName(uri: string): string | null {
  if (!uri) return null;

  const uriParts = uri.split('.blob.core.windows.net/')[1];
  if (!uriParts) return null;

  const pathParts = uriParts.split('/');
  const blobPath = pathParts.slice(1).join('/');
  
  return blobPath || null;
}
