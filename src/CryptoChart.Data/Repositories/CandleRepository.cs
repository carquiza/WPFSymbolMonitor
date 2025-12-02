using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using CryptoChart.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CryptoChart.Data.Repositories;

/// <summary>
/// Repository implementation for Candle entities.
/// </summary>
public class CandleRepository : ICandleRepository
{
    private readonly CryptoDbContext _context;

    public CandleRepository(CryptoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Candle>> GetCandlesAsync(
        int symbolId,
        TimeFrame timeFrame,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.Candles
            .AsNoTracking()
            .Where(c => c.SymbolId == symbolId 
                && c.TimeFrame == timeFrame 
                && c.OpenTime >= startTime 
                && c.OpenTime <= endTime)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Candle>> GetLatestCandlesAsync(
        int symbolId,
        TimeFrame timeFrame,
        int count,
        CancellationToken cancellationToken = default)
    {
        var candles = await _context.Candles
            .AsNoTracking()
            .Where(c => c.SymbolId == symbolId && c.TimeFrame == timeFrame)
            .OrderByDescending(c => c.OpenTime)
            .Take(count)
            .ToListAsync(cancellationToken);

        // Return in chronological order
        return candles.OrderBy(c => c.OpenTime);
    }

    public async Task<Candle?> GetLatestCandleAsync(
        int symbolId,
        TimeFrame timeFrame,
        CancellationToken cancellationToken = default)
    {
        return await _context.Candles
            .AsNoTracking()
            .Where(c => c.SymbolId == symbolId && c.TimeFrame == timeFrame)
            .OrderByDescending(c => c.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Candle?> GetOldestCandleAsync(
        int symbolId,
        TimeFrame timeFrame,
        CancellationToken cancellationToken = default)
    {
        return await _context.Candles
            .AsNoTracking()
            .Where(c => c.SymbolId == symbolId && c.TimeFrame == timeFrame)
            .OrderBy(c => c.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default)
    {
        var candleList = candles.ToList();
        if (candleList.Count == 0) return;

        // Use bulk insert for better performance
        await _context.Candles.AddRangeAsync(candleList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertAsync(Candle candle, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Candles
            .FirstOrDefaultAsync(c => 
                c.SymbolId == candle.SymbolId 
                && c.TimeFrame == candle.TimeFrame 
                && c.OpenTime == candle.OpenTime, 
                cancellationToken);

        if (existing != null)
        {
            // Update existing candle
            existing.Open = candle.Open;
            existing.High = candle.High;
            existing.Low = candle.Low;
            existing.Close = candle.Close;
            existing.Volume = candle.Volume;
            existing.QuoteVolume = candle.QuoteVolume;
            existing.TradeCount = candle.TradeCount;
            existing.CloseTime = candle.CloseTime;
        }
        else
        {
            // Add new candle
            await _context.Candles.AddAsync(candle, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        int symbolId,
        TimeFrame timeFrame,
        CancellationToken cancellationToken = default)
    {
        return await _context.Candles
            .CountAsync(c => c.SymbolId == symbolId && c.TimeFrame == timeFrame, cancellationToken);
    }
}
