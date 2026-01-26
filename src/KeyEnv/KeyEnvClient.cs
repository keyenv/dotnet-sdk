using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyEnv.Types;

namespace KeyEnv;

/// <summary>
/// Configuration options for the KeyEnv client.
/// </summary>
public record KeyEnvOptions
{
    /// <summary>
    /// Service token for authentication (required).
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// API base URL (optional, defaults to https://api.keyenv.dev).
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// HTTP request timeout (optional, defaults to 30 seconds).
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Cache TTL for secrets (optional, 0 or null disables caching).
    /// </summary>
    public TimeSpan? CacheTtl { get; init; }
}

/// <summary>
/// KeyEnv API client for managing secrets.
/// </summary>
/// <example>
/// <code>
/// var client = KeyEnvClient.Create("your-token");
/// var secrets = await client.GetSecretsAsync("project-id", "production");
/// </code>
/// </example>
public sealed class KeyEnvClient : IDisposable
{
    /// <summary>
    /// Default API base URL.
    /// </summary>
    public const string DefaultBaseUrl = "https://api.keyenv.dev";

    /// <summary>
    /// Default request timeout.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// SDK version.
    /// </summary>
    public const string Version = "1.0.0";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly TimeSpan _cacheTtl;
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly object _cacheLock = new();
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private record CacheEntry(string Data, DateTime ExpiresAt);

    private KeyEnvClient(KeyEnvOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw KeyEnvException.Config("Token is required");
        }

