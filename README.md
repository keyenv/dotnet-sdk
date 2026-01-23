# KeyEnv .NET SDK

Official .NET SDK for [KeyEnv](https://keyenv.dev) - Secrets management made simple.

## Installation

```bash
dotnet add package KeyEnv
```

Or via the NuGet Package Manager:

```powershell
Install-Package KeyEnv
```

## Quick Start

```csharp
using KeyEnv;

// Create a client with your service token
var client = KeyEnvClient.Create("your-service-token");

// Fetch all secrets for an environment
var secrets = await client.GetSecretsAsync("your-project-id", "production");

foreach (var secret in secrets)
{
    Console.WriteLine($"{secret.Key}={secret.Value}");
}
```

## Loading Secrets into Environment Variables

```csharp
// Load all secrets into environment variables
int count = await client.LoadEnvAsync("your-project-id", "production");
Console.WriteLine($"Loaded {count} secrets");

// Now use them
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
```

## Getting a Single Secret

```csharp
var secret = await client.GetSecretAsync("your-project-id", "production", "DATABASE_URL");
Console.WriteLine(secret.Value);
```

## Setting Secrets

```csharp
// Set a secret (creates or updates)
await client.SetSecretAsync(
    "your-project-id",
    "production",
    "API_KEY",
    "sk_live_...",
    description: "My API key"
);
```

## Bulk Import

```csharp
var secrets = new[]
{
    SecretInput.Create("DATABASE_URL", "postgres://..."),
    SecretInput.Create("API_KEY", "sk_...", "My API key"),
};

var result = await client.BulkImportAsync(
    "your-project-id",
    "development",
    secrets,
    new BulkImportOptions { Overwrite = true }
);

Console.WriteLine($"Created: {result.Created}, Updated: {result.Updated}");
```

## Error Handling

```csharp
try
{
    var secret = await client.GetSecretAsync("project-id", "production", "MISSING_KEY");
}
catch (KeyEnvException ex) when (ex.IsNotFound)
{
    Console.WriteLine("Secret not found");
}
catch (KeyEnvException ex) when (ex.IsUnauthorized)
{
    Console.WriteLine("Invalid or expired token");
}
catch (KeyEnvException ex) when (ex.IsForbidden)
{
    Console.WriteLine("Access denied");
}
catch (KeyEnvException ex)
{
    Console.WriteLine($"Error {ex.StatusCode}: {ex.Message}");
}
```

## Configuration Options

```csharp
var client = KeyEnvClient.Create(new KeyEnvOptions
{
    Token = "your-service-token",
    BaseUrl = "https://api.keyenv.dev",      // Optional: custom API URL
    Timeout = TimeSpan.FromSeconds(60),       // Optional: request timeout
    CacheTtl = TimeSpan.FromMinutes(5)        // Optional: cache secrets for 5 min
});
```

## Caching

Enable caching for better performance in serverless environments:

```csharp
var client = KeyEnvClient.Create(new KeyEnvOptions
{
    Token = "your-token",
    CacheTtl = TimeSpan.FromMinutes(5)  // Cache secrets for 5 minutes
});

// First call fetches from API
var secrets1 = await client.GetSecretsAsync("project-id", "production");

// Second call returns cached result
var secrets2 = await client.GetSecretsAsync("project-id", "production");

// Clear cache when needed
client.ClearCache("project-id", "production");
// Or clear all cache
client.ClearAllCache();
```

## Generating .env Files

```csharp
string envContent = await client.GenerateEnvFileAsync("project-id", "production");
await File.WriteAllTextAsync(".env", envContent);
```

## ASP.NET Core Integration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Load KeyEnv secrets before building
using var keyenv = KeyEnvClient.Create(builder.Configuration["KeyEnv:Token"]!);
await keyenv.LoadEnvAsync(
    builder.Configuration["KeyEnv:ProjectId"]!,
    builder.Environment.EnvironmentName.ToLowerInvariant()
);

// Now environment variables are available
var app = builder.Build();
```

## API Reference

### KeyEnvClient

| Method | Description |
|--------|-------------|
| `Create(token)` | Create a client with a token |
| `Create(options)` | Create a client with options |
| `GetSecretsAsync(projectId, environment)` | Get all secrets for an environment |
| `GetSecretAsync(projectId, environment, key)` | Get a single secret |
| `SetSecretAsync(projectId, environment, key, value, description?)` | Create or update a secret |
| `DeleteSecretAsync(projectId, environment, key)` | Delete a secret |
| `LoadEnvAsync(projectId, environment)` | Load secrets into environment variables |
| `GenerateEnvFileAsync(projectId, environment)` | Generate .env file content |
| `BulkImportAsync(projectId, environment, secrets, options?)` | Import multiple secrets |
| `GetSecretHistoryAsync(projectId, environment, key)` | Get secret version history |
| `ValidateTokenAsync()` | Validate the token and get user info |
| `ClearCache(projectId, environment)` | Clear cache for an environment |
| `ClearAllCache()` | Clear all cached data |

### KeyEnvException

| Property | Description |
|----------|-------------|
| `StatusCode` | HTTP status code (0 for non-HTTP errors) |
| `ErrorCode` | Optional error code for programmatic handling |
| `IsNotFound` | True if 404 error |
| `IsUnauthorized` | True if 401 error |
| `IsForbidden` | True if 403 error |
| `IsConflict` | True if 409 error |
| `IsRateLimited` | True if 429 error |
| `IsServerError` | True if 5xx error |

## Requirements

- .NET 6.0 or later
- Nullable reference types enabled

## License

MIT License - see [LICENSE](LICENSE) for details.
