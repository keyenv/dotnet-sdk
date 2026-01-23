using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace KeyEnv.Tests;

/// <summary>
/// Integration tests that run against the live KeyEnv API.
/// Requires KEYENV_SERVICE_TOKEN and KEYENV_PROJECT_ID environment variables.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class IntegrationTests : IAsyncLifetime
{
    private readonly KeyEnvClient? _client;
    private readonly string? _projectId;
    private readonly string _environment = "development";
    private readonly string _testSecretKey;
    private bool _shouldSkip;

    public IntegrationTests()
    {
        var token = Environment.GetEnvironmentVariable("KEYENV_SERVICE_TOKEN");
        _projectId = Environment.GetEnvironmentVariable("KEYENV_PROJECT_ID");

        _shouldSkip = string.IsNullOrEmpty(token) || string.IsNullOrEmpty(_projectId);

        if (!_shouldSkip)
        {
            _client = KeyEnvClient.Create(token!);
        }

        _testSecretKey = $"TEST_INTEGRATION_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test secret
        if (_client != null && !_shouldSkip)
        {
            try
            {
                await _client.DeleteSecretAsync(_projectId!, _environment, _testSecretKey);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void SkipIfNotConfigured()
    {
        Skip.If(_shouldSkip, "KEYENV_SERVICE_TOKEN and KEYENV_PROJECT_ID must be set");
    }

    [SkippableFact]
    public async Task ListProjects_ReturnsProjects()
    {
        SkipIfNotConfigured();

        var projects = await _client!.ListProjectsAsync();

        Assert.NotNull(projects);
        Assert.NotEmpty(projects);
    }

    [SkippableFact]
    public async Task GetProject_ReturnsProjectDetails()
    {
        SkipIfNotConfigured();

        var project = await _client!.GetProjectAsync(_projectId!);

        Assert.NotNull(project);
        Assert.Equal(_projectId, project.Id);
        Assert.NotNull(project.Name);
    }

    [SkippableFact]
    public async Task ListEnvironments_ReturnsEnvironments()
    {
        SkipIfNotConfigured();

        var environments = await _client!.ListEnvironmentsAsync(_projectId!);

        Assert.NotNull(environments);
        Assert.NotEmpty(environments);
    }

    [SkippableFact]
    public async Task SecretCrudOperations_WorkCorrectly()
    {
        SkipIfNotConfigured();

        var testValue = $"test-value-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // Create
        await _client!.SetSecretAsync(_projectId!, _environment, _testSecretKey, testValue);

        // Read
        var retrieved = await _client.GetSecretAsync(_projectId!, _environment, _testSecretKey);
        Assert.NotNull(retrieved);
        Assert.Equal(_testSecretKey, retrieved.Key);
        Assert.StartsWith("test-value-", retrieved.Value);

        // List includes our secret
        var secrets = await _client.GetSecretsAsync(_projectId!, _environment);
        Assert.Contains(secrets, s => s.Key == _testSecretKey);

        // Update
        var updatedValue = $"updated-value-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await _client.SetSecretAsync(_projectId!, _environment, _testSecretKey, updatedValue);
        _client.ClearCache(_projectId!, _environment);
        var updated = await _client.GetSecretAsync(_projectId!, _environment, _testSecretKey);
        Assert.Equal(updatedValue, updated.Value);

        // Delete
        await _client.DeleteSecretAsync(_projectId!, _environment, _testSecretKey);
        _client.ClearCache(_projectId!, _environment);
        await Assert.ThrowsAsync<KeyEnvException>(async () =>
            await _client.GetSecretAsync(_projectId!, _environment, _testSecretKey));
    }

    [SkippableFact]
    public async Task GenerateEnvFile_GeneratesValidContent()
    {
        SkipIfNotConfigured();

        // Create a test secret first
        await _client!.SetSecretAsync(_projectId!, _environment, _testSecretKey, "test-value");

        var envContent = await _client.GenerateEnvFileAsync(_projectId!, _environment);

        Assert.NotNull(envContent);
        Assert.Contains($"{_testSecretKey}=", envContent);
    }
}
