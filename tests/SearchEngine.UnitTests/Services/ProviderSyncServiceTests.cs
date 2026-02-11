using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SearchEngine.Application.Commands;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Configuration;
using SearchEngine.Infrastructure.Messaging;
using SearchEngine.Infrastructure.Services;

namespace SearchEngine.UnitTests.Services;

/// <summary>
/// ProviderSyncService testleri.
/// Senkronizasyon orkestrasyonu, hata toleransı, olay yayınlama ve SemaphoreSlim koruması.
/// </summary>
public class ProviderSyncServiceTests
{
    private readonly Mock<IContentProviderFactory> _factoryMock = new();
    private readonly Mock<IContentScorer> _scorerMock = new();
    private readonly Mock<IContentRepository> _repositoryMock = new();
    private readonly Mock<IPublishEndpoint> _publishMock = new();
    private readonly Mock<ILogger<ProviderSyncService>> _loggerMock = new();
    private readonly ProviderSyncService _sut;

    public ProviderSyncServiceTests()
    {
        var settings = Options.Create(new ProviderSettings { AdjustDatesToNow = false });

        _sut = new ProviderSyncService(
            _factoryMock.Object,
            _scorerMock.Object,
            _repositoryMock.Object,
            _publishMock.Object,
            _loggerMock.Object,
            settings);
    }

    [Fact]
    public async Task SyncAllAsync_ShouldFetchFromAllProviders()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", 3);
        var provider2 = CreateMockProvider("Provider2", 2);

        _factoryMock.Setup(f => f.GetAllProviders())
            .Returns([provider1.Object, provider2.Object]);

        _scorerMock.Setup(s => s.CalculateScore(It.IsAny<Content>())).Returns(10);

        // Act
        var count = await _sut.SyncAllAsync();

        // Assert
        count.Should().Be(5);
        _repositoryMock.Verify(r => r.UpsertManyAsync(
            It.Is<List<Content>>(l => l.Count == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAllAsync_OneProviderFails_ShouldContinueWithOthers()
    {
        // Arrange — ilk sağlayıcı hata verir, ikincisi başarılı
        var failingProvider = new Mock<IContentProvider>();
        failingProvider.Setup(p => p.ProviderName).Returns("Failing");
        failingProvider.Setup(p => p.FetchContentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Bağlantı hatası"));

        var successProvider = CreateMockProvider("Success", 3);

        _factoryMock.Setup(f => f.GetAllProviders())
            .Returns([failingProvider.Object, successProvider.Object]);

        _scorerMock.Setup(s => s.CalculateScore(It.IsAny<Content>())).Returns(10);

        // Act
        var count = await _sut.SyncAllAsync();

        // Assert — yalnızca başarılı sağlayıcının verileri kaydedildi
        count.Should().Be(3);
    }

    [Fact]
    public async Task SyncAllAsync_NoContentFetched_ShouldReturnZero()
    {
        // Arrange
        _factoryMock.Setup(f => f.GetAllProviders())
            .Returns(Array.Empty<IContentProvider>());

        // Act
        var count = await _sut.SyncAllAsync();

        // Assert
        count.Should().Be(0);
        _repositoryMock.Verify(r => r.UpsertManyAsync(It.IsAny<List<Content>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncAllAsync_ShouldPublishEvent()
    {
        // Arrange
        var provider = CreateMockProvider("Test", 2);
        _factoryMock.Setup(f => f.GetAllProviders()).Returns([provider.Object]);
        _scorerMock.Setup(s => s.CalculateScore(It.IsAny<Content>())).Returns(10);

        // Act
        await _sut.SyncAllAsync();

        // Assert — RabbitMQ olayı yayınlandı
        _publishMock.Verify(p => p.Publish(
            It.Is<ProviderDataSyncedEvent>(e => e.ItemCount == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IContentProvider> CreateMockProvider(string name, int itemCount)
    {
        var mock = new Mock<IContentProvider>();
        mock.Setup(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.FetchContentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, itemCount).Select(i => new Content
            {
                Id = Guid.NewGuid(),
                ExternalId = $"{name}-{i}",
                Title = $"Content {i}",
                ContentType = ContentType.Video,
                SourceProvider = name,
                PublishedAt = DateTime.UtcNow.AddDays(-i),
                Tags = ["test"]
            }).ToList());
        return mock;
    }
}
