using FluentAssertions;
using SearchEngine.Application.Services;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;

namespace SearchEngine.UnitTests.Services;

public class ContentScorerTests
{
    private readonly ContentScorer _scorer = new();

    // Deterministik testler için sabit referans tarihi
    private static readonly DateTime ReferenceDate = new(2024, 3, 20, 0, 0, 0, DateTimeKind.Utc);

    private static Content CreateVideo(int views = 15000, int likes = 1200, DateTime? publishedAt = null) => new()
    {
        ContentType = ContentType.Video,
        Views = views,
        Likes = likes,
        PublishedAt = publishedAt ?? new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc)
    };

    private static Content CreateArticle(int readingTime = 8, int reactions = 450, DateTime? publishedAt = null) => new()
    {
        ContentType = ContentType.Article,
        ReadingTime = readingTime,
        Reactions = reactions,
        PublishedAt = publishedAt ?? new DateTime(2024, 3, 14, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void Video_BaseScore_ShouldCalculateCorrectly()
    {
        // TemelPuan = görüntülenme/1000 + beğeni/100 = 15 + 12 = 27
        var video = CreateVideo(views: 15000, likes: 1200);
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // SonPuan = (27 * 1.5) + tazelikPuanı + etkileşimPuanı
        // Tazelik: 5 gün → 1 hafta içi → +5
        // Etkileşim: (1200/15000) * 10 = 0.8
        // = 40.5 + 5 + 0.8 = 46.3
        score.Should().BeApproximately(46.3, 0.01);
    }

    [Fact]
    public void Article_BaseScore_ShouldCalculateCorrectly()
    {
        // TemelPuan = okumaSüresi + tepki/50 = 8 + 9 = 17
        var article = CreateArticle(readingTime: 8, reactions: 450);
        var score = _scorer.CalculateScore(article, ReferenceDate);

        // SonPuan = (17 * 1.0) + tazelikPuanı + etkileşimPuanı
        // Tazelik: 6 gün → 1 hafta içi → +5
        // Etkileşim: (450/8) * 5 = 281.25
        // = 17 + 5 + 281.25 = 303.25
        score.Should().BeApproximately(303.25, 0.01);
    }

    [Fact]
    public void Video_TypeCoefficient_ShouldBe_1_5()
    {
        // 0 görüntülenme ve 0 beğeni → temel puan = 0
        // Etkileşim = 0 (görüntülenme yok)
        // Yalnızca tazelik etkili
        var video = CreateVideo(views: 0, likes: 0, publishedAt: ReferenceDate.AddDays(-100));
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // SonPuan = (0 * 1.5) + 0 + 0 = 0
        score.Should().Be(0);
    }

    [Fact]
    public void Article_TypeCoefficient_ShouldBe_1_0()
    {
        // 10 okuma süresi ve 0 tepki ile
        var article = CreateArticle(readingTime: 10, reactions: 0, publishedAt: ReferenceDate.AddDays(-100));
        var score = _scorer.CalculateScore(article, ReferenceDate);

        // SonPuan = (10 * 1.0) + 0 + 0 = 10
        score.Should().Be(10);
    }

    [Fact]
    public void FreshnessScore_WithinOneWeek_ShouldReturn5()
    {
        var video = CreateVideo(views: 1000, likes: 0, publishedAt: ReferenceDate.AddDays(-3));
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // TemelPuan = 1000/1000 + 0/100 = 1.0
        // SonPuan = (1.0 * 1.5) + 5 + 0 = 6.5
        score.Should().BeApproximately(6.5, 0.01);
    }

    [Fact]
    public void FreshnessScore_WithinOneMonth_ShouldReturn3()
    {
        var video = CreateVideo(views: 1000, likes: 0, publishedAt: ReferenceDate.AddDays(-15));
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // SonPuan = (1.0 * 1.5) + 3 + 0 = 4.5
        score.Should().BeApproximately(4.5, 0.01);
    }

    [Fact]
    public void FreshnessScore_WithinThreeMonths_ShouldReturn1()
    {
        var video = CreateVideo(views: 1000, likes: 0, publishedAt: ReferenceDate.AddDays(-60));
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // SonPuan = (1.0 * 1.5) + 1 + 0 = 2.5
        score.Should().BeApproximately(2.5, 0.01);
    }

    [Fact]
    public void FreshnessScore_OlderThanThreeMonths_ShouldReturn0()
    {
        var video = CreateVideo(views: 1000, likes: 0, publishedAt: ReferenceDate.AddDays(-100));
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // SonPuan = (1.0 * 1.5) + 0 + 0 = 1.5
        score.Should().BeApproximately(1.5, 0.01);
    }

    [Fact]
    public void EngagementScore_Video_ShouldCalculateCorrectly()
    {
        // Etkileşim = (beğeni/görüntülenme) * 10
        var video = CreateVideo(views: 10000, likes: 500, publishedAt: ReferenceDate.AddDays(-100));
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // TemelPuan = 10 + 5 = 15
        // SonPuan = (15 * 1.5) + 0 + (500/10000)*10 = 22.5 + 0 + 0.5 = 23
        score.Should().BeApproximately(23.0, 0.01);
    }

    [Fact]
    public void EngagementScore_Article_ShouldCalculateCorrectly()
    {
        // Etkileşim = (tepki/okumaSüresi) * 5
        var article = CreateArticle(readingTime: 10, reactions: 100, publishedAt: ReferenceDate.AddDays(-100));
        var score = _scorer.CalculateScore(article, ReferenceDate);

        // TemelPuan = 10 + 100/50 = 12
        // SonPuan = (12 * 1.0) + 0 + (100/10)*5 = 12 + 0 + 50 = 62
        score.Should().BeApproximately(62.0, 0.01);
    }

    [Fact]
    public void EngagementScore_Video_ZeroViews_ShouldReturnZero()
    {
        var video = CreateVideo(views: 0, likes: 100, publishedAt: ReferenceDate.AddDays(-100));
        var score = _scorer.CalculateScore(video, ReferenceDate);

        // TemelPuan = 0/1000 + 100/100 = 1.0
        // Etkileşim = 0 (görüntülenme = 0)
        // SonPuan = (1.0 * 1.5) + 0 + 0 = 1.5
        score.Should().BeApproximately(1.5, 0.01);
    }

    [Fact]
    public void EngagementScore_Article_ZeroReadingTime_ShouldReturnZero()
    {
        var article = CreateArticle(readingTime: 0, reactions: 100, publishedAt: ReferenceDate.AddDays(-100));
        var score = _scorer.CalculateScore(article, ReferenceDate);

        // TemelPuan = 0 + 100/50 = 2
        // Etkileşim = 0 (okumaSüresi = 0)
        // SonPuan = (2.0 * 1.0) + 0 + 0 = 2.0
        score.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public void FinalScore_MockDataVideoV1_ShouldBeCorrect()
    {
        // Sağlayıcı 1, öğe v1: "Go Programming Tutorial"
        // Görüntülenme: 15000, Beğeni: 1200, Yayın: 2024-03-15
        var content = CreateVideo(views: 15000, likes: 1200,
            publishedAt: new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc));

        var score = _scorer.CalculateScore(content, ReferenceDate);

        // TemelPuan = 15 + 12 = 27
        // TürKatsayısı = 1.5
        // Tazelik = 5 gün → +5
        // Etkileşim = (1200/15000) * 10 = 0.8
        // Sonuç = (27 * 1.5) + 5 + 0.8 = 46.3
        score.Should().BeApproximately(46.3, 0.01);
    }

    [Fact]
    public void FinalScore_MockDataArticleA1_ShouldBeCorrect()
    {
        // Sağlayıcı 2, öğe a1: "Clean Architecture in Go"
        // OkumaSüresi: 8, Tepki: 450, Yayın: 2024-03-14
        var content = CreateArticle(readingTime: 8, reactions: 450,
            publishedAt: new DateTime(2024, 3, 14, 0, 0, 0, DateTimeKind.Utc));

        var score = _scorer.CalculateScore(content, ReferenceDate);

        // TemelPuan = 8 + 9 = 17
        // TürKatsayısı = 1.0
        // Tazelik = 6 gün → +5
        // Etkileşim = (450/8) * 5 = 281.25
        // Sonuç = (17 * 1.0) + 5 + 281.25 = 303.25
        score.Should().BeApproximately(303.25, 0.01);
    }

    [Fact]
    public void CalculateScore_WithoutReferenceDate_ShouldUseUtcNow()
    {
        var video = CreateVideo(views: 1000, likes: 0, publishedAt: DateTime.UtcNow.AddDays(-3));
        var score = _scorer.CalculateScore(video);

        // Tazelik +5 içermeli (1 hafta içi)
        // TemelPuan = 1 * 1.5 = 1.5 + 5 + 0 = 6.5
        score.Should().BeApproximately(6.5, 0.01);
    }

    [Fact]
    public void FreshnessScore_ExactlySevenDays_ShouldReturn5()
    {
        var score = ContentScorer.CalculateFreshnessScore(
            ReferenceDate.AddDays(-7), ReferenceDate);
        score.Should().Be(5);
    }

    [Fact]
    public void FreshnessScore_ExactlyThirtyDays_ShouldReturn3()
    {
        var score = ContentScorer.CalculateFreshnessScore(
            ReferenceDate.AddDays(-30), ReferenceDate);
        score.Should().Be(3);
    }

    [Fact]
    public void FreshnessScore_ExactlyNinetyDays_ShouldReturn1()
    {
        var score = ContentScorer.CalculateFreshnessScore(
            ReferenceDate.AddDays(-90), ReferenceDate);
        score.Should().Be(1);
    }
}
