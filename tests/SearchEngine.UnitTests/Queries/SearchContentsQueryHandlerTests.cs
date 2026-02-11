using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SearchEngine.Application.Queries;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.UnitTests.Queries;

public class SearchContentsQueryHandlerTests
{
    private readonly Mock<IContentRepository> _repositoryMock = new();
    private readonly Mock<ISearchService> _searchServiceMock = new();
    private readonly Mock<ILogger<SearchContentsQueryHandler>> _loggerMock = new();
    private readonly SearchContentsQueryHandler _handler;

    public SearchContentsQueryHandlerTests()
    {
        // Varsayılan: ES kullanılamıyor → her zaman DB'ye fallback
        _searchServiceMock.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _handler = new SearchContentsQueryHandler(
            _repositoryMock.Object,
            _searchServiceMock.Object,
            _loggerMock.Object);
    }

    private static List<Content> CreateSampleContents(int count = 3) =>
        Enumerable.Range(1, count).Select(i => new Content
        {
            Id = Guid.NewGuid(),
            Title = $"Test Content {i}",
            ContentType = i % 2 == 0 ? ContentType.Article : ContentType.Video,
            SourceProvider = "TestProvider",
            FinalScore = 100 - i,
            PublishedAt = DateTime.UtcNow.AddDays(-i),
            Tags = [$"tag{i}"]
        }).ToList();

    [Fact]
    public async Task Handle_ShouldReturnPaginatedResults()
    {
        var contents = CreateSampleContents(3);
        _repositoryMock.Setup(r => r.SearchAsync(null, null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((contents, 3));

        var query = new SearchContentsQuery(null, null, SortBy.Popularity, 1, 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCalculateTotalPagesCorrectly()
    {
        var contents = CreateSampleContents(2);
        _repositoryMock.Setup(r => r.SearchAsync(null, null, SortBy.Popularity, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((contents, 5));

        var query = new SearchContentsQuery(null, null, SortBy.Popularity, 1, 2);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalPages.Should().Be(3); // ceil(5/2)
    }

    [Fact]
    public async Task Handle_ShouldPassKeywordToRepository()
    {
        _repositoryMock.Setup(r => r.SearchAsync("docker", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Content>(), 0));

        var query = new SearchContentsQuery("docker", null, SortBy.Popularity, 1, 10);
        await _handler.Handle(query, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync("docker", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPassTypeFilterToRepository()
    {
        _repositoryMock.Setup(r => r.SearchAsync(null, ContentType.Video, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Content>(), 0));

        var query = new SearchContentsQuery(null, ContentType.Video, SortBy.Popularity, 1, 10);
        await _handler.Handle(query, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync(null, ContentType.Video, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapContentsToDtos()
    {
        var contents = new List<Content>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Test Video",
                ContentType = ContentType.Video,
                SourceProvider = "Provider1_JSON",
                FinalScore = 46.3,
                PublishedAt = DateTime.UtcNow,
                Tags = ["programming"],
                Views = 15000,
                Likes = 1200,
                Duration = "15:30"
            }
        };
        _repositoryMock.Setup(r => r.SearchAsync(null, null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((contents, 1));

        var query = new SearchContentsQuery(null, null, SortBy.Popularity, 1, 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        var dto = result.Items[0];
        dto.Title.Should().Be("Test Video");
        dto.ContentType.Should().Be("Video");
        dto.Views.Should().Be(15000);
        dto.FinalScore.Should().Be(46.3);
    }

    [Fact]
    public async Task Handle_KeywordWithEsAvailable_ShouldUseElasticsearch()
    {
        // Arrange — ES kullanılabilir
        _searchServiceMock.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var esContents = CreateSampleContents(2);
        _searchServiceMock.Setup(s => s.SearchAsync("docker", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((esContents, 2));

        // Act
        var query = new SearchContentsQuery("docker", null, SortBy.Popularity, 1, 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — ES çağrıldı, DB çağrılmadı
        result.Items.Should().HaveCount(2);
        _searchServiceMock.Verify(s => s.SearchAsync("docker", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.SearchAsync(It.IsAny<string?>(), It.IsAny<ContentType?>(), It.IsAny<SortBy>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KeywordWithEsUnavailable_ShouldFallbackToDb()
    {
        // Arrange — ES kullanılamıyor (varsayılan setup)
        _repositoryMock.Setup(r => r.SearchAsync("docker", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Content>(), 0));

        // Act
        var query = new SearchContentsQuery("docker", null, SortBy.Popularity, 1, 10);
        await _handler.Handle(query, CancellationToken.None);

        // Assert — DB'ye fallback yapıldı
        _repositoryMock.Verify(r => r.SearchAsync("docker", null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
        _searchServiceMock.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType?>(), It.IsAny<SortBy>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoKeyword_ShouldAlwaysUseDb()
    {
        // Arrange — ES kullanılabilir olsa bile, keyword yoksa DB kullanılmalı
        _searchServiceMock.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repositoryMock.Setup(r => r.SearchAsync(null, null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Content>(), 0));

        // Act
        var query = new SearchContentsQuery(null, null, SortBy.Popularity, 1, 10);
        await _handler.Handle(query, CancellationToken.None);

        // Assert — keyword yoksa doğrudan DB
        _repositoryMock.Verify(r => r.SearchAsync(null, null, SortBy.Popularity, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
