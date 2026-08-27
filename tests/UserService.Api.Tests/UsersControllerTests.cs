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
        var response = await _client.GetAsync("/users/28151");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithValidToken_ReturnsUser()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/users/28151");

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
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/users/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("1051", "director@example.com", "director")]
    [InlineData("16051", "accounting@example.com", "accounting")]
    [InlineData("21051", "manager@example.com", "manager")]
    [InlineData("28151", "engineer@example.com", "engineer")]
    public async Task GetUserById_WithDifferentUsers_ReturnsCorrectUser(string userId, string expectedEmail, string expectedRole)
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(userId);
        user.Email.Should().Be(expectedEmail);
        user.Role.Should().Be(expectedRole);
    }

    [Fact]
    public async Task GetUsersByIds_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/users/batch?ids=28151&ids=21051");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsersByIds_WithValidIds_ReturnsMatchingUsersOnly()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/users/batch?ids=28151&ids=21051&ids=99999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        users.Should().NotBeNull();
        users!.Select(u => u.Id).Should().BeEquivalentTo(new[] { "28151", "21051" });
    }

    [Fact]
    public async Task GetUsersByIds_WithEmptyIds_ReturnsEmptyArray()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/users/batch");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        users.Should().NotBeNull();
        users.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserById_And_GetUsersByIds_DoNotConflictInRouting()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var singleResponse = await _client.GetAsync("/users/28151");
        var batchResponse = await _client.GetAsync("/users/batch?ids=28151");

        singleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await singleResponse.Content.ReadFromJsonAsync<UserDto>();
        user!.Id.Should().Be("28151");

        batchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await batchResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        users!.Should().ContainSingle(u => u.Id == "28151");
    }
}

