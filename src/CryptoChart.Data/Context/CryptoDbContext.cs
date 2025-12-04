using CryptoChart.Core.Enums;
using CryptoChart.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoChart.Data.Context;

/// <summary>
/// Entity Framework DbContext for the crypto chart database.
/// </summary>
public class CryptoDbContext : DbContext
{
    public CryptoDbContext(DbContextOptions<CryptoDbContext> options) : base(options)
    {
    }

    public DbSet<Symbol> Symbols => Set<Symbol>();
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Symbol configuration
        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(20);
            
            entity.Property(e => e.BaseAsset)
                .IsRequired()
                .HasMaxLength(10);
            
            entity.Property(e => e.QuoteAsset)
                .IsRequired()
                .HasMaxLength(10);

            entity.HasIndex(e => e.Name)
                .IsUnique();

            // Seed data for our target symbols
            entity.HasData(
                new Symbol { Id = 1, Name = "BTCUSDT", BaseAsset = "BTC", QuoteAsset = "USDT", IsActive = true },
                new Symbol { Id = 2, Name = "ETHUSDT", BaseAsset = "ETH", QuoteAsset = "USDT", IsActive = true },
                new Symbol { Id = 3, Name = "ETHBTC", BaseAsset = "ETH", QuoteAsset = "BTC", IsActive = true }
            );
        });

        // Candle configuration
        modelBuilder.Entity<Candle>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TimeFrame)
                .HasConversion<string>()
                .HasMaxLength(10);

            entity.Property(e => e.Open).HasPrecision(18, 8);
            entity.Property(e => e.High).HasPrecision(18, 8);
            entity.Property(e => e.Low).HasPrecision(18, 8);
            entity.Property(e => e.Close).HasPrecision(18, 8);
            entity.Property(e => e.Volume).HasPrecision(18, 8);
            entity.Property(e => e.QuoteVolume).HasPrecision(18, 8);

            // Composite index for efficient queries
            entity.HasIndex(e => new { e.SymbolId, e.TimeFrame, e.OpenTime })
                .IsUnique();

            // Index for time-based queries
            entity.HasIndex(e => e.OpenTime);

            // Relationship
            entity.HasOne(e => e.Symbol)
                .WithMany(s => s.Candles)
                .HasForeignKey(e => e.SymbolId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // NewsArticle configuration
        modelBuilder.Entity<NewsArticle>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExternalId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Source)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.Symbol)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Headline)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Summary)
                .HasMaxLength(2000);

            entity.Property(e => e.Url)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.ImageUrl)
                .HasMaxLength(1000);

            entity.Property(e => e.Publisher)
                .HasMaxLength(100);

            entity.Property(e => e.SentimentLabel)
                .HasMaxLength(50);

            entity.Property(e => e.Category)
                .HasMaxLength(50);

            entity.Property(e => e.SentimentScore)
                .HasPrecision(5, 4);

            entity.Property(e => e.RelevanceScore)
                .HasPrecision(5, 4);

            // Unique constraint on ExternalId + Source to prevent duplicates
            entity.HasIndex(e => new { e.ExternalId, e.Source })
                .IsUnique();

            // Index for querying by symbol and date range
            entity.HasIndex(e => new { e.Symbol, e.PublishedAt });

            // Index for time-based queries
            entity.HasIndex(e => e.PublishedAt);

            // Ignore computed properties
            entity.Ignore(e => e.IsBullish);
            entity.Ignore(e => e.IsBearish);
            entity.Ignore(e => e.IsNeutral);
            entity.Ignore(e => e.SentimentCategory);
        });
    }
}
