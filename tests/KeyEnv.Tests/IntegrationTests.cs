using System;
using System.Linq;
using System.Threading.Tasks;
using KeyEnv.Types;
using Xunit;

namespace KeyEnv.Tests;

/// <summary>
/// Integration tests that run against the live KeyEnv test API.
///
/// Required environment variables:
/// - KEYENV_API_URL: API base URL (e.g., http://localhost:8081/api/v1)
/// - KEYENV_TOKEN: Service token for authentication
/// - KEYENV_PROJECT: Project slug (optional, defaults to "sdk-test")
///
/// To run these tests:
/// 1. Start the test infrastructure: make test-infra-up
/// 2. Set environment variables:
///    export KEYENV_API_URL=http://localhost:8081/api/v1
///    export KEYENV_TOKEN=env_test_integration_token_12345
///    export KEYENV_PROJECT=sdk-test
/// 3. Run: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class IntegrationTests : IAsyncLifetime
{
    private readonly KeyEnvClient? _client;
    private readonly string _projectSlug;
    private readonly string _environment = "development";
    private readonly bool _shouldSkip;
    private readonly List<string> _createdSecretKeys = new();
    private readonly string _testKeyPrefix;

    public IntegrationTests()
    {
        var apiUrl = Environment.GetEnvironmentVariable("KEYENV_API_URL");
        var token = Environment.GetEnvironmentVariable("KEYENV_TOKEN");
        _projectSlug = Environment.GetEnvironmentVariable("KEYENV_PROJECT") ?? "sdk-test";

        _shouldSkip = string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(token);

        if (!_shouldSkip)
        {
            // Remove /api/v1 suffix if present since the client adds it
            var baseUrl = apiUrl!;
            if (baseUrl.EndsWith("/api/v1"))
            {
                baseUrl = baseUrl[..^7];
            }
            else if (baseUrl.EndsWith("/api/v1/"))
            {
                baseUrl = baseUrl[..^8];
            }

            _client = KeyEnvClient.Create(new KeyEnvOptions
            {
                Token = token!,
                BaseUrl = baseUrl
            });
        }

        // Use timestamp to create unique test keys
        _testKeyPrefix = $"DOTNET_SDK_TEST_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up all created secrets
        if (_client != null && !_shouldSkip)
        {
            foreach (var key in _createdSecretKeys)
            {
                try
                {
                    await _client.DeleteSecretAsync(_projectSlug, _environment, key);
                }
                catch
                {
                    // Ignore cleanup errors - secret may already be deleted
                }
            }
            _client.Dispose();
        }
    }

    private string CreateTestKey(string suffix)
    {
        var key = $"{_testKeyPrefix}_{suffix}";
        _createdSecretKeys.Add(key);
        return key;
    }

    private void SkipIfNotConfigured()
    {
        Skip.If(_shouldSkip, "KEYENV_API_URL and KEYENV_TOKEN environment variables must be set");
    }

    #region Projects

    [SkippableFact]
    public async Task ListProjects_ReturnsProjects()
    {
        SkipIfNotConfigured();

        var projects = await _client!.ListProjectsAsync();

        Assert.NotNull(projects);
        Assert.NotEmpty(projects);
        Assert.Contains(projects, p => p.Name == _projectSlug || p.Id == _projectSlug);
    }

    [SkippableFact]
    public async Task GetProject_ReturnsProjectDetails()
    {
        SkipIfNotConfigured();

        var project = await _client!.GetProjectAsync(_projectSlug);

        Assert.NotNull(project);
        Assert.NotNull(project.Id);
        Assert.NotNull(project.Name);
        Assert.NotNull(project.TeamId);
    }

    [SkippableFact]
    public async Task GetProject_WithInvalidSlug_ThrowsNotFound()
    {
        SkipIfNotConfigured();

        var ex = await Assert.ThrowsAsync<KeyEnvException>(
            () => _client!.GetProjectAsync("nonexistent-project-12345"));

        Assert.True(ex.IsNotFound || ex.IsForbidden);
    }

    #endregion

    #region Environments

    [SkippableFact]
    public async Task ListEnvironments_ReturnsEnvironments()
    {
        SkipIfNotConfigured();

        var environments = await _client!.ListEnvironmentsAsync(_projectSlug);

        Assert.NotNull(environments);
        Assert.NotEmpty(environments);

        // Should have the standard environments
        var envNames = environments.Select(e => e.Name).ToList();
        Assert.Contains("development", envNames);
    }

    [SkippableFact]
    public async Task ListEnvironments_ContainsExpectedEnvironments()
    {
        SkipIfNotConfigured();

        var environments = await _client!.ListEnvironmentsAsync(_projectSlug);
        var envNames = environments.Select(e => e.Name).ToHashSet();

        // The test project should have standard environments
        Assert.True(
            envNames.Contains("development") ||
            envNames.Contains("staging") ||
            envNames.Contains("production"),
            $"Expected at least one standard environment, but found: {string.Join(", ", envNames)}");
    }

    #endregion

    #region Secrets Export

    [SkippableFact]
    public async Task GetSecrets_ReturnsSecretsList()
    {
        SkipIfNotConfigured();

        var secrets = await _client!.GetSecretsAsync(_projectSlug, _environment);

        Assert.NotNull(secrets);
        // The list may be empty if no secrets exist yet, which is fine
    }

    [SkippableFact]
    public async Task GetSecretsAsDictionary_ReturnsDictionary()
    {
        SkipIfNotConfigured();

        // First create a secret so we have something to retrieve
        var testKey = CreateTestKey("DICT");
        var testValue = "dict-test-value";
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, testValue);

        // Clear cache to ensure fresh data
        _client.ClearCache(_projectSlug, _environment);

        var secrets = await _client.GetSecretsAsDictionaryAsync(_projectSlug, _environment);

        Assert.NotNull(secrets);
        Assert.True(secrets.ContainsKey(testKey));
        Assert.Equal(testValue, secrets[testKey]);
    }

    #endregion

    #region Secret CRUD Operations

    [SkippableFact]
    public async Task CreateSecret_CreatesNewSecret()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("CREATE");
        var testValue = $"test-value-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var description = "Test secret created by .NET SDK integration tests";

        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, testValue, description);

        // Verify the secret was created
        var retrieved = await _client.GetSecretAsync(_projectSlug, _environment, testKey);

        Assert.NotNull(retrieved);
        Assert.Equal(testKey, retrieved.Key);
        Assert.Equal(testValue, retrieved.Value);
    }

    [SkippableFact]
    public async Task GetSecret_ReturnsSecretWithValue()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("GET");
        var testValue = "get-test-value";
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, testValue);

        var secret = await _client.GetSecretAsync(_projectSlug, _environment, testKey);

        Assert.NotNull(secret);
        Assert.Equal(testKey, secret.Key);
        Assert.Equal(testValue, secret.Value);
        Assert.NotNull(secret.Id);
    }

    [SkippableFact]
    public async Task GetSecret_WithInvalidKey_ThrowsNotFound()
    {
        SkipIfNotConfigured();

        var ex = await Assert.ThrowsAsync<KeyEnvException>(
            () => _client!.GetSecretAsync(_projectSlug, _environment, "NONEXISTENT_KEY_12345"));

        Assert.True(ex.IsNotFound);
    }

    [SkippableFact]
    public async Task UpdateSecret_UpdatesExistingSecret()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("UPDATE");
        var initialValue = "initial-value";
        var updatedValue = "updated-value";

        // Create initial secret
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, initialValue);

        // Update the secret
        await _client.SetSecretAsync(_projectSlug, _environment, testKey, updatedValue);

        // Clear cache and verify update
        _client.ClearCache(_projectSlug, _environment);
        var retrieved = await _client.GetSecretAsync(_projectSlug, _environment, testKey);

        Assert.Equal(updatedValue, retrieved.Value);
    }

    [SkippableFact]
    public async Task DeleteSecret_RemovesSecret()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("DELETE");
        var testValue = "delete-test-value";

        // Create and then delete
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, testValue);
        await _client.DeleteSecretAsync(_projectSlug, _environment, testKey);

        // Remove from cleanup list since we already deleted it
        _createdSecretKeys.Remove(testKey);

        // Verify deletion
        _client.ClearCache(_projectSlug, _environment);
        var ex = await Assert.ThrowsAsync<KeyEnvException>(
            () => _client.GetSecretAsync(_projectSlug, _environment, testKey));

        Assert.True(ex.IsNotFound);
    }

    [SkippableFact]
    public async Task SecretCrudOperations_FullLifecycle()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("LIFECYCLE");
        var createValue = $"create-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var updateValue = $"update-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // Create
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, createValue);
        var created = await _client.GetSecretAsync(_projectSlug, _environment, testKey);
        Assert.Equal(createValue, created.Value);

        // Read via list
        _client.ClearCache(_projectSlug, _environment);
        var secrets = await _client.GetSecretsAsync(_projectSlug, _environment);
        Assert.Contains(secrets, s => s.Key == testKey && s.Value == createValue);

        // Update
        await _client.SetSecretAsync(_projectSlug, _environment, testKey, updateValue);
        _client.ClearCache(_projectSlug, _environment);
        var updated = await _client.GetSecretAsync(_projectSlug, _environment, testKey);
        Assert.Equal(updateValue, updated.Value);

        // Delete
        await _client.DeleteSecretAsync(_projectSlug, _environment, testKey);
        _createdSecretKeys.Remove(testKey);

        // Verify deleted
        _client.ClearCache(_projectSlug, _environment);
        await Assert.ThrowsAsync<KeyEnvException>(
            () => _client.GetSecretAsync(_projectSlug, _environment, testKey));
    }

    #endregion

    #region Bulk Operations

    [SkippableFact]
    public async Task BulkImport_CreatesMultipleSecrets()
    {
        SkipIfNotConfigured();

        var key1 = CreateTestKey("BULK1");
        var key2 = CreateTestKey("BULK2");
        var key3 = CreateTestKey("BULK3");

        var secrets = new[]
        {
            SecretInput.Create(key1, "bulk-value-1"),
            SecretInput.Create(key2, "bulk-value-2"),
            SecretInput.Create(key3, "bulk-value-3", "Bulk import test")
        };

        var result = await _client!.BulkImportAsync(_projectSlug, _environment, secrets);

        Assert.NotNull(result);
        Assert.True(result.Created >= 0);
        Assert.True(result.Updated >= 0);

        // Verify secrets were created
        _client.ClearCache(_projectSlug, _environment);
        var allSecrets = await _client.GetSecretsAsDictionaryAsync(_projectSlug, _environment);

        Assert.True(allSecrets.ContainsKey(key1));
        Assert.True(allSecrets.ContainsKey(key2));
        Assert.True(allSecrets.ContainsKey(key3));
    }

    [SkippableFact]
    public async Task BulkImport_WithOverwrite_UpdatesExisting()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("BULKOVERWRITE");

        // Create initial secret
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, "original-value");

        // Bulk import with overwrite
        var secrets = new[] { SecretInput.Create(testKey, "overwritten-value") };
        var result = await _client.BulkImportAsync(
            _projectSlug,
            _environment,
            secrets,
            new BulkImportOptions { Overwrite = true });

        Assert.NotNull(result);

        // Verify overwrite
        _client.ClearCache(_projectSlug, _environment);
        var retrieved = await _client.GetSecretAsync(_projectSlug, _environment, testKey);
        Assert.Equal("overwritten-value", retrieved.Value);
    }

    #endregion

    #region Generate Env File

    [SkippableFact]
    public async Task GenerateEnvFile_ReturnsValidEnvFormat()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("ENVFILE");
        var testValue = "env-file-test-value";
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, testValue);

        _client.ClearCache(_projectSlug, _environment);
        var envContent = await _client.GenerateEnvFileAsync(_projectSlug, _environment);

        Assert.NotNull(envContent);
        Assert.Contains($"{testKey}={testValue}", envContent);
    }

    [SkippableFact]
    public async Task GenerateEnvFile_QuotesSpecialCharacters()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("ENVSPECIAL");
        var testValue = "value with spaces and $pecial chars";
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, testValue);

        _client.ClearCache(_projectSlug, _environment);
        var envContent = await _client.GenerateEnvFileAsync(_projectSlug, _environment);

        Assert.NotNull(envContent);
        // Should be quoted due to special characters
        Assert.Contains($"{testKey}=", envContent);
        Assert.Contains("\"", envContent.Split('\n').First(l => l.StartsWith(testKey)));
    }

    #endregion

    #region Secret History

    [SkippableFact]
    public async Task GetSecretHistory_ReturnsVersionHistory()
    {
        SkipIfNotConfigured();

        var testKey = CreateTestKey("HISTORY");

        // Create and update to generate history
        await _client!.SetSecretAsync(_projectSlug, _environment, testKey, "version-1");
        await _client.SetSecretAsync(_projectSlug, _environment, testKey, "version-2");

        var history = await _client.GetSecretHistoryAsync(_projectSlug, _environment, testKey);

        Assert.NotNull(history);
        Assert.True(history.Count >= 1, "Expected at least one history entry");
    }

    #endregion

    #region Token Validation

    [SkippableFact]
    public async Task ValidateToken_ReturnsCurrentUser()
    {
        SkipIfNotConfigured();

        var response = await _client!.ValidateTokenAsync();

        Assert.NotNull(response);
        Assert.NotNull(response.Type);
        // Should be a service token for integration tests
        Assert.Equal("service_token", response.Type);
        Assert.NotNull(response.ServiceToken);
    }

    [SkippableFact]
    public async Task GetCurrentUser_ReturnsServiceTokenInfo()
    {
        SkipIfNotConfigured();

        var response = await _client!.GetCurrentUserAsync();

        Assert.NotNull(response);
        Assert.Equal("service_token", response.Type);
        Assert.NotNull(response.ServiceToken);
        Assert.NotNull(response.ServiceToken.Id);
        Assert.NotNull(response.ServiceToken.ProjectId);
    }

    #endregion

    #region Error Handling

    [SkippableFact]
    public async Task InvalidToken_ThrowsUnauthorized()
    {
        SkipIfNotConfigured();

        var apiUrl = Environment.GetEnvironmentVariable("KEYENV_API_URL")!;
        var baseUrl = apiUrl;
        if (baseUrl.EndsWith("/api/v1"))
        {
            baseUrl = baseUrl[..^7];
        }

        using var invalidClient = KeyEnvClient.Create(new KeyEnvOptions
        {
            Token = "invalid_token_12345",
            BaseUrl = baseUrl
        });

        var ex = await Assert.ThrowsAsync<KeyEnvException>(
            () => invalidClient.ListProjectsAsync());

        Assert.True(ex.IsUnauthorized || ex.IsForbidden);
    }

    #endregion

    #region Multiple Environments

    [SkippableFact]
    public async Task SecretsInDifferentEnvironments_AreIsolated()
    {
        SkipIfNotConfigured();

        // Check if staging environment exists
        var environments = await _client!.ListEnvironmentsAsync(_projectSlug);
        var hasStaging = environments.Any(e => e.Name == "staging");

        Skip.IfNot(hasStaging, "Staging environment not available");

        var testKey = CreateTestKey("ENVISO");
        var devValue = "development-value";
        var stagingValue = "staging-value";

        // Create secret in development
        await _client.SetSecretAsync(_projectSlug, "development", testKey, devValue);

        // Create same key with different value in staging
        await _client.SetSecretAsync(_projectSlug, "staging", testKey, stagingValue);
        _createdSecretKeys.Add(testKey); // Track for cleanup in staging too

        // Verify they're isolated
        _client.ClearCache(_projectSlug, "development");
        _client.ClearCache(_projectSlug, "staging");

        var devSecret = await _client.GetSecretAsync(_projectSlug, "development", testKey);
        var stagingSecret = await _client.GetSecretAsync(_projectSlug, "staging", testKey);

        Assert.Equal(devValue, devSecret.Value);
        Assert.Equal(stagingValue, stagingSecret.Value);

        // Clean up staging secret
        try
        {
            await _client.DeleteSecretAsync(_projectSlug, "staging", testKey);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
