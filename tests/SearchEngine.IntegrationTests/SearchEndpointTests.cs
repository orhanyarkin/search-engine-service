using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SearchEngine.Application.DTOs;

namespace SearchEngine.IntegrationTests;

/// <summary>
/// Arama API endpoint'i entegrasyon testleri.
/// Gerçek HTTP istekleri ile arama, filtreleme, sayfalama ve ID ile getirme senaryoları.
/// </summary>
public class SearchEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public SearchEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Search_WithoutParams_ShouldReturnAllContent()
    {
        // Arrange
        await _factory.SeedDataAsync();

        // Act
        var response = await _client.GetAsync("/api/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Search_WithSortByRecency_ShouldReturnOk()
    {
        // Arrange
        await _factory.SeedDataAsync();

        // Act — sortBy parametresi ile arama
        var response = await _client.GetAsync("/api/search?sortBy=recency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Search_WithTypeFilter_ShouldReturnOnlyMatchingType()
    {
        // Arrange
        await _factory.SeedDataAsync();

        // Act
        var response = await _client.GetAsync("/api/search?type=article");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().OnlyContain(i => i.ContentType == "Article");
    }

    [Fact]
    public async Task Search_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        await _factory.SeedDataAsync();

        // Act
        var response = await _client.GetAsync("/api/search?page=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        result.Should().NotBeNull();
        result!.Items.Count.Should().BeLessThanOrEqualTo(2);
        result.PageSize.Should().Be(2);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task Search_InvalidPage_ShouldReturnBadRequest()
    {
        // Act — page=0 FluentValidation tarafından reddedilir
        var response = await _client.GetAsync("/api/search?page=0");

        // Assert — 400 Bad Request (doğrulama hatası)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_InvalidPageSize_ShouldReturnBadRequest()
    {
        // Act — pageSize=100 FluentValidation tarafından reddedilir (maks: 50)
        var response = await _client.GetAsync("/api/search?pageSize=100");

        // Assert — 400 Bad Request (doğrulama hatası)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_ExistingContent_ShouldReturnContent()
    {
        // Arrange
        await _factory.SeedDataAsync();

        // Act
        var response = await _client.GetAsync("/api/contents/11111111-1111-1111-1111-111111111111");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ContentDto>();
        result.Should().NotBeNull();
        result!.Title.Should().Be("Docker Tutorial");
    }

    [Fact]
    public async Task GetById_NonExistingContent_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/contents/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_ResponseShouldContainCorrelationIdHeader()
    {
        // Act
        var response = await _client.GetAsync("/api/search");

        // Assert — X-Correlation-Id header mevcut
        response.Headers.Should().ContainKey("X-Correlation-Id");
        var correlationId = response.Headers.GetValues("X-Correlation-Id").FirstOrDefault();
        correlationId.Should().NotBeNullOrEmpty();
    }
}
