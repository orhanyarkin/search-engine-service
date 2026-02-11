using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Application.Services;

/// <summary>
/// İçerik puanlama algoritmasını uygular.
/// FinalScore = (BaseScore x TypeCoefficient) + FreshnessScore + EngagementScore
/// </summary>
public class ContentScorer : IContentScorer
{
    /// <inheritdoc />
    public double CalculateScore(Content content)
        => CalculateScore(content, DateTime.UtcNow);

    /// <inheritdoc />
    public double CalculateScore(Content content, DateTime referenceDate)
    {
        var baseScore = CalculateBaseScore(content);
        var typeCoefficient = GetTypeCoefficient(content.ContentType);
        var freshnessScore = CalculateFreshnessScore(content.PublishedAt, referenceDate);
        var engagementScore = CalculateEngagementScore(content);

        return (baseScore * typeCoefficient) + freshnessScore + engagementScore;
    }

    private static double CalculateBaseScore(Content content) => content.ContentType switch
    {
        ContentType.Video => (content.Views ?? 0) / 1000.0 + (content.Likes ?? 0) / 100.0,
        ContentType.Article => (content.ReadingTime ?? 0) + (content.Reactions ?? 0) / 50.0,
        _ => 0
    };

    private static double GetTypeCoefficient(ContentType type) => type switch
    {
        ContentType.Video => 1.5,
        ContentType.Article => 1.0,
        _ => 1.0
    };

    internal static double CalculateFreshnessScore(DateTime publishedAt, DateTime referenceDate)
    {
        var age = referenceDate - publishedAt;
        return age.TotalDays switch
        {
            <= 7 => 5,
            <= 30 => 3,
            <= 90 => 1,
            _ => 0
        };
    }

    private static double CalculateEngagementScore(Content content) => content.ContentType switch
    {
        ContentType.Video when (content.Views ?? 0) > 0
            => (content.Likes ?? 0) / (double)(content.Views!.Value) * 10,
        ContentType.Article when (content.ReadingTime ?? 0) > 0
            => (content.Reactions ?? 0) / (double)(content.ReadingTime!.Value) * 5,
        _ => 0
    };
}
