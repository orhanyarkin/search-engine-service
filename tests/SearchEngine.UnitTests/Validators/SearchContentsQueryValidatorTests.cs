using FluentAssertions;
using FluentValidation.TestHelper;
using SearchEngine.Application.Queries;
using SearchEngine.Application.Validators;
using SearchEngine.Domain.Enums;

namespace SearchEngine.UnitTests.Validators;

/// <summary>
/// SearchContentsQueryValidator testleri.
/// Sayfalama, pageSize ve keyword doğrulama kurallarını test eder.
/// </summary>
public class SearchContentsQueryValidatorTests
{
    private readonly SearchContentsQueryValidator _validator = new();

    [Fact]
    public void ValidQuery_ShouldPassValidation()
    {
        // Arrange
        var query = new SearchContentsQuery("docker", ContentType.Video, SortBy.Popularity, 1, 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Page_LessThanOne_ShouldFail(int page)
    {
        // Arrange
        var query = new SearchContentsQuery(null, null, SortBy.Popularity, page, 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(100)]
    public void PageSize_OutOfRange_ShouldFail(int pageSize)
    {
        // Arrange
        var query = new SearchContentsQuery(null, null, SortBy.Popularity, 1, pageSize);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Keyword_TooLong_ShouldFail()
    {
        // Arrange — 201 karakter
        var longKeyword = new string('a', 201);
        var query = new SearchContentsQuery(longKeyword, null, SortBy.Popularity, 1, 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Keyword);
    }

    [Fact]
    public void Keyword_Null_ShouldPass()
    {
        // Arrange
        var query = new SearchContentsQuery(null, null, SortBy.Popularity, 1, 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Keyword);
    }

    [Fact]
    public void Keyword_MaxLength200_ShouldPass()
    {
        // Arrange — tam 200 karakter
        var keyword = new string('a', 200);
        var query = new SearchContentsQuery(keyword, null, SortBy.Popularity, 1, 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Keyword);
    }
}
