using Microsoft.EntityFrameworkCore;
using SearchEngine.Domain.Entities;

namespace SearchEngine.Infrastructure.Data;

/// <summary>
/// Arama motoru icin Entity Framework Core veritabani baglam sinifi.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>Tum saglayicilardan gelen icerik ogeleri.</summary>
    public DbSet<Content> Contents => Set<Content>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Content>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExternalId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Title)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.SourceProvider)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Duration)
                .HasMaxLength(20);

            entity.Property(e => e.ContentType)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Etiketler yerel PostgreSQL text[] dizisi olarak saklanir
            entity.Property(e => e.Tags)
                .HasColumnType("text[]");

            // Indeksler
            entity.HasIndex(e => new { e.ExternalId, e.SourceProvider })
                .IsUnique();

            entity.HasIndex(e => e.FinalScore)
                .IsDescending();

            entity.HasIndex(e => e.PublishedAt);

            entity.HasIndex(e => e.Title);

            entity.HasIndex(e => e.ContentType);
        });
    }
}
