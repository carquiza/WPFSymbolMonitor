using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using CryptoChart.Data.Context;
using Microsoft.EntityFrameworkCore;

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
        return await _context.NewsArticles
            .Where(n => n.Symbol == symbol &&
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
        return await _context.NewsArticles
            .Where(n => n.Symbol == symbol &&
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
        return await _context.NewsArticles
            .Where(n => n.Symbol == symbol)
            .OrderByDescending(n => n.PublishedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<NewsArticle?> GetLatestAsync(
        string symbol,
        NewsSource source,
        CancellationToken cancellationToken = default)
    {
        return await _context.NewsArticles
            .Where(n => n.Symbol == symbol && n.Source == source)
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
        return await _context.NewsArticles
            .CountAsync(n => n.Symbol == symbol, cancellationToken);
    }

    public async Task<IEnumerable<NewsArticle>> GetBySentimentAsync(
        string symbol,
        bool? isBullish,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _context.NewsArticles
            .Where(n => n.Symbol == symbol && n.SentimentScore.HasValue);

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
}
