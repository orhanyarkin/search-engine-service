using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SearchEngine.Application.DTOs;

namespace SearchEngine.IntegrationTests;

/// <summary>
/// Kimlik doğrulama endpoint'i entegrasyon testleri.
/// JWT token alma, yetkisiz erişim ve anonim endpoint senaryoları.
/// </summary>
public class AuthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _authenticatedClient;

    public AuthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _authenticatedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturnToken()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin", "admin123"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ShouldReturnUnauthorized()
    {
        // Act — geçersiz ama validasyondan geçen kimlik bilgileri (min 3 kullanıcı adı, min 6 şifre)
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("wronguser", "wrongpass"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_WithoutToken_ShouldReturnUnauthorized()
    {
        // Act — token olmadan arama
        var response = await _client.GetAsync("/api/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sync_WithoutToken_ShouldReturnUnauthorized()
    {
        // Act — token olmadan sync
        var response = await _client.GetAsync("/api/providers/sync");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthCheck_WithoutToken_ShouldStillWork()
    {
        // Act — health endpoint auth gerektirmez
        var response = await _client.GetAsync("/health");

        // Assert — 200 OK veya 503 (servisler stub olduğu için)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Login_VersionedUrl_ShouldReturnToken()
    {
        // Act — versioned URL ile login
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin", "admin123"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Search_VersionedUrl_ShouldReportApiVersion()
    {
        // Act — versioned URL ile arama (authenticated)
        var response = await _authenticatedClient.GetAsync("/api/v1/search");

        // Assert — api-supported-versions header mevcut olmalı
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("api-supported-versions");
    }
}
