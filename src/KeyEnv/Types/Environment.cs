using System.Text.Json.Serialization;

namespace KeyEnv.Types;

/// <summary>
/// Represents an environment within a project.
/// </summary>
public record Environment
{
    /// <summary>
    /// Environment ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Environment name (e.g., "development", "production").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Environment description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Project ID this environment belongs to.
    /// </summary>
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    /// <summary>
    /// ID of environment this one inherits from.
    /// </summary>
    [JsonPropertyName("inherits_from_id")]
    public string? InheritsFromId { get; init; }

    /// <summary>
    /// Display order.
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; init; }

    /// <summary>
    /// When the environment was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the environment was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Represents a KeyEnv project.
/// </summary>
public record Project
{
    /// <summary>
    /// Project ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Project name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Project slug (URL-friendly name).
    /// </summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    /// <summary>
    /// Project description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Team ID that owns this project.
    /// </summary>
    [JsonPropertyName("team_id")]
    public required string TeamId { get; init; }

    /// <summary>
    /// When the project was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the project was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Environments in this project.
    /// </summary>
    [JsonPropertyName("environments")]
    public IReadOnlyList<Environment>? Environments { get; init; }
}

/// <summary>
/// Represents a KeyEnv user.
/// </summary>
public record User
{
    /// <summary>
    /// User ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// User email address.
    /// </summary>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>
    /// User's first name.
    /// </summary>
    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    /// <summary>
    /// User's last name.
    /// </summary>
    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    /// <summary>
    /// URL to user's avatar image.
    /// </summary>
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// When the user was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Represents a team.
/// </summary>
public record Team
{
    /// <summary>
    /// Team ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Team name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// When the team was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the team was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Response containing current user or service token information.
/// This is returned by GET /users/me and varies based on authentication type.
/// For service tokens: returns id, team_id, project_ids, scopes, auth_type
/// For users: returns full user object with teams
/// </summary>
public record CurrentUserResponse
{
    /// <summary>
    /// Token/User ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Type of authentication ("service_token" for tokens, null/missing for users).
    /// </summary>
    [JsonPropertyName("auth_type")]
    public string? Type { get; init; }

    /// <summary>
    /// Team ID (for service tokens).
    /// </summary>
    [JsonPropertyName("team_id")]
    public string? TeamId { get; init; }

    /// <summary>
    /// Project IDs this token has access to (for service tokens).
    /// </summary>
    [JsonPropertyName("project_ids")]
    public IReadOnlyList<string>? ProjectIds { get; init; }

    /// <summary>
    /// Scopes granted to this token (for service tokens).
    /// </summary>
    [JsonPropertyName("scopes")]
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// User email (for user auth).
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// User's first name (for user auth).
    /// </summary>
    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    /// <summary>
    /// User's last name (for user auth).
    /// </summary>
    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    /// <summary>
    /// Clerk ID (for user auth).
    /// </summary>
    [JsonPropertyName("clerk_id")]
    public string? ClerkId { get; init; }

    /// <summary>
    /// Teams the user belongs to (for user auth).
    /// </summary>
    [JsonPropertyName("teams")]
    public IReadOnlyList<Team>? Teams { get; init; }

    /// <summary>
    /// When the user/token was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// Helper to check if this is a service token response.
    /// </summary>
    [JsonIgnore]
    public bool IsServiceToken => Type == "service_token";

    /// <summary>
    /// Helper property for backwards compatibility - returns service token info.
    /// </summary>
    [JsonIgnore]
    public ServiceTokenInfo? ServiceToken => IsServiceToken ? new ServiceTokenInfo
    {
        Id = Id ?? string.Empty,
        TeamId = TeamId ?? string.Empty,
        ProjectIds = ProjectIds ?? Array.Empty<string>(),
        Scopes = Scopes ?? Array.Empty<string>()
    } : null;
}

/// <summary>
/// Service token information extracted from CurrentUserResponse.
/// </summary>
public record ServiceTokenInfo
{
    /// <summary>
    /// Token ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Team ID this token belongs to.
    /// </summary>
    public required string TeamId { get; init; }

    /// <summary>
    /// Project IDs this token has access to.
    /// </summary>
    public required IReadOnlyList<string> ProjectIds { get; init; }

    /// <summary>
    /// Scopes granted to this token.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }
}

/// <summary>
/// A permission for an environment.
/// </summary>
public record Permission
{
    /// <summary>
    /// Permission ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// User ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    /// <summary>
    /// User email.
    /// </summary>
    [JsonPropertyName("user_email")]
    public required string UserEmail { get; init; }

    /// <summary>
    /// Environment ID.
    /// </summary>
    [JsonPropertyName("environment_id")]
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Environment name.
    /// </summary>
    [JsonPropertyName("environment_name")]
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// Role (e.g., "read", "write", "admin").
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>
    /// Whether the user can write to this environment.
    /// </summary>
    [JsonPropertyName("can_write")]
    public bool CanWrite { get; init; }

    /// <summary>
    /// When the permission was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the permission was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Response containing the current user's permissions.
/// </summary>
public record MyPermissionsResponse
{
    /// <summary>
    /// List of permissions.
    /// </summary>
    [JsonPropertyName("permissions")]
    public required IReadOnlyList<Permission> Permissions { get; init; }

    /// <summary>
    /// Whether the user is a team admin.
    /// </summary>
    [JsonPropertyName("is_team_admin")]
    public bool IsTeamAdmin { get; init; }
}

/// <summary>
/// Input for setting a user's permission in a bulk operation.
/// </summary>
public record BulkPermissionInput
{
    /// <summary>
    /// User ID to set permission for.
    /// </summary>
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    /// <summary>
    /// Role to assign ("none", "read", "write", or "admin").
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>
    /// Create a new bulk permission input.
    /// </summary>
    public static BulkPermissionInput Create(string userId, string role)
        => new() { UserId = userId, Role = role };
}

/// <summary>
/// Default permission settings for an environment.
/// </summary>
public record DefaultPermission
{
    /// <summary>
    /// Environment name.
    /// </summary>
    [JsonPropertyName("environment_name")]
    public required string EnvironmentName { get; init; }

    /// <summary>
    /// Default role for new team members.
    /// </summary>
    [JsonPropertyName("default_role")]
    public required string DefaultRole { get; init; }
}
