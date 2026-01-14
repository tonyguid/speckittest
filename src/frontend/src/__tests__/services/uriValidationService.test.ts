import { describe, it, expect } from 'vitest';
import {
  validateUri,
  checkIdenticalUris,
  validateCopyRequest,
  extractAccountName,
  extractContainerName,
  extractBlobName,
} from '../../services/uriValidationService';

describe('uriValidationService', () => {
  describe('validateUri', () => {
    describe('valid URIs', () => {
      it('accepts a valid Azure Blob Storage URI', () => {
        const result = validateUri('https://myaccount.blob.core.windows.net/mycontainer/myblob.txt');
        expect(result.valid).toBe(true);
        expect(result.error).toBeUndefined();
      });

      it('accepts URI with nested blob path', () => {
        const result = validateUri('https://storage.blob.core.windows.net/container/path/to/blob.txt');
        expect(result.valid).toBe(true);
      });

      it('accepts URI with hyphens in container name', () => {
        const result = validateUri('https://account.blob.core.windows.net/my-container/blob.txt');
        expect(result.valid).toBe(true);
      });

      it('accepts URI with numbers in account and container', () => {
        const result = validateUri('https://account123.blob.core.windows.net/container456/blob.txt');
        expect(result.valid).toBe(true);
      });

      it('accepts URI with special characters in blob name', () => {
        const result = validateUri('https://account.blob.core.windows.net/container/blob-name_v2.1.txt');
        expect(result.valid).toBe(true);
      });

      it('accepts URI with whitespace that gets trimmed', () => {
        const result = validateUri('  https://account.blob.core.windows.net/container/blob.txt  ');
        expect(result.valid).toBe(true);
      });
    });

    describe('invalid URIs - empty or missing', () => {
      it('rejects empty string', () => {
        const result = validateUri('');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI is required');
      });

      it('rejects whitespace only', () => {
        const result = validateUri('   ');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI is required');
      });

      it('rejects null-like values', () => {
        const result = validateUri(null as unknown as string);
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI is required');
      });

      it('rejects undefined', () => {
        const result = validateUri(undefined as unknown as string);
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI is required');
      });
    });

    describe('invalid URIs - protocol', () => {
      it('rejects HTTP (non-HTTPS) URIs', () => {
        const result = validateUri('http://account.blob.core.windows.net/container/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI must use HTTPS protocol');
      });

      it('rejects FTP URIs', () => {
        const result = validateUri('ftp://account.blob.core.windows.net/container/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI must use HTTPS protocol');
      });

      it('rejects URIs without protocol', () => {
        const result = validateUri('account.blob.core.windows.net/container/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI must use HTTPS protocol');
      });
    });

    describe('invalid URIs - domain', () => {
      it('rejects non-Azure Blob Storage domains', () => {
        const result = validateUri('https://example.com/container/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI must be a valid Azure Blob Storage URL (*.blob.core.windows.net)');
      });

      it('rejects AWS S3 URLs', () => {
        const result = validateUri('https://mybucket.s3.amazonaws.com/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI must be a valid Azure Blob Storage URL (*.blob.core.windows.net)');
      });

      it('rejects Google Cloud Storage URLs', () => {
        const result = validateUri('https://storage.googleapis.com/bucket/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('URI must be a valid Azure Blob Storage URL (*.blob.core.windows.net)');
      });
    });

    describe('invalid URIs - format', () => {
      it('rejects URI without container', () => {
        const result = validateUri('https://account.blob.core.windows.net/');
        expect(result.valid).toBe(false);
        expect(result.error).toContain('Invalid Azure Blob Storage URI format');
      });

      it('rejects URI with only container, no blob', () => {
        const result = validateUri('https://account.blob.core.windows.net/container/');
        expect(result.valid).toBe(false);
        // Regex fails before reaching specific blob name validation
        expect(result.error).toContain('Invalid Azure Blob Storage URI format');
      });

      it('rejects URI with uppercase in account name', () => {
        const result = validateUri('https://MyAccount.blob.core.windows.net/container/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toContain('Invalid Azure Blob Storage URI format');
      });
    });

    describe('invalid URIs - container name rules', () => {
      it('rejects container name shorter than 3 characters', () => {
        const result = validateUri('https://account.blob.core.windows.net/ab/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('Container name must be between 3 and 63 characters');
      });

      it('rejects container name starting with hyphen', () => {
        const result = validateUri('https://account.blob.core.windows.net/-container/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('Container name cannot start or end with a hyphen');
      });

      it('rejects container name ending with hyphen', () => {
        const result = validateUri('https://account.blob.core.windows.net/container-/blob.txt');
        expect(result.valid).toBe(false);
        expect(result.error).toBe('Container name cannot start or end with a hyphen');
      });

      it('rejects container name with uppercase letters', () => {
        const result = validateUri('https://account.blob.core.windows.net/MyContainer/blob.txt');
        expect(result.valid).toBe(false);
        // Regex fails before reaching specific container validation
        expect(result.error).toContain('Invalid Azure Blob Storage URI format');
      });

      it('rejects container name with underscores', () => {
        const result = validateUri('https://account.blob.core.windows.net/my_container/blob.txt');
        expect(result.valid).toBe(false);
        // Regex fails before reaching specific container validation
        expect(result.error).toContain('Invalid Azure Blob Storage URI format');
      });
    });

    describe('invalid URIs - blob name rules', () => {
      it('rejects blob name exceeding 1024 characters', () => {
        const longBlobName = 'a'.repeat(1025);
        const result = validateUri(`https://account.blob.core.windows.net/container/${longBlobName}`);
        expect(result.valid).toBe(false);
        expect(result.error).toBe('Blob name cannot exceed 1024 characters');
      });

      it('accepts blob name at exactly 1024 characters', () => {
        const maxBlobName = 'a'.repeat(1024);
        const result = validateUri(`https://account.blob.core.windows.net/container/${maxBlobName}`);
        expect(result.valid).toBe(true);
      });
    });
  });

  describe('checkIdenticalUris', () => {
    it('returns true for identical URIs', () => {
      const uri = 'https://account.blob.core.windows.net/container/blob.txt';
      expect(checkIdenticalUris(uri, uri)).toBe(true);
    });

    it('returns true for URIs differing only in case', () => {
      const source = 'https://Account.blob.core.windows.net/Container/Blob.txt';
      const dest = 'https://account.blob.core.windows.net/container/blob.txt';
      expect(checkIdenticalUris(source, dest)).toBe(true);
    });

    it('returns true for URIs with different whitespace', () => {
      const source = '  https://account.blob.core.windows.net/container/blob.txt  ';
      const dest = 'https://account.blob.core.windows.net/container/blob.txt';
      expect(checkIdenticalUris(source, dest)).toBe(true);
    });

    it('returns false for different URIs', () => {
      const source = 'https://account.blob.core.windows.net/container/blob1.txt';
      const dest = 'https://account.blob.core.windows.net/container/blob2.txt';
      expect(checkIdenticalUris(source, dest)).toBe(false);
    });

    it('returns false for URIs with different containers', () => {
      const source = 'https://account.blob.core.windows.net/container1/blob.txt';
      const dest = 'https://account.blob.core.windows.net/container2/blob.txt';
      expect(checkIdenticalUris(source, dest)).toBe(false);
    });

    it('returns false for URIs with different accounts', () => {
      const source = 'https://account1.blob.core.windows.net/container/blob.txt';
      const dest = 'https://account2.blob.core.windows.net/container/blob.txt';
      expect(checkIdenticalUris(source, dest)).toBe(false);
    });

    it('returns false when source is empty', () => {
      expect(checkIdenticalUris('', 'https://account.blob.core.windows.net/container/blob.txt')).toBe(false);
    });

    it('returns false when destination is empty', () => {
      expect(checkIdenticalUris('https://account.blob.core.windows.net/container/blob.txt', '')).toBe(false);
    });

    it('returns false when both are empty', () => {
      expect(checkIdenticalUris('', '')).toBe(false);
    });

    it('returns false when source is null', () => {
      expect(checkIdenticalUris(null as unknown as string, 'https://account.blob.core.windows.net/container/blob.txt')).toBe(false);
    });

    it('returns false when destination is null', () => {
      expect(checkIdenticalUris('https://account.blob.core.windows.net/container/blob.txt', null as unknown as string)).toBe(false);
    });
  });

  describe('validateCopyRequest', () => {
    it('returns valid results for valid source and destination', () => {
      const result = validateCopyRequest(
        'https://source.blob.core.windows.net/container/blob.txt',
        'https://dest.blob.core.windows.net/container/blob.txt'
      );

      expect(result.sourceValidation.valid).toBe(true);
      expect(result.destinationValidation.valid).toBe(true);
      expect(result.identicalUris).toBe(false);
    });

    it('detects identical URIs when both are valid', () => {
      const uri = 'https://account.blob.core.windows.net/container/blob.txt';
      const result = validateCopyRequest(uri, uri);

      expect(result.sourceValidation.valid).toBe(true);
      expect(result.destinationValidation.valid).toBe(true);
      expect(result.identicalUris).toBe(true);
    });

    it('returns invalid source validation for invalid source', () => {
      const result = validateCopyRequest(
        'http://invalid.blob.core.windows.net/container/blob.txt',
        'https://dest.blob.core.windows.net/container/blob.txt'
      );

      expect(result.sourceValidation.valid).toBe(false);
      expect(result.sourceValidation.error).toBe('URI must use HTTPS protocol');
      expect(result.destinationValidation.valid).toBe(true);
      expect(result.identicalUris).toBe(false);
    });

    it('returns invalid destination validation for invalid destination', () => {
      const result = validateCopyRequest(
        'https://source.blob.core.windows.net/container/blob.txt',
        'invalid-uri'
      );

      expect(result.sourceValidation.valid).toBe(true);
      expect(result.destinationValidation.valid).toBe(false);
      expect(result.identicalUris).toBe(false);
    });

    it('returns both invalid when both URIs are invalid', () => {
      const result = validateCopyRequest('', '');

      expect(result.sourceValidation.valid).toBe(false);
      expect(result.destinationValidation.valid).toBe(false);
      expect(result.identicalUris).toBe(false);
    });

    it('does not check identical URIs when source is invalid', () => {
      const result = validateCopyRequest(
        '',
        'https://account.blob.core.windows.net/container/blob.txt'
      );

      expect(result.identicalUris).toBe(false);
    });
  });

  describe('extractAccountName', () => {
    it('extracts account name from valid URI', () => {
      expect(extractAccountName('https://myaccount.blob.core.windows.net/container/blob.txt')).toBe('myaccount');
    });

    it('extracts account name with numbers', () => {
      expect(extractAccountName('https://account123.blob.core.windows.net/container/blob.txt')).toBe('account123');
    });

    it('returns null for empty string', () => {
      expect(extractAccountName('')).toBeNull();
    });

    it('returns null for null input', () => {
      expect(extractAccountName(null as unknown as string)).toBeNull();
    });

    it('returns null for non-Azure URI', () => {
      expect(extractAccountName('https://example.com/path/file.txt')).toBeNull();
    });

    it('returns null for malformed Azure URI', () => {
      expect(extractAccountName('https://blob.core.windows.net/container/blob.txt')).toBeNull();
    });

    it('returns null for URI with uppercase account (pattern mismatch)', () => {
      expect(extractAccountName('https://MyAccount.blob.core.windows.net/container/blob.txt')).toBeNull();
    });
  });

  describe('extractContainerName', () => {
    it('extracts container name from valid URI', () => {
      expect(extractContainerName('https://account.blob.core.windows.net/mycontainer/blob.txt')).toBe('mycontainer');
    });

    it('extracts container name with hyphens', () => {
      expect(extractContainerName('https://account.blob.core.windows.net/my-container/blob.txt')).toBe('my-container');
    });

    it('extracts container name when blob has nested path', () => {
      expect(extractContainerName('https://account.blob.core.windows.net/container/path/to/blob.txt')).toBe('container');
    });

    it('returns null for empty string', () => {
      expect(extractContainerName('')).toBeNull();
    });

    it('returns null for null input', () => {
      expect(extractContainerName(null as unknown as string)).toBeNull();
    });

    it('returns null for non-Azure URI', () => {
      expect(extractContainerName('https://example.com/path/file.txt')).toBeNull();
    });

    it('returns null for URI without container', () => {
      expect(extractContainerName('https://account.blob.core.windows.net/')).toBeNull();
    });
  });

  describe('extractBlobName', () => {
    it('extracts blob name from valid URI', () => {
      expect(extractBlobName('https://account.blob.core.windows.net/container/myblob.txt')).toBe('myblob.txt');
    });

    it('extracts nested blob path', () => {
      expect(extractBlobName('https://account.blob.core.windows.net/container/path/to/blob.txt')).toBe('path/to/blob.txt');
    });

    it('extracts blob name with special characters', () => {
      expect(extractBlobName('https://account.blob.core.windows.net/container/my-blob_v2.1.txt')).toBe('my-blob_v2.1.txt');
    });

    it('returns null for empty string', () => {
      expect(extractBlobName('')).toBeNull();
    });

    it('returns null for null input', () => {
      expect(extractBlobName(null as unknown as string)).toBeNull();
    });

    it('returns null for non-Azure URI', () => {
      expect(extractBlobName('https://example.com/path/file.txt')).toBeNull();
    });

    it('returns null for URI with only container', () => {
      expect(extractBlobName('https://account.blob.core.windows.net/container/')).toBeNull();
    });

    it('returns empty string as null for URI without blob', () => {
      expect(extractBlobName('https://account.blob.core.windows.net/container')).toBeNull();
    });
  });
});
