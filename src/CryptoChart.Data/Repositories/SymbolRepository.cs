using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using CryptoChart.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CryptoChart.Data.Repositories;

/// <summary>
/// Repository implementation for Symbol entities.
/// </summary>
public class SymbolRepository : ISymbolRepository
{
    private readonly CryptoDbContext _context;

    public SymbolRepository(CryptoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Symbol>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Symbols
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Symbol>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Symbols
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Symbol?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
    }

    public async Task<Symbol?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Symbols
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Symbol> AddAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _context.Symbols.Add(symbol);
        await _context.SaveChangesAsync(cancellationToken);
        return symbol;
    }

    public async Task UpdateAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _context.Symbols.Update(symbol);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