        _baseUrl = (options.BaseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _cacheTtl = options.CacheTtl ?? TimeSpan.Zero;

        var timeout = options.Timeout ?? DefaultTimeout;
        _httpClient = new HttpClient { Timeout = timeout };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"keyenv-dotnet/{Version}");
    }

    /// <summary>
    /// Creates a new KeyEnv client with the specified token.
    /// </summary>
    /// <param name="token">Service token for authentication.</param>
    /// <returns>A new KeyEnvClient instance.</returns>
    public static KeyEnvClient Create(string token)
        => new(new KeyEnvOptions { Token = token });

    /// <summary>
    /// Creates a new KeyEnv client with the specified options.
    /// </summary>
    /// <param name="options">Client configuration options.</param>
    /// <returns>A new KeyEnvClient instance.</returns>
    public static KeyEnvClient Create(KeyEnvOptions options)
        => new(options);

    #region HTTP Methods

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        return await DeserializeAsync<T>(response, cancellationToken);
    }

    private async Task<T> PostAsync<T>(string path, object? body, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, path, body, cancellationToken);
        return await DeserializeAsync<T>(response, cancellationToken);
    }

    private async Task PostAsync(string path, object? body, CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Post, path, body, cancellationToken);
    }

    private async Task<T> PutAsync<T>(string path, object? body, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, path, body, cancellationToken);
        return await DeserializeAsync<T>(response, cancellationToken);
    }

    private async Task PutAsync(string path, object? body, CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Put, path, body, cancellationToken);
    }

    private async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Delete, path, null, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var url = $"{_baseUrl}{path}";

        using var request = new HttpRequestMessage(method, url);
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw KeyEnvException.Timeout();
        }
        catch (HttpRequestException ex)
        {
            throw new KeyEnvException($"Request failed: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponse(response, cancellationToken);
        }

        return response;
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new KeyEnvException("Failed to deserialize response");
    }

    private static async Task HandleErrorResponse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        string message;
        string? code = null;

        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            message = root.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString() ?? response.ReasonPhrase ?? "Unknown error"
                : root.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString() ?? response.ReasonPhrase ?? "Unknown error"
                    : response.ReasonPhrase ?? "Unknown error";

            if (root.TryGetProperty("code", out var codeProp))
            {
                code = codeProp.GetString();
            }
        }
        catch
        {
            message = response.ReasonPhrase ?? "Unknown error";
        }

        throw KeyEnvException.Api(statusCode, message, code);
    }

    #endregion

    #region Cache

    private T? GetCached<T>(string key)
    {
        if (_cacheTtl == TimeSpan.Zero) return default;

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.ExpiresAt)
            {
                return JsonSerializer.Deserialize<T>(entry.Data, JsonOptions);
            }
        }
        return default;
    }

    private void SetCache<T>(string key, T data)
    {
        if (_cacheTtl == TimeSpan.Zero) return;

        var json = JsonSerializer.Serialize(data, JsonOptions);
        lock (_cacheLock)
        {
            _cache[key] = new CacheEntry(json, DateTime.UtcNow.Add(_cacheTtl));
        }
    }

    /// <summary>
    /// Clears the cache for a specific project and environment.
    /// </summary>
    public void ClearCache(string projectId, string environment)
    {
        var prefix = $"secrets:{projectId}:{environment}";
        lock (_cacheLock)
        {
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void ClearAllCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
    }

    #endregion

    #region User & Token

    /// <summary>
    /// Gets information about the current authenticated user or service token.
    /// </summary>
    public async Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        => await GetAsync<CurrentUserResponse>("/api/v1/users/me", cancellationToken);

    /// <summary>
    /// Validates the token and returns user information.
    /// </summary>
    public Task<CurrentUserResponse> ValidateTokenAsync(CancellationToken cancellationToken = default)
        => GetCurrentUserAsync(cancellationToken);

    #endregion

    #region Projects

    /// <summary>
    /// Lists all projects accessible to the current user or service token.
    /// </summary>
    public async Task<IReadOnlyList<Project>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ProjectsResponse>("/api/v1/projects", cancellationToken);
        return response.Projects;
    }

    /// <summary>
    /// Gets a project by ID including its environments.
    /// </summary>
    public async Task<Project> GetProjectAsync(string projectId, CancellationToken cancellationToken = default)
        => await GetAsync<Project>($"/api/v1/projects/{projectId}", cancellationToken);

    /// <summary>
    /// Creates a new project.
    /// </summary>
    public async Task<Project> CreateProjectAsync(string teamId, string name, CancellationToken cancellationToken = default)
        => await PostAsync<Project>("/api/v1/projects", new { team_id = teamId, name }, cancellationToken);

    /// <summary>
    /// Deletes a project.
    /// </summary>
    public async Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default)
        => await DeleteAsync($"/api/v1/projects/{projectId}", cancellationToken);

    #endregion

    #region Environments

    /// <summary>
    /// Lists all environments in a project.
    /// </summary>
    public async Task<IReadOnlyList<Types.Environment>> ListEnvironmentsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<EnvironmentsResponse>($"/api/v1/projects/{projectId}/environments", cancellationToken);
        return response.Environments;
    }

    /// <summary>
    /// Creates a new environment in a project.
    /// </summary>
    public async Task<Types.Environment> CreateEnvironmentAsync(
        string projectId,
        string name,
        string? inheritsFrom = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (inheritsFrom != null) body["inherits_from"] = inheritsFrom;

        return await PostAsync<Types.Environment>($"/api/v1/projects/{projectId}/environments", body, cancellationToken);
    }

    /// <summary>
    /// Deletes an environment from a project.
    /// </summary>
    public async Task DeleteEnvironmentAsync(string projectId, string environment, CancellationToken cancellationToken = default)
        => await DeleteAsync($"/api/v1/projects/{projectId}/environments/{environment}", cancellationToken);

    #endregion

    #region Secrets

    /// <summary>
    /// Lists secrets (without values) in an environment.
    /// </summary>
    public async Task<IReadOnlyList<SecretWithInheritance>> ListSecretsAsync(
        string projectId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<SecretsResponse>($"/api/v1/projects/{projectId}/environments/{environment}/secrets", cancellationToken);
        return response.Secrets;
    }

    /// <summary>
    /// Gets all secrets with their decrypted values for an environment.
    /// Results are cached when CacheTtl is configured.
    /// </summary>
    public async Task<IReadOnlyList<SecretWithValueAndInheritance>> GetSecretsAsync(
        string projectId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"secrets:{projectId}:{environment}:export";

        var cached = GetCached<List<SecretWithValueAndInheritance>>(cacheKey);
        if (cached != null) return cached;

        var response = await GetAsync<SecretsExportResponse>($"/api/v1/projects/{projectId}/environments/{environment}/secrets/export", cancellationToken);
        SetCache(cacheKey, response.Secrets);
        return response.Secrets;
    }

    /// <summary>
    /// Gets secrets as a dictionary of key-value pairs.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsDictionaryAsync(
        string projectId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var secrets = await GetSecretsAsync(projectId, environment, cancellationToken);
        return secrets.ToDictionary(s => s.Key, s => s.Value);
    }

    /// <summary>
    /// Gets a single secret by key.
    /// </summary>
    public async Task<SecretWithValue> GetSecretAsync(
        string projectId,
        string environment,
        string key,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<SecretResponse>($"/api/v1/projects/{projectId}/environments/{environment}/secrets/{key}", cancellationToken);
        return response.Secret;
    }

    /// <summary>
    /// Sets (creates or updates) a secret.
    /// </summary>
    public async Task SetSecretAsync(
        string projectId,
        string environment,
        string key,
        string value,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/projects/{projectId}/environments/{environment}/secrets/{key}";
        var body = new Dictionary<string, object?> { ["value"] = value };
        if (description != null) body["description"] = description;

        try
        {
            await PutAsync(path, body, cancellationToken);
        }
        catch (KeyEnvException ex) when (ex.IsNotFound)
        {
            // Secret doesn't exist, create it
            var createBody = new Dictionary<string, object?> { ["key"] = key, ["value"] = value };
            if (description != null) createBody["description"] = description;

            await PostAsync($"/api/v1/projects/{projectId}/environments/{environment}/secrets", createBody, cancellationToken);
        }

        ClearCache(projectId, environment);
    }

    /// <summary>
    /// Deletes a secret by key.
    /// </summary>
    public async Task DeleteSecretAsync(
        string projectId,
        string environment,
        string key,
        CancellationToken cancellationToken = default)
    {
        await DeleteAsync($"/api/v1/projects/{projectId}/environments/{environment}/secrets/{key}", cancellationToken);
        ClearCache(projectId, environment);
    }

    /// <summary>
    /// Imports multiple secrets at once.
    /// </summary>
    public async Task<BulkImportResult> BulkImportAsync(
        string projectId,
        string environment,
        IEnumerable<SecretInput> secrets,
        BulkImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            secrets = secrets.ToList(),
            overwrite = options?.Overwrite ?? false
        };

        var result = await PostAsync<BulkImportResult>($"/api/v1/projects/{projectId}/environments/{environment}/secrets/bulk", body, cancellationToken);
        ClearCache(projectId, environment);
        return result;
    }

    /// <summary>
    /// Loads secrets into environment variables.
    /// </summary>
    /// <returns>The number of secrets loaded.</returns>
    public async Task<int> LoadEnvAsync(
        string projectId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var secrets = await GetSecretsAsync(projectId, environment, cancellationToken);
        foreach (var secret in secrets)
        {
            System.Environment.SetEnvironmentVariable(secret.Key, secret.Value);
        }
        return secrets.Count;
    }

    /// <summary>
    /// Generates .env file content from secrets.
    /// </summary>
    public async Task<string> GenerateEnvFileAsync(
        string projectId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var secrets = await GetSecretsAsync(projectId, environment, cancellationToken);
        var builder = new StringBuilder();

        foreach (var secret in secrets)
        {
            var value = secret.Value;
            var needsQuotes = value.Contains(' ') || value.Contains('\t') ||
                             value.Contains('\n') || value.Contains('"') ||
                             value.Contains('\'') || value.Contains('\\') ||
                             value.Contains('$');

            if (needsQuotes)
            {
                var escaped = value
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("$", "\\$");
                builder.AppendLine($"{secret.Key}=\"{escaped}\"");
            }
            else
            {
                builder.AppendLine($"{secret.Key}={value}");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the version history of a secret.
    /// </summary>
    public async Task<IReadOnlyList<SecretHistory>> GetSecretHistoryAsync(
        string projectId,
        string environment,
        string key,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<HistoryResponse>($"/api/v1/projects/{projectId}/environments/{environment}/secrets/{key}/history", cancellationToken);
        return response.History;
    }

    #endregion

    #region Permissions

    /// <summary>
    /// Lists permissions for an environment.
    /// </summary>
    public async Task<IReadOnlyList<Permission>> ListPermissionsAsync(
        string projectId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<PermissionsResponse>($"/api/v1/projects/{projectId}/environments/{environment}/permissions", cancellationToken);
        return response.Permissions;
    }

    /// <summary>
    /// Sets a user's permission for an environment.
    /// </summary>
    public async Task SetPermissionAsync(
        string projectId,
        string environment,
        string userId,
        string role,
        CancellationToken cancellationToken = default)
        => await PutAsync($"/api/v1/projects/{projectId}/environments/{environment}/permissions/{userId}", new { role }, cancellationToken);

    /// <summary>
    /// Deletes a user's permission for an environment.
    /// </summary>
    public async Task DeletePermissionAsync(
        string projectId,
        string environment,
        string userId,
        CancellationToken cancellationToken = default)
        => await DeleteAsync($"/api/v1/projects/{projectId}/environments/{environment}/permissions/{userId}", cancellationToken);

    /// <summary>
    /// Gets the current user's permissions for a project.
    /// </summary>
    public async Task<MyPermissionsResponse> GetMyPermissionsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
        => await GetAsync<MyPermissionsResponse>($"/api/v1/projects/{projectId}/my-permissions", cancellationToken);

    /// <summary>
    /// Gets default permissions for a project.
    /// </summary>
    public async Task<IReadOnlyList<DefaultPermission>> GetProjectDefaultsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<DefaultsResponse>($"/api/v1/projects/{projectId}/permissions/defaults", cancellationToken);
        return response.Defaults;
    }

    /// <summary>
    /// Sets default permissions for a project.
    /// </summary>
    public async Task SetProjectDefaultsAsync(
        string projectId,
        IEnumerable<DefaultPermission> defaults,
        CancellationToken cancellationToken = default)
        => await PutAsync($"/api/v1/projects/{projectId}/permissions/defaults", new { defaults = defaults.ToList() }, cancellationToken);

    #endregion

    #region Response Types

    private record ProjectsResponse(
        [property: JsonPropertyName("projects")] List<Project> Projects);

    private record EnvironmentsResponse(
        [property: JsonPropertyName("environments")] List<Types.Environment> Environments);

    private record SecretsResponse(
        [property: JsonPropertyName("secrets")] List<SecretWithInheritance> Secrets);

    private record SecretResponse(
        [property: JsonPropertyName("secret")] SecretWithValue Secret);

    private record SecretsExportResponse(
        [property: JsonPropertyName("secrets")] List<SecretWithValueAndInheritance> Secrets);

    private record PermissionsResponse(
        [property: JsonPropertyName("permissions")] List<Permission> Permissions);

    private record DefaultsResponse(
        [property: JsonPropertyName("defaults")] List<DefaultPermission> Defaults);

    private record HistoryResponse(
        [property: JsonPropertyName("history")] List<SecretHistory> History);

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
