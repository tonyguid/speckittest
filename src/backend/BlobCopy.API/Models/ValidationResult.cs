namespace BlobCopy.API.Models;

/// <summary>
/// Result of URI validation containing validation status and any errors.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed (no errors).
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors, empty if validation passed.
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string field, string message, string code) => new()
    {
        IsValid = false,
        Errors = new List<ValidationError>
        {
            new ValidationError { Field = field, Message = message, Code = code }
        }
    };
}
