using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using CryptoChart.Data.Context;
using Microsoft.EntityFrameworkCore;

// Note: This is duplicated from Services to avoid a dependency issue.
// News articles are stored with base asset symbols (BTC, ETH) not trading pairs (BTCUSDT).

namespace CryptoChart.Data.Repositories;

/// <summary>
/// Repository for managing NewsArticle entities.
/// </summary>
public class NewsRepository : INewsRepository
{
    private readonly CryptoDbContext _context;

    public NewsRepository(CryptoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<NewsArticle>> GetNewsAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        // Convert trading pair (BTCUSDT) to base asset (BTC) for querying
        // News articles are stored with base asset symbols
        var baseAsset = GetBaseAsset(symbol);
        
        return await _context.NewsArticles
            .Where(n => n.Symbol == baseAsset &&
                        n.PublishedAt >= startTime &&
                        n.PublishedAt <= endTime)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<NewsArticle>> GetNewsForCandleAsync(
        string symbol,
        DateTime candleOpenTime,
        DateTime candleCloseTime,
        CancellationToken cancellationToken = default)
    {
        var baseAsset = GetBaseAsset(symbol);
        
        return await _context.NewsArticles
            .Where(n => n.Symbol == baseAsset &&
                        n.PublishedAt >= candleOpenTime &&
                        n.PublishedAt < candleCloseTime)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<NewsArticle>> GetLatestNewsAsync(
        string symbol,
        int count,
        CancellationToken cancellationToken = default)
    {
        var baseAsset = GetBaseAsset(symbol);
        
        return await _context.NewsArticles
            .Where(n => n.Symbol == baseAsset)
            .OrderByDescending(n => n.PublishedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<NewsArticle?> GetLatestAsync(
        string symbol,
        NewsSource source,
        CancellationToken cancellationToken = default)
    {
        var baseAsset = GetBaseAsset(symbol);
        
        return await _context.NewsArticles
            .Where(n => n.Symbol == baseAsset && n.Source == source)
            .OrderByDescending(n => n.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<NewsArticle> articles,
        CancellationToken cancellationToken = default)
    {
        var articleList = articles.ToList();
        if (!articleList.Any())
            return;

        // Filter out duplicates that already exist
        var existingIds = await _context.NewsArticles
            .Where(n => articleList.Select(a => a.ExternalId).Contains(n.ExternalId))
            .Select(n => new { n.ExternalId, n.Source })
            .ToListAsync(cancellationToken);

        var existingSet = existingIds
            .Select(e => $"{e.ExternalId}:{e.Source}")
            .ToHashSet();

        var newArticles = articleList
            .Where(a => !existingSet.Contains($"{a.ExternalId}:{a.Source}"))
            .ToList();

        if (newArticles.Any())
        {
            await _context.NewsArticles.AddRangeAsync(newArticles, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(
        string externalId,
        NewsSource source,
        CancellationToken cancellationToken = default)
    {
        return await _context.NewsArticles
            .AnyAsync(n => n.ExternalId == externalId && n.Source == source, 
                cancellationToken);
    }

    public async Task<IEnumerable<string>> GetSymbolsWithNewsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.NewsArticles
            .Select(n => n.Symbol)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var baseAsset = GetBaseAsset(symbol);
        
        return await _context.NewsArticles
            .CountAsync(n => n.Symbol == baseAsset, cancellationToken);
    }

    public async Task<IEnumerable<NewsArticle>> GetBySentimentAsync(
        string symbol,
        bool? isBullish,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var baseAsset = GetBaseAsset(symbol);
        
        var query = _context.NewsArticles
            .Where(n => n.Symbol == baseAsset && n.SentimentScore.HasValue);

        if (isBullish.HasValue)
        {
            if (isBullish.Value)
            {
                // Bullish: positive sentiment
                query = query.Where(n => n.SentimentScore > 0.1m);
            }
            else
            {
                // Bearish: negative sentiment
                query = query.Where(n => n.SentimentScore < -0.1m);
            }
        }
        else
        {
            // Neutral: near-zero sentiment
            query = query.Where(n => n.SentimentScore >= -0.1m && n.SentimentScore <= 0.1m);
        }

        return await query
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    #region Helpers

    /// <summary>
    /// Extracts the base asset from a trading pair symbol.
    /// For example, "BTCUSDT" -> "BTC", "ETHBTC" -> "ETH".
    /// This is needed because news articles are stored with base asset symbols.
    /// </summary>
    private static string GetBaseAsset(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return symbol;

        symbol = symbol.ToUpperInvariant();

        // Handle common quote currencies
        if (symbol.EndsWith("USDT"))
            return symbol[..^4];
        if (symbol.EndsWith("BUSD"))
            return symbol[..^4];
        if (symbol.EndsWith("USDC"))
            return symbol[..^4];
        if (symbol.EndsWith("BTC") && symbol.Length > 3)
            return symbol[..^3];
        if (symbol.EndsWith("ETH") && symbol.Length > 3)
            return symbol[..^3];

        return symbol;
    }

    #endregion
}
