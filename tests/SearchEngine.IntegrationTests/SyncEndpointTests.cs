using System.Net;
using FluentAssertions;

namespace SearchEngine.IntegrationTests;

/// <summary>
/// Senkronizasyon API endpoint'i entegrasyon testleri.
/// GET ve POST /api/providers/sync her ikisinin de çalıştığını doğrular.
/// </summary>
public class SyncEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SyncEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Sync_GET_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/providers/sync");

        // Assert — GET sync endpoint çalışmalı
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sync_POST_ShouldReturnOk()
    {
        // Act
        var response = await _client.PostAsync("/api/providers/sync", null);

        // Assert — POST sync endpoint çalışmalı
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
