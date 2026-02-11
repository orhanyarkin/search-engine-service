using FluentAssertions;
using Moq;
using SearchEngine.Application.Queries;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.UnitTests.Queries;

/// <summary>
/// GetContentByIdQueryHandler testleri.
/// ID ile içerik getirme, bulunamama ve DTO dönüşüm senaryoları.
/// </summary>
public class GetContentByIdQueryHandlerTests
{
    private readonly Mock<IContentRepository> _repositoryMock = new();
    private readonly GetContentByIdQueryHandler _handler;

    public GetContentByIdQueryHandlerTests()
    {
        _handler = new GetContentByIdQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ContentExists_ShouldReturnDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var content = new Content
        {
            Id = id,
            Title = "Docker Tutorial",
            ContentType = ContentType.Video,
            SourceProvider = "Provider1_JSON",
            FinalScore = 42.5,
            PublishedAt = DateTime.UtcNow,
            Tags = ["docker", "devops"],
            Views = 22000,
            Likes = 1800,
            Duration = "25:15"
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        var result = await _handler.Handle(new GetContentByIdQuery(id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Title.Should().Be("Docker Tutorial");
        result.ContentType.Should().Be("Video");
        result.FinalScore.Should().Be(42.5);
        result.Tags.Should().Contain("docker");
    }

    [Fact]
    public async Task Handle_ContentNotFound_ShouldReturnNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Content?)null);

        // Act
        var result = await _handler.Handle(new GetContentByIdQuery(id), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryWithCorrectId()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Content?)null);

        // Act
        await _handler.Handle(new GetContentByIdQuery(id), CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
