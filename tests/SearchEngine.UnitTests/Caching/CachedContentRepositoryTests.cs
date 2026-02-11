using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Caching;
using SearchEngine.Infrastructure.Configuration;

namespace SearchEngine.UnitTests.Caching;

/// <summary>
/// CachedContentRepository (Dekoratör deseni) testleri.
/// Önbellek kaçırma, passthrough ve set senaryolarını doğrular.
/// Not: CachedSearchResult private olduğu için önbellek isabet testi doğrudan yapılamaz,
/// ancak önbellek kaçırma akışı ve SetAsync çağrısı doğrulanabilir.
/// </summary>
public class CachedContentRepositoryTests
{
    private readonly Mock<IContentRepository> _innerMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly CachedContentRepository _sut;

    public CachedContentRepositoryTests()
    {
        var settings = Options.Create(new CacheSettings
        {
            SearchTtlMinutes = 5,
            ContentTtlMinutes = 10
        });

        // Varsayılan: önbellek boş (GetAsync her zaman default döner)
        _sut = new CachedContentRepository(_innerMock.Object, _cacheMock.Object, settings);
    }

    [Fact]
    public async Task SearchAsync_CacheMiss_ShouldCallInnerRepository()
    {
        // Arrange
        var dbItems = new List<Content> { CreateContent("DB Item") };
        _innerMock.Setup(r => r.SearchAsync("test", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((dbItems, 1));

        // Act
        var result = await _sut.SearchAsync("test", null, SortBy.Popularity, 1, 10);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("DB Item");
        _innerMock.Verify(r => r.SearchAsync("test", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldGenerateCorrectCacheKey()
    {
        // Arrange
        _innerMock.Setup(r => r.SearchAsync("docker", ContentType.Video, SortBy.Recency, 2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Content>(), 0));

        // Act
        await _sut.SearchAsync("docker", ContentType.Video, SortBy.Recency, 2, 20);

        // Assert — önbellek anahtarı doğru formatta
        _cacheMock.Verify(c => c.SetAsync(
            "searchengine:search:docker:Video:Recency:2:20",
            It.IsAny<object>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_CacheMiss_ShouldSetCacheWithCorrectTtl()
    {
        // Arrange
        _innerMock.Setup(r => r.SearchAsync(null, null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Content>(), 0));

        // Act
        await _sut.SearchAsync(null, null, SortBy.Popularity, 1, 10);

        // Assert — 5 dakika TTL ile önbellek set çağrıldı
        _cacheMock.Verify(c => c.SetAsync(
            It.Is<string>(k => k.StartsWith("searchengine:search:")),
            It.IsAny<object>(),
            TimeSpan.FromMinutes(5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_CacheMiss_ShouldCallInnerAndSetCache()
    {
        // Arrange
        var id = Guid.NewGuid();
        var content = CreateContent("Test");
        content.Id = id;

        _cacheMock.Setup(c => c.GetAsync<Content>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Content?)null);

        _innerMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Test");
        _cacheMock.Verify(c => c.SetAsync(
            $"searchengine:content:{id}",
            It.IsAny<Content>(),
            TimeSpan.FromMinutes(10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ShouldNotSetCache()
    {
        // Arrange
        var id = Guid.NewGuid();
        _cacheMock.Setup(c => c.GetAsync<Content>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Content?)null);
        _innerMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Content?)null);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        result.Should().BeNull();
        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<Content>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpsertManyAsync_ShouldPassthroughToInner()
    {
        // Arrange
        var contents = new List<Content> { CreateContent("New") };

        // Act
        await _sut.UpsertManyAsync(contents);

        // Assert — doğrudan iç depoya delege eder, önbelleğe dokunmaz
        _innerMock.Verify(r => r.UpsertManyAsync(contents, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Content CreateContent(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        ContentType = ContentType.Video,
        SourceProvider = "Test",
        FinalScore = 10,
        PublishedAt = DateTime.UtcNow,
        Tags = ["test"]
    };
}
