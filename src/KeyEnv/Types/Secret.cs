using System.Text.Json.Serialization;

namespace KeyEnv.Types;

/// <summary>
/// Represents a secret's metadata without the value.
/// </summary>
public record Secret
{
    /// <summary>
    /// Secret ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Secret key name.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// Secret description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Environment ID this secret belongs to.
    /// </summary>
    [JsonPropertyName("environment_id")]
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Type of secret (detected automatically).
    /// </summary>
    [JsonPropertyName("secret_type")]
    public string? SecretType { get; init; }

    /// <summary>
    /// Version number.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; init; }

    /// <summary>
    /// When the secret was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the secret was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Represents a secret including its decrypted value.
/// </summary>
public record SecretWithValue : Secret
{
    /// <summary>
    /// The decrypted secret value.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// Represents a secret with inheritance information.
/// </summary>
public record SecretWithInheritance : Secret
{
    /// <summary>
    /// Environment name this secret was inherited from.
    /// </summary>
    [JsonPropertyName("inherited_from")]
    public string? InheritedFrom { get; init; }
}

/// <summary>
/// Represents a secret with value and inheritance information.
/// </summary>
public record SecretWithValueAndInheritance : SecretWithValue
{
    /// <summary>
    /// Environment name this secret was inherited from.
    /// </summary>
    [JsonPropertyName("inherited_from")]
    public string? InheritedFrom { get; init; }
}

/// <summary>
/// Input for creating or importing a secret.
/// </summary>
public record SecretInput
{
    /// <summary>
    /// Secret key name.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// Secret value.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Create a new secret input.
    /// </summary>
    public static SecretInput Create(string key, string value, string? description = null)
        => new() { Key = key, Value = value, Description = description };
}

/// <summary>
/// Options for bulk import operations.
/// </summary>
public record BulkImportOptions
{
    /// <summary>
    /// Whether to overwrite existing secrets.
    /// </summary>
    [JsonPropertyName("overwrite")]
    public bool Overwrite { get; init; }
}

/// <summary>
/// Result of a bulk import operation.
/// </summary>
public record BulkImportResult
{
    /// <summary>
    /// Number of secrets created.
    /// </summary>
    [JsonPropertyName("created")]
    public int Created { get; init; }

    /// <summary>
    /// Number of secrets updated.
    /// </summary>
    [JsonPropertyName("updated")]
    public int Updated { get; init; }

    /// <summary>
    /// Number of secrets skipped.
    /// </summary>
    [JsonPropertyName("skipped")]
    public int Skipped { get; init; }
}

/// <summary>
/// Historical version of a secret.
/// </summary>
public record SecretHistory
{
    /// <summary>
    /// History entry ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Secret ID.
    /// </summary>
    [JsonPropertyName("secret_id")]
    public required string SecretId { get; init; }

    /// <summary>
    /// Secret key.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// Version number.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; init; }

    /// <summary>
    /// User ID who made the change.
    /// </summary>
    [JsonPropertyName("changed_by")]
    public string? ChangedBy { get; init; }

    /// <summary>
    /// Type of change.
    /// </summary>
    [JsonPropertyName("change_type")]
    public required string ChangeType { get; init; }

    /// <summary>
    /// When the change was made.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}
