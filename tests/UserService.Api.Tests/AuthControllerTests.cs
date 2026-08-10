using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UserService.Application.DTOs;
using Xunit;

namespace UserService.Api.Tests;

public class AuthControllerTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public AuthControllerTests(ApiTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "engineer@example.com",
            Password = "password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.Should().NotBeNullOrEmpty();
        loginResponse.User.Should().NotBeNull();
        loginResponse.User.Email.Should().Be("engineer@example.com");
        loginResponse.User.Role.Should().Be("engineer");
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "invalid@example.com",
            Password = "password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "engineer@example.com",
            Password = "wrongpassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("director@example.com", "director")]
    [InlineData("accounting@example.com", "accounting")]
    [InlineData("manager@example.com", "manager")]
    [InlineData("engineer@example.com", "engineer")]
    public async Task Login_WithDifferentRoles_ReturnsCorrectRole(string email, string expectedRole)
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = email,
            Password = "password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.User.Role.Should().Be(expectedRole);
    }
}

