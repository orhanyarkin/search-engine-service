using FluentAssertions;
using Moq;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Interfaces;
using SearchEngine.Infrastructure.Providers;

namespace SearchEngine.UnitTests.Providers;

/// <summary>
/// ContentProviderFactory testleri.
/// Factory pattern — provider çözümleme, case-insensitive arama ve hata senaryoları.
/// </summary>
public class ContentProviderFactoryTests
{
    [Fact]
    public void GetProvider_ShouldReturnCorrectProvider()
    {
        // Arrange
        var jsonProvider = CreateMockProvider("Provider1_JSON");
        var xmlProvider = CreateMockProvider("Provider2_XML");
        var factory = new ContentProviderFactory([jsonProvider.Object, xmlProvider.Object]);

        // Act
        var result = factory.GetProvider("Provider1_JSON");

        // Assert
        result.ProviderName.Should().Be("Provider1_JSON");
    }

    [Fact]
    public void GetProvider_CaseInsensitive_ShouldResolve()
    {
        // Arrange
        var provider = CreateMockProvider("Provider1_JSON");
        var factory = new ContentProviderFactory([provider.Object]);

        // Act
        var result = factory.GetProvider("provider1_json");

        // Assert
        result.ProviderName.Should().Be("Provider1_JSON");
    }

    [Fact]
    public void GetProvider_NotRegistered_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var factory = new ContentProviderFactory([]);

        // Act & Assert
        var act = () => factory.GetProvider("NonExistent");
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*NonExistent*");
    }

    [Fact]
    public void GetAllProviders_ShouldReturnAllRegistered()
    {
        // Arrange
        var p1 = CreateMockProvider("Provider1");
        var p2 = CreateMockProvider("Provider2");
        var factory = new ContentProviderFactory([p1.Object, p2.Object]);

        // Act
        var all = factory.GetAllProviders().ToList();

        // Assert
        all.Should().HaveCount(2);
    }

    private static Mock<IContentProvider> CreateMockProvider(string name)
    {
        var mock = new Mock<IContentProvider>();
        mock.Setup(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.FetchContentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Content>());
        return mock;
    }
}
