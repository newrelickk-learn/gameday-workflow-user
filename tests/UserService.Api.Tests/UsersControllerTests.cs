using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UserService.Application.DTOs;
using Xunit;

namespace UserService.Api.Tests;

public class UsersControllerTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public UsersControllerTests(ApiTestFixture fixture)
    {
        _client = fixture.Client;
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new LoginRequest
        {
            Email = "engineer@example.com",
            Password = "password"
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return loginResponse!.Token;
    }

    [Fact]
    public async Task GetUserById_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/users/28151");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithValidToken_ReturnsUser()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/users/28151");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        user!.Id.Should().Be("28151");
        user.Email.Should().Be("engineer@example.com");
        user.Role.Should().Be("engineer");
    }

    [Fact]
    public async Task GetUserById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/users/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("1051", "director@example.com", "director")]
    [InlineData("16051", "accounting@example.com", "accounting")]
    [InlineData("21051", "manager@example.com", "manager")]
    [InlineData("28151", "engineer@example.com", "engineer")]
    public async Task GetUserById_WithDifferentUsers_ReturnsCorrectUser(string userId, string expectedEmail, string expectedRole)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(userId);
        user.Email.Should().Be(expectedEmail);
        user.Role.Should().Be(expectedRole);
    }
}

