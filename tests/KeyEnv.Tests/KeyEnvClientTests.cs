using KeyEnv;
using KeyEnv.Types;

namespace KeyEnv.Tests;

public class KeyEnvClientTests
{
    [Fact]
    public void Create_WithToken_ReturnsClient()
    {
        // Arrange & Act
        using var client = KeyEnvClient.Create("test-token");

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_WithOptions_ReturnsClient()
    {
        // Arrange & Act
        using var client = KeyEnvClient.Create(new KeyEnvOptions
        {
            Token = "test-token",
            BaseUrl = "https://custom-api.example.com",
            Timeout = TimeSpan.FromSeconds(60),
            CacheTtl = TimeSpan.FromMinutes(5)
        });

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_WithEmptyToken_ThrowsException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<KeyEnvException>(() => KeyEnvClient.Create(""));
        Assert.Contains("Token is required", ex.Message);
    }

    [Fact]
    public void Create_WithNullToken_ThrowsException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<KeyEnvException>(() => KeyEnvClient.Create(new KeyEnvOptions
        {
            Token = null!
        }));
        Assert.Contains("Token is required", ex.Message);
    }

    [Fact]
    public void Create_WithWhitespaceToken_ThrowsException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<KeyEnvException>(() => KeyEnvClient.Create("   "));
        Assert.Contains("Token is required", ex.Message);
    }
}

public class KeyEnvExceptionTests
{
    [Fact]
    public void IsNotFound_Returns_True_For_404()
    {
        var ex = new KeyEnvException("Not found", 404);
        Assert.True(ex.IsNotFound);
    }

    [Fact]
    public void IsUnauthorized_Returns_True_For_401()
    {
        var ex = new KeyEnvException("Unauthorized", 401);
        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public void IsForbidden_Returns_True_For_403()
    {
        var ex = new KeyEnvException("Forbidden", 403);
        Assert.True(ex.IsForbidden);
    }

    [Fact]
    public void IsConflict_Returns_True_For_409()
    {
        var ex = new KeyEnvException("Conflict", 409);
        Assert.True(ex.IsConflict);
    }

    [Fact]
    public void IsRateLimited_Returns_True_For_429()
    {
        var ex = new KeyEnvException("Rate limited", 429);
        Assert.True(ex.IsRateLimited);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void IsServerError_Returns_True_For_5xx(int statusCode)
    {
        var ex = new KeyEnvException("Server error", statusCode);
        Assert.True(ex.IsServerError);
    }

    [Fact]
    public void ToString_IncludesStatusAndCode()
    {
        var ex = new KeyEnvException("Test error", 400, "VALIDATION_ERROR");
        var result = ex.ToString();

        Assert.Contains("Test error", result);
        Assert.Contains("400", result);
        Assert.Contains("VALIDATION_ERROR", result);
    }
}

public class SecretInputTests
{
    [Fact]
    public void Create_WithKeyAndValue_ReturnsInput()
    {
        var input = SecretInput.Create("API_KEY", "secret-value");

        Assert.Equal("API_KEY", input.Key);
        Assert.Equal("secret-value", input.Value);
        Assert.Null(input.Description);
    }

    [Fact]
    public void Create_WithDescription_ReturnsInput()
    {
        var input = SecretInput.Create("API_KEY", "secret-value", "My API key");

        Assert.Equal("API_KEY", input.Key);
        Assert.Equal("secret-value", input.Value);
        Assert.Equal("My API key", input.Description);
    }
}
